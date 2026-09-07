using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MeltySynth;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace SS14_MIDI_IDE
{
    public class MidiTrackPlayer
    {
        public MeltySynth.MidiFile MidiFile;
        public MidiFileSequencer Sequencer;
        public Synthesizer Synthesizer;
        public bool Muted;
        public int Program = 0;
        public TempoMap TempoMap;
        public List<Note> Notes;
        public int NextNoteIndex = 0;
        public List<Note> ActiveNotes = new List<Note>();
        public List<TimedEvent> ModulatorEvents;
        public int NextModulatorEventIndex = 0;
        public List<TimedEvent> ProgramEvents;
        public int NextProgramEventIndex = 0;
        public int CurrentBank = 0;
        public bool IsDrumTrack = false;
    }

    public class AudioEngine : IWaveProvider, IDisposable
    {
        private SoundFont _cachedSoundFont;
        private readonly WaveOut _waveOut;
        private readonly WaveFormat _waveFormat;
        private readonly object _lock = new object();
        private volatile bool _isPlaying = false;
        
        public bool IsMetronomeEnabled { get; set; } = false;

        private List<MidiTrackPlayer> _trackPlayers = new List<MidiTrackPlayer>();
        private MidiTrackPlayer _metronomePlayer;
        private WaveformSynth _waveformSynth;
        
        public Dictionary<int, FtmInstrument> CustomFtmInstruments { get; set; }
        public Dictionary<int, FtmSequence> CustomFtmSequences { get; set; }
        public Dictionary<(int track, long tick), int> FtmNoteInstruments { get; set; }
        public int EngineSpeed { get; set; } = 60;
        
        private long _samplesRendered = 0;

        private WaveStream? _referenceWaveStream;
        private ISampleProvider? _referenceSampleProvider;
        private int _referenceSilenceFrames = 0;

        public double ReferenceOffsetSeconds { get; set; } = 0;
        public double ReferenceMix { get; set; } = 0.0;

        public AudioEngine(Stream soundFontStream, int sampleRate)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            _cachedSoundFont = new SoundFont(soundFontStream);
            _waveformSynth = new WaveformSynth(sampleRate, 2);

            _waveOut = new WaveOut();
            _waveOut.Init(this);
            _waveOut.Play();
        }

        public static int CustomInstrumentBaseIndex { get; private set; } = 128;

        public string[] GetInstrumentNames()
        {
            if (_cachedSoundFont == null) return Array.Empty<string>();
            var instruments = new List<string>();
            foreach (var inst in _cachedSoundFont.Instruments)
            {
                instruments.Add(inst.Name);
            }
            
            CustomInstrumentBaseIndex = instruments.Count;

            // Add custom 8-bit instruments
            instruments.Add("Pulse 12.5%");
            instruments.Add("Pulse 25%");
            instruments.Add("Pulse 50%");
            instruments.Add("Pulse 75%");
            instruments.Add("Triangle Wave");
            instruments.Add("Sawtooth Wave");
            instruments.Add("Noise");
            
            if (CustomFtmInstruments != null)
            {
                foreach (var kvp in CustomFtmInstruments.OrderBy(k => k.Key))
                {
                    instruments.Add($"FTM: {kvp.Value.Name}");
                }
            }
            
            return instruments.ToArray();
        }

        public WaveFormat WaveFormat => _waveFormat;

        public double CurrentTimeSeconds
        {
            get
            {
                return (double)_samplesRendered / _waveFormat.SampleRate;
            }
        }

        public void LoadMidi(string filePath, string metronomeFilePath = null)
        {
            // Fallback for empty/tests
        }

        public void LoadMidi(System.IO.Stream stream, System.IO.Stream metronomeStream = null)
        {
            // Fallback for empty/tests
        }

        public void LoadMidiTracks(List<Stream> trackStreams, Stream metronomeStream)
        {
            lock (_lock)
            {
                _trackPlayers.Clear();
                var settings = new SynthesizerSettings(_waveFormat.SampleRate);
                
                foreach (var stream in trackStreams)
                {
                    var player = new MidiTrackPlayer();
                    
                    long startPos = stream.Position;
                    player.MidiFile = new MeltySynth.MidiFile(stream);
                    player.Synthesizer = new Synthesizer(_cachedSoundFont, settings);
                    player.Sequencer = new MidiFileSequencer(player.Synthesizer);
                    player.Sequencer.Play(player.MidiFile, true);
                    
                    stream.Position = startPos;
                    var dryWetFile = Melanchall.DryWetMidi.Core.MidiFile.Read(stream);
                    player.TempoMap = dryWetFile.GetTempoMap();
                    player.Notes = new List<Note>(dryWetFile.GetNotes());
                    player.NextNoteIndex = 0;
                    player.ActiveNotes.Clear();
                    
                    int trackChannel = player.Notes.FirstOrDefault()?.Channel ?? -1;
                    
                    player.ModulatorEvents = dryWetFile.GetTimedEvents()
                        .Where(e => (e.Event is PitchBendEvent pb && pb.Channel == trackChannel) || 
                                   (e.Event is ControlChangeEvent cc && cc.Channel == trackChannel && (cc.ControlNumber == 1 || cc.ControlNumber == 7 || cc.ControlNumber == 11 || cc.ControlNumber == 5 || cc.ControlNumber == 65)))
                        .OrderBy(e => e.Time).ToList();
                    player.NextModulatorEventIndex = 0;

                    player.ProgramEvents = dryWetFile.GetTimedEvents()
                        .Where(e => (e.Event is ProgramChangeEvent pc && pc.Channel == trackChannel) || 
                                    (e.Event is ControlChangeEvent cc && cc.Channel == trackChannel && cc.ControlNumber == 0))
                        .OrderBy(e => e.Time)
                        .ThenBy(e => e.Event is ControlChangeEvent ? 0 : 1)
                        .ToList();
                    

                    
                    player.NextProgramEventIndex = 0;
                    player.CurrentBank = 0;
                    
                    player.IsDrumTrack = player.Notes.Any(n => n.Channel == 9);
                    player.Muted = false;
                    _trackPlayers.Add(player);
                }

                if (metronomeStream != null)
                {
                    _metronomePlayer = new MidiTrackPlayer();
                    _metronomePlayer.MidiFile = new MeltySynth.MidiFile(metronomeStream);
                    _metronomePlayer.Synthesizer = new Synthesizer(_cachedSoundFont, settings);
                    _metronomePlayer.Sequencer = new MidiFileSequencer(_metronomePlayer.Synthesizer);
                    _metronomePlayer.Sequencer.Play(_metronomePlayer.MidiFile, true);
                }
                else
                {
                    _metronomePlayer = null;
                }

                _samplesRendered = 0;
                _isPlaying = false;
            }
        }

        public void SetTrackMute(int index, bool muted)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _trackPlayers.Count)
                {
                    _trackPlayers[index].Muted = muted;
                    _waveformSynth?.SetTrackMute(index, muted);
                }
            }
        }

        public void LoadReferenceTrack(string filePath)
        {
            lock (_lock)
            {
                if (_referenceWaveStream != null)
                {
                    _referenceWaveStream.Dispose();
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    _referenceWaveStream = null;
                    _referenceSampleProvider = null;
                    return;
                }

                _referenceWaveStream = new MediaFoundationReader(filePath);
                var waveChannel = new WaveChannel32(_referenceWaveStream);
                
                var resampler = new MediaFoundationResampler(waveChannel, _waveFormat);
                resampler.ResamplerQuality = 60;
                
                _referenceSampleProvider = resampler.ToSampleProvider();
                
                if (CurrentTimeSeconds > ReferenceOffsetSeconds)
                {
                    double targetSec = CurrentTimeSeconds - ReferenceOffsetSeconds;
                    _referenceWaveStream.Position = (long)(targetSec * _referenceWaveStream.WaveFormat.AverageBytesPerSecond);
                }
            }
        }

        public void Play()
        {
            lock (_lock)
            {
                _isPlaying = true;
                if (_waveOut.PlaybackState != PlaybackState.Playing)
                    _waveOut.Play();
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                _isPlaying = false;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isPlaying = false;
                if (_trackPlayers.Count > 0)
                {
                    Seek(0);
                }
            }
        }

        public bool IsPlaying
        {
            get { return _isPlaying; }
        }

        public void NoteOn(int trackIndex, int channel, int key, int velocity)
        {
            lock (_lock)
            {
                if (trackIndex >= 0 && trackIndex < _trackPlayers.Count) _trackPlayers[trackIndex].Synthesizer.NoteOn(channel, key, velocity);
            }
        }

        public void NoteOff(int trackIndex, int channel, int key)
        {
            lock (_lock)
            {
                if (trackIndex >= 0 && trackIndex < _trackPlayers.Count) _trackPlayers[trackIndex].Synthesizer.NoteOff(channel, key);
            }
        }

        public void PreviewNoteOff(int trackIndex, int key)
        {
            lock (_lock)
            {
                // Turn off on all tracks in case trackIndex changed while key was held
                for (int i = 0; i < _trackPlayers.Count; i++)
                {
                    _waveformSynth?.NoteOff(i, key);
                    _trackPlayers[i].Synthesizer.NoteOff(0, key);
                }
            }
        }

        public void PreviewNoteOn(int trackIndex, int key, int programOverride = -1)
        {
            lock (_lock)
            {
                // Simple track mapping for preview waveform type
                FtmWaveformType type = FtmWaveformType.Square50;
                if (trackIndex == 2) type = FtmWaveformType.Triangle;
                else if (trackIndex == 3) type = FtmWaveformType.Noise;
                else if (trackIndex == 4) type = FtmWaveformType.Sawtooth;
                
                FtmInstrument inst = null;
                if (CustomFtmInstruments != null && CustomInstrumentBaseIndex > 0)
                {
                    int currentProg = programOverride >= 0 ? programOverride : (trackIndex >= 0 && trackIndex < _trackPlayers.Count ? _trackPlayers[trackIndex].Program : -1);
                    if (currentProg >= CustomInstrumentBaseIndex + 7)
                    {
                        var orderedKeys = CustomFtmInstruments.Keys.OrderBy(k => k).ToList();
                        int orderIndex = currentProg - (CustomInstrumentBaseIndex + 7);
                        if (orderIndex >= 0 && orderIndex < orderedKeys.Count)
                        {
                            inst = CustomFtmInstruments[orderedKeys[orderIndex]];
                        }
                    }
                    else if (currentProg >= CustomInstrumentBaseIndex && currentProg <= CustomInstrumentBaseIndex + 6)
                    {
                        if (currentProg == CustomInstrumentBaseIndex) type = FtmWaveformType.Square12;
                        else if (currentProg == CustomInstrumentBaseIndex + 1) type = FtmWaveformType.Square25;
                        else if (currentProg == CustomInstrumentBaseIndex + 2) type = FtmWaveformType.Square50;
                        else if (currentProg == CustomInstrumentBaseIndex + 3) type = FtmWaveformType.Square75;
                        else if (currentProg == CustomInstrumentBaseIndex + 4) type = FtmWaveformType.Triangle;
                        else if (currentProg == CustomInstrumentBaseIndex + 5) type = FtmWaveformType.Sawtooth;
                        else if (currentProg == CustomInstrumentBaseIndex + 6) type = FtmWaveformType.Noise;
                    }
                }
                
                _waveformSynth?.NoteOn(trackIndex, key, type, inst);
            }
        }
        
        public void Rewind()
        {
            Seek(0);
        }

        public void Seek(double seconds)
        {
            lock (_lock)
            {
                if (_trackPlayers.Count == 0) return;

                int targetSamples = (int)(seconds * _waveFormat.SampleRate);
                if (targetSamples < 0) targetSamples = 0;

                // Reset all
                for (int i = 0; i < _trackPlayers.Count; i++)
                {
                    var player = _trackPlayers[i];
                    player.Sequencer = new MidiFileSequencer(player.Synthesizer);
                    player.Sequencer.Play(player.MidiFile, true);
                    
                    if (player.Notes != null)
                    {
                        player.NextNoteIndex = 0;
                        foreach (var activeNote in player.ActiveNotes)
                            _waveformSynth?.NoteOff(i, activeNote.NoteNumber);
                        player.ActiveNotes.Clear();
                        
                        // Fast forward NextNoteIndex to the seek time
                        while (player.NextNoteIndex < player.Notes.Count)
                        {
                            if (player.Notes[player.NextNoteIndex].TimeAs<MetricTimeSpan>(player.TempoMap).TotalMicroseconds / 1000000.0 < seconds)
                                player.NextNoteIndex++;
                            else
                                break;
                        }
                        
                        if (player.ModulatorEvents != null)
                        {
                            player.NextModulatorEventIndex = 0;
                            // reset to defaults
                            _waveformSynth?.SetPitchBend(i, 8192);
                            _waveformSynth?.SetModulation(i, 0);
                            _waveformSynth?.SetVolume(i, 100);
                            
                            while (player.NextModulatorEventIndex < player.ModulatorEvents.Count)
                            {
                                var ev = player.ModulatorEvents[player.NextModulatorEventIndex];
                                if (ev.TimeAs<MetricTimeSpan>(player.TempoMap).TotalMicroseconds / 1000000.0 < seconds)
                                {
                                    if (ev.Event is PitchBendEvent pb) _waveformSynth?.SetPitchBend(i, pb.PitchValue);
                                    else if (ev.Event is ControlChangeEvent cc)
                                    {
                                        if (cc.ControlNumber == 1) _waveformSynth?.SetModulation(i, cc.ControlValue);
                                        else if (cc.ControlNumber == 7 || cc.ControlNumber == 11) _waveformSynth?.SetVolume(i, cc.ControlValue);
                                        else if (cc.ControlNumber == 65) _waveformSynth?.SetPortamento(i, cc.ControlValue >= 64);
                                        else if (cc.ControlNumber == 5) _waveformSynth?.SetPortamentoTime(i, cc.ControlValue);
                                    }
                                    player.NextModulatorEventIndex++;
                                }
                                else break;
                            }
                        }
                    }
                }
                if (_metronomePlayer != null)
                {
                    _metronomePlayer.Sequencer = new MidiFileSequencer(_metronomePlayer.Synthesizer);
                    _metronomePlayer.Sequencer.Play(_metronomePlayer.MidiFile, true);
                }

                int speedMultiplier = 100;
                int targetSpeedSamples = targetSamples / speedMultiplier;
                
                foreach (var player in _trackPlayers) player.Sequencer.Speed = speedMultiplier;
                if (_metronomePlayer != null) _metronomePlayer.Sequencer.Speed = speedMultiplier;

                int rendered = 0;
                float[] devNull = new float[4096 * _waveFormat.Channels]; // larger buffer to reduce loop overhead
                
                // Fast-forward at 100x speed
                while (rendered < targetSpeedSamples)
                {
                    int toRender = Math.Min(targetSpeedSamples - rendered, 4096);
                    foreach (var player in _trackPlayers)
                    {
                        player.Sequencer.RenderInterleaved(devNull.AsSpan(0, toRender * _waveFormat.Channels));
                    }
                    if (_metronomePlayer != null)
                    {
                        _metronomePlayer.Sequencer.RenderInterleaved(devNull.AsSpan(0, toRender * _waveFormat.Channels));
                    }
                    rendered += toRender;
                }
                
                // Restore speed and render the remaining samples at normal speed for perfect alignment
                int remainingSamples = targetSamples - (targetSpeedSamples * speedMultiplier);
                
                foreach (var player in _trackPlayers) player.Sequencer.Speed = 1.0f;
                if (_metronomePlayer != null) _metronomePlayer.Sequencer.Speed = 1.0f;
                
                rendered = 0;
                while (rendered < remainingSamples)
                {
                    int toRender = Math.Min(remainingSamples - rendered, 4096);
                    foreach (var player in _trackPlayers)
                    {
                        player.Sequencer.RenderInterleaved(devNull.AsSpan(0, toRender * _waveFormat.Channels));
                    }
                    if (_metronomePlayer != null)
                    {
                        _metronomePlayer.Sequencer.RenderInterleaved(devNull.AsSpan(0, toRender * _waveFormat.Channels));
                    }
                    rendered += toRender;
                }
                
                _samplesRendered = targetSamples;

                if (_referenceWaveStream != null)
                {
                    double refTime = seconds - ReferenceOffsetSeconds;
                    if (refTime <= 0)
                    {
                        _referenceWaveStream.Position = 0;
                        _referenceSilenceFrames = (int)(-refTime * _waveFormat.SampleRate);
                    }
                    else
                    {
                        _referenceSilenceFrames = 0;
                        _referenceWaveStream.Position = (long)(refTime * _referenceWaveStream.WaveFormat.AverageBytesPerSecond);
                        _referenceWaveStream.Position -= _referenceWaveStream.Position % _referenceWaveStream.WaveFormat.BlockAlign;
                    }
                }
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return Read(new Span<byte>(buffer, offset, count));
        }

        public int Read(Span<byte> buffer)
        {
            int count = buffer.Length;
            int floatCount = count / sizeof(float);
            int frames = floatCount / _waveFormat.Channels;
            if (frames <= 0) return 0;

            var temp = new float[frames * _waveFormat.Channels];
            Array.Clear(temp, 0, temp.Length);

            lock (_lock)
            {
                if (_trackPlayers.Count > 0)
                {
                    var trackTemp = new float[frames * _waveFormat.Channels];
                    
                    for (int i = 0; i < _trackPlayers.Count; i++)
                    {
                        var player = _trackPlayers[i];
                        Array.Clear(trackTemp, 0, trackTemp.Length);

                        bool isFtmTrack = FtmNoteInstruments != null && FtmNoteInstruments.Keys.Any(k => k.track == i);
                        if (isFtmTrack || player.Program >= CustomInstrumentBaseIndex)
                        {
                            if (_isPlaying && player.Notes != null)
                            {
                                double currentTime = (double)_samplesRendered / _waveFormat.SampleRate;
                                double bufferDuration = (double)frames / _waveFormat.SampleRate;
                                double endTime = currentTime + bufferDuration;
                                
                                // Turn off active notes that end in this buffer
                                for (int k = player.ActiveNotes.Count - 1; k >= 0; k--)
                                {
                                    var activeNote = player.ActiveNotes[k];
                                    double noteEndTime = (activeNote.TimeAs<MetricTimeSpan>(player.TempoMap) + activeNote.LengthAs<MetricTimeSpan>(player.TempoMap)).TotalMicroseconds / 1000000.0;
                                    if (noteEndTime <= endTime)
                                    {
                                        _waveformSynth?.NoteOff(i, activeNote.NoteNumber);
                                        player.ActiveNotes.RemoveAt(k);
                                    }
                                }

                                // Process modulator events
                                if (player.ModulatorEvents != null)
                                {
                                    while (player.NextModulatorEventIndex < player.ModulatorEvents.Count)
                                    {
                                        var nextEv = player.ModulatorEvents[player.NextModulatorEventIndex];
                                        double evTime = nextEv.TimeAs<MetricTimeSpan>(player.TempoMap).TotalMicroseconds / 1000000.0;
                                        if (evTime <= endTime)
                                        {
                                            if (nextEv.Event is PitchBendEvent pb) _waveformSynth?.SetPitchBend(i, pb.PitchValue);
                                            else if (nextEv.Event is ControlChangeEvent cc)
                                            {
                                                if (cc.ControlNumber == 1) _waveformSynth?.SetModulation(i, cc.ControlValue);
                                                else if (cc.ControlNumber == 7 || cc.ControlNumber == 11) _waveformSynth?.SetVolume(i, cc.ControlValue);
                                                else if (cc.ControlNumber == 65) _waveformSynth?.SetPortamento(i, cc.ControlValue >= 64);
                                                else if (cc.ControlNumber == 5) _waveformSynth?.SetPortamentoTime(i, cc.ControlValue);
                                            }
                                            player.NextModulatorEventIndex++;
                                        }
                                        else break;
                                    }
                                }

                                // Process program events
                                if (player.ProgramEvents != null)
                                {
                                    while (player.NextProgramEventIndex < player.ProgramEvents.Count)
                                    {
                                        var nextEv = player.ProgramEvents[player.NextProgramEventIndex];
                                        double evTime = nextEv.TimeAs<MetricTimeSpan>(player.TempoMap).TotalMicroseconds / 1000000.0;
                                        if (evTime <= endTime)
                                        {
                                            if (nextEv.Event is ProgramChangeEvent pc)
                                            {
                                                player.Program = (player.CurrentBank * 128) + pc.ProgramNumber;
                                                System.IO.File.AppendAllText("bank_debug.txt", $"Time={evTime:F4} Event=PC Prog={pc.ProgramNumber} ResultingPlayerProg={player.Program} CurrentBank={player.CurrentBank}\n");
                                            }
                                            else if (nextEv.Event is ControlChangeEvent cc && cc.ControlNumber == 0)
                                            {
                                                player.CurrentBank = cc.ControlValue;
                                                System.IO.File.AppendAllText("bank_debug.txt", $"Time={evTime:F4} Event=Bank Val={cc.ControlValue}\n");
                                            }
                                            player.NextProgramEventIndex++;
                                        }
                                        else break;
                                    }
                                }

                                // Turn on new notes that start in this buffer
                                while (player.NextNoteIndex < player.Notes.Count)
                                {
                                    var nextNote = player.Notes[player.NextNoteIndex];
                                    double noteStartTime = nextNote.TimeAs<MetricTimeSpan>(player.TempoMap).TotalMicroseconds / 1000000.0;
                                    
                                    if (noteStartTime <= endTime)
                                    {
                                        FtmWaveformType type = FtmWaveformType.Square50;
                                        if (player.Program == CustomInstrumentBaseIndex) type = FtmWaveformType.Square12;
                                        else if (player.Program == CustomInstrumentBaseIndex + 1) type = FtmWaveformType.Square25;
                                        else if (player.Program == CustomInstrumentBaseIndex + 2) type = FtmWaveformType.Square50;
                                        else if (player.Program == CustomInstrumentBaseIndex + 3) type = FtmWaveformType.Square75;
                                        else if (player.Program == CustomInstrumentBaseIndex + 4) type = FtmWaveformType.Triangle;
                                        else if (player.Program == CustomInstrumentBaseIndex + 5) type = FtmWaveformType.Sawtooth;
                                        else if (player.Program == CustomInstrumentBaseIndex + 6) type = FtmWaveformType.Noise;
                                        
                                        FtmInstrument inst = null;
                                        bool wasAssignedByMap = false;
                                        
                                        if (FtmNoteInstruments != null)
                                        {
                                            System.IO.File.AppendAllText("lookup_debug.txt", $"[LOOKUP TRY] track {i}, tick {nextNote.Time}. Keys count: {FtmNoteInstruments.Count}. ContainsKey: {FtmNoteInstruments.ContainsKey((i, nextNote.Time))}\n");
                                            
                                            if (FtmNoteInstruments.TryGetValue((i, nextNote.Time), out int instId))
                                            {
                                                if (nextNote.Channel == 2) type = FtmWaveformType.Triangle;
                                                else if (nextNote.Channel == 3) type = FtmWaveformType.Noise;
                                                
                                                if (CustomFtmInstruments != null && CustomFtmInstruments.TryGetValue(instId, out var customInst))
                                                {
                                                    inst = customInst;
                                                    wasAssignedByMap = true;
                                                }
                                            }
                                        }
                                        
                                        if (!wasAssignedByMap && player.Program >= CustomInstrumentBaseIndex + 7 && CustomFtmInstruments != null)
                                        {
                                            System.IO.File.AppendAllText("lookup_debug.txt", $"[FALLBACK] Using program index fallback for track {i}, Program {player.Program}\n");
                                            if (nextNote.Channel == 2) type = FtmWaveformType.Triangle;
                                            else if (nextNote.Channel == 3) type = FtmWaveformType.Noise;
                                            
                                            int orderIndex = player.Program - (CustomInstrumentBaseIndex + 7);
                                            var orderedKeys = CustomFtmInstruments.Keys.OrderBy(k => k).ToList();
                                            if (orderIndex >= 0 && orderIndex < orderedKeys.Count)
                                            {
                                                inst = CustomFtmInstruments[orderedKeys[orderIndex]];
                                            }
                                        }
                                        {
                                            if (nextNote.Channel == 2) type = FtmWaveformType.Triangle;
                                            else if (nextNote.Channel == 3) type = FtmWaveformType.Noise;
                                            
                                            int orderIndex = player.Program - (CustomInstrumentBaseIndex + 7);
                                            var orderedKeys = CustomFtmInstruments.Keys.OrderBy(k => k).ToList();
                                            if (orderIndex >= 0 && orderIndex < orderedKeys.Count)
                                            {
                                                inst = CustomFtmInstruments[orderedKeys[orderIndex]];
                                            }
                                        }
                                        
                                        _waveformSynth?.NoteOn(i, nextNote.NoteNumber, type, inst);
                                        player.ActiveNotes.Add(nextNote);
                                        player.NextNoteIndex++;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (_isPlaying)
                                player.Sequencer.RenderInterleaved(trackTemp.AsSpan());
                            else
                                player.Synthesizer.RenderInterleaved(trackTemp.AsSpan());

                            if (!player.Muted)
                            {
                                for (int j = 0; j < temp.Length; j++)
                                    temp[j] += trackTemp[j];
                            }
                        }
                    }

                    if (_metronomePlayer != null)
                    {
                        Array.Clear(trackTemp, 0, trackTemp.Length);
                        _metronomePlayer.Sequencer.RenderInterleaved(trackTemp.AsSpan());
                        if (IsMetronomeEnabled)
                        {
                            for (int j = 0; j < temp.Length; j++)
                                temp[j] += trackTemp[j];
                        }
                    }

                    if (_referenceSampleProvider != null && ReferenceMix > 0.0)
                    {
                        var refBuffer = new float[frames * _waveFormat.Channels];
                        int refFramesRead = 0;
                        
                        if (_referenceSilenceFrames > 0)
                        {
                            int silenceToConsume = Math.Min(frames, _referenceSilenceFrames);
                            _referenceSilenceFrames -= silenceToConsume;
                            refFramesRead = silenceToConsume;
                            
                            if (refFramesRead < frames)
                            {
                                int framesToRead = frames - refFramesRead;
                                int actuallyRead = _referenceSampleProvider.Read(refBuffer.AsSpan(refFramesRead * _waveFormat.Channels, framesToRead * _waveFormat.Channels)) / _waveFormat.Channels;
                                refFramesRead += actuallyRead;
                            }
                        }
                        else
                        {
                            refFramesRead = _referenceSampleProvider.Read(refBuffer.AsSpan(0, floatCount)) / _waveFormat.Channels;
                        }
                        
                        for (int i = 0; i < refFramesRead * _waveFormat.Channels; i++)
                        {
                            temp[i] = (float)((temp[i] * (1.0 - ReferenceMix)) + (refBuffer[i] * ReferenceMix));
                        }
                    }

                    _samplesRendered += frames;
                }

                if (_waveformSynth != null)
                {
                    var synthBuffer = new float[floatCount];
                    _waveformSynth.Read(synthBuffer, 0, floatCount);
                    for (int i = 0; i < floatCount; i++)
                    {
                        temp[i] += synthBuffer[i];
                    }
                }
            }

            var tempBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<float, byte>(temp.AsSpan());
            tempBytes.CopyTo(buffer);
            return count;
        }

        public void SetProgram(int trackIndex, int channel, int program)
        {
            lock (_lock)
            {
                if (trackIndex >= 0 && trackIndex < _trackPlayers.Count)
                {
                    _trackPlayers[trackIndex].Program = program;
                    _trackPlayers[trackIndex].Synthesizer.ProcessMidiMessage(channel, 0xC0, program, 0);
                }
            }
        }

        public void Dispose()
        {
            _waveOut?.Dispose();
            _referenceWaveStream?.Dispose();
        }
    }
}
