using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SS14_MIDI_IDE
{
    public enum FtmWaveformType
    {
        Square12,
        Square25,
        Square50,
        Square75,
        Triangle,
        Sawtooth,
        Noise
    }

    public class WaveformSynth : ISampleProvider
    {
        private class TrackState
        {
            public int PitchBend = 8192;
            public int Modulation = 0;
            public int Volume = 100;
            public bool Muted = false;
            public bool PortamentoEnabled = false;
            public int PortamentoTime = 0;
            public double LastFrequency = 0;
        }

        private class Voice
        {
            public int TrackIndex;
            public int Key;
            public FtmWaveformType Type;
            public double Phase;
            public double BaseFrequency;
            public double CurrentFrequency;
            public double Envelope;
            public bool IsReleasing;
            public FtmInstrument Instrument;
            public int MacroTick;
            public int VolumeOffset;
            public int PitchOffset;
            public int ArpeggioOffset;
            public int DutyOffset;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Dictionary<int, TrackState> _trackStates = new Dictionary<int, TrackState>();
        private readonly WaveFormat _waveFormat;
        private readonly object _lock = new object();
        
        // Very fast attack and release for retro chiptune feel
        private const double AttackRate = 0.05; 
        private const double ReleaseRate = 0.01;

        public Dictionary<int, FtmInstrument> CustomFtmInstruments { get; set; }
        public Dictionary<int, FtmSequence> CustomFtmSequences { get; set; }
        public int EngineSpeed { get; set; } = 60;
        
        private double _fractionalTick = 0;

        public WaveformSynth(int sampleRate, int channels)
        {
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public WaveFormat WaveFormat => _waveFormat;
        
        private Random _random = new Random();

        private TrackState GetTrackState(int trackIndex)
        {
            if (!_trackStates.TryGetValue(trackIndex, out var state))
            {
                state = new TrackState();
                _trackStates[trackIndex] = state;
            }
            return state;
        }

        public void SetPitchBend(int trackIndex, int pitchBend)
        {
            lock (_lock) GetTrackState(trackIndex).PitchBend = pitchBend;
        }

        public void SetModulation(int trackIndex, int modulation)
        {
            lock (_lock) GetTrackState(trackIndex).Modulation = modulation;
        }

        public void SetVolume(int trackIndex, int volume)
        {
            lock (_lock) GetTrackState(trackIndex).Volume = volume;
        }

        public void SetPortamento(int trackIndex, bool enabled)
        {
            lock (_lock) GetTrackState(trackIndex).PortamentoEnabled = enabled;
        }

        public void SetPortamentoTime(int trackIndex, int time)
        {
            lock (_lock) GetTrackState(trackIndex).PortamentoTime = time;
        }

        public void SetTrackMute(int trackIndex, bool muted)
        {
            lock (_lock) GetTrackState(trackIndex).Muted = muted;
        }

        public void NoteOn(int trackIndex, int key, FtmWaveformType type, FtmInstrument? instrument = null)
        {
            lock (_lock)
            {
                var state = GetTrackState(trackIndex);
                double targetFreq = NoteToFrequency(key);
                double startFreq = targetFreq;
                
                if (state.PortamentoEnabled && state.LastFrequency > 0)
                {
                    startFreq = state.LastFrequency;
                }
                state.LastFrequency = targetFreq;

                // Remove existing voice with same key on same track
                _voices.RemoveAll(v => v.Key == key && v.TrackIndex == trackIndex);

                _voices.Add(new Voice
                {
                    TrackIndex = trackIndex,
                    Key = key,
                    Type = type,
                    Phase = 0,
                    BaseFrequency = targetFreq,
                    CurrentFrequency = startFreq,
                    Envelope = 0,
                    IsReleasing = false,
                    Instrument = instrument,
                    MacroTick = 0,
                    VolumeOffset = 0,
                    PitchOffset = 0,
                    ArpeggioOffset = 0,
                    DutyOffset = 0
                });
            }
        }

        public void NoteOff(int trackIndex, int key)
        {
            lock (_lock)
            {
                foreach (var voice in _voices)
                {
                    if (voice.Key == key && voice.TrackIndex == trackIndex)
                    {
                        voice.IsReleasing = true;
                    }
                }
            }
        }
        
        public void ReleaseAll()
        {
            lock (_lock)
            {
                foreach (var voice in _voices)
                {
                    voice.IsReleasing = true;
                }
            }
        }

        public int Read(Span<float> buffer)
        {
            float[] temp = new float[buffer.Length];
            int read = Read(temp, 0, temp.Length);
            temp.AsSpan(0, read).CopyTo(buffer);
            return read;
        }

        private double _lfoPhase = 0;
        private const double LfoFrequency = 6.0; // 6 Hz Vibrato

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);

            lock (_lock)
            {
                double lfoStep = LfoFrequency / _waveFormat.SampleRate;
                double samplesPerTick = _waveFormat.SampleRate / (double)(EngineSpeed > 0 ? EngineSpeed : 60.0);

                for (int j = 0; j < count; j += _waveFormat.Channels)
                {
                    _fractionalTick++;
                    bool tickMacros = false;
                    if (_fractionalTick >= samplesPerTick)
                    {
                        _fractionalTick -= samplesPerTick;
                        tickMacros = true;
                    }

                    for (int i = _voices.Count - 1; i >= 0; i--)
                    {
                        var voice = _voices[i];
                        var state = GetTrackState(voice.TrackIndex);

                        if (tickMacros && voice.Instrument != null && CustomFtmSequences != null)
                        {
                            int GetMacroIndex(int tick, FtmSequence seq)
                            {
                                if (seq.Values.Length == 0) return 0;
                                if (tick < seq.Values.Length) return tick;
                                if (seq.LoopPoint >= 0 && seq.LoopPoint < seq.Values.Length)
                                {
                                    int loopLen = seq.Values.Length - seq.LoopPoint;
                                    return seq.LoopPoint + ((tick - seq.LoopPoint) % loopLen);
                                }
                                return seq.Values.Length - 1;
                            }

                            if (voice.Instrument.VolumeSeq != -1 && CustomFtmSequences.TryGetValue(voice.Instrument.VolumeSeq, out var volSeq) && volSeq.Values.Length > 0)
                            {
                                voice.VolumeOffset = volSeq.Values[GetMacroIndex(voice.MacroTick, volSeq)];
                            }
                            
                            if (voice.Instrument.ArpeggioSeq != -1 && CustomFtmSequences.TryGetValue(voice.Instrument.ArpeggioSeq, out var arpSeq) && arpSeq.Values.Length > 0)
                            {
                                voice.ArpeggioOffset = (sbyte)arpSeq.Values[GetMacroIndex(voice.MacroTick, arpSeq)];
                            }
                            
                            if (voice.Instrument.PitchSeq != -1 && CustomFtmSequences.TryGetValue(voice.Instrument.PitchSeq, out var pitchSeq) && pitchSeq.Values.Length > 0)
                            {
                                voice.PitchOffset = (sbyte)pitchSeq.Values[GetMacroIndex(voice.MacroTick, pitchSeq)];
                            }
                            
                            if (voice.Instrument.DutySeq != -1 && CustomFtmSequences.TryGetValue(voice.Instrument.DutySeq, out var dutySeq) && dutySeq.Values.Length > 0)
                            {
                                voice.DutyOffset = dutySeq.Values[GetMacroIndex(voice.MacroTick, dutySeq)];
                            }
                            
                            voice.MacroTick++;
                        }

                        // Pitch bend
                        double pitchBendNorm = (state.PitchBend - 8192.0) / 8192.0;
                        double pitchBendSemitones = pitchBendNorm * 2.0; // +/- 2 semitones

                        // Base Volume (0-127) + FTM Volume Macro (0-15)
                        double volNorm = state.Muted ? 0.0 : (state.Volume / 127.0);
                        if (voice.Instrument != null && voice.Instrument.VolumeSeq != -1)
                        {
                            volNorm *= (voice.VolumeOffset / 15.0);
                        }
                        double gain = volNorm * volNorm * 0.1; // base gain 0.1 (reduced from 0.2 to prevent loudness)

                        double currentLfoPhase = _lfoPhase;
                        
                        if (voice.IsReleasing)
                        {
                            voice.Envelope -= ReleaseRate;
                            if (voice.Envelope <= 0)
                            {
                                voice.Envelope = 0;
                            }
                        }
                        else
                        {
                            voice.Envelope += AttackRate;
                            if (voice.Envelope > 1.0) voice.Envelope = 1.0;
                        }

                        if (voice.CurrentFrequency != voice.BaseFrequency)
                        {
                            double rate = (state.PortamentoTime == 0 ? 1.0 : state.PortamentoTime) / 127.0;
                            double speedFactor = 1000.0 / rate; 
                            
                            double freqDiff = voice.BaseFrequency - voice.CurrentFrequency;
                            double move = freqDiff * (1.0 / speedFactor);
                            
                            if (Math.Abs(freqDiff) < 1.0) voice.CurrentFrequency = voice.BaseFrequency;
                            else voice.CurrentFrequency += move;
                        }

                        // Vibrato + Pitch Bend + FTM Arpeggio
                        double vibratoDepth = state.Modulation / 127.0; // max 1 semitone
                        double vibratoOffset = Math.Sin(currentLfoPhase * Math.PI * 2) * vibratoDepth;
                        
                        double totalSemitoneOffset = pitchBendSemitones + vibratoOffset + voice.ArpeggioOffset;
                        // FTM Pitch macro usually operates in fine pitch units. 1 unit ~ 1/64th semitone.
                        totalSemitoneOffset += (voice.PitchOffset / 64.0);
                        
                        double actualFrequency = voice.CurrentFrequency * Math.Pow(2.0, totalSemitoneOffset / 12.0);
                        double phaseStep = actualFrequency / _waveFormat.SampleRate;

                        double sample = 0;
                        FtmWaveformType currentType = voice.Type;
                        
                        // Apply FTM Duty macro
                        if (voice.Instrument != null && voice.Instrument.DutySeq != -1 && (currentType == FtmWaveformType.Square12 || currentType == FtmWaveformType.Square25 || currentType == FtmWaveformType.Square50 || currentType == FtmWaveformType.Square75))
                        {
                            if (voice.DutyOffset == 0) currentType = FtmWaveformType.Square12;
                            else if (voice.DutyOffset == 1) currentType = FtmWaveformType.Square25;
                            else if (voice.DutyOffset == 2) currentType = FtmWaveformType.Square50;
                            else if (voice.DutyOffset == 3) currentType = FtmWaveformType.Square75;
                        }
                        switch (currentType)
                        {
                            case FtmWaveformType.Square12:
                                sample = voice.Phase < 0.125 ? 1.0 : -1.0;
                                break;
                            case FtmWaveformType.Square25:
                                sample = voice.Phase < 0.25 ? 1.0 : -1.0;
                                break;
                            case FtmWaveformType.Square50:
                                sample = voice.Phase < 0.5 ? 1.0 : -1.0;
                                break;
                            case FtmWaveformType.Square75:
                                sample = voice.Phase < 0.75 ? 1.0 : -1.0;
                                break;
                            case FtmWaveformType.Triangle:
                                sample = voice.Phase < 0.5 ? (voice.Phase * 4.0 - 1.0) : (3.0 - voice.Phase * 4.0);
                                break;
                            case FtmWaveformType.Sawtooth:
                                sample = voice.Phase * 2.0 - 1.0;
                                break;
                            case FtmWaveformType.Noise:
                                sample = (_random.NextDouble() * 2.0) - 1.0;
                                break;
                        }
                        
                        sample *= gain;

                        for (int c = 0; c < _waveFormat.Channels; c++)
                        {
                            buffer[offset + j + c] += (float)(sample * voice.Envelope);
                        }
                        
                        voice.Phase += phaseStep;
                        if (voice.Phase >= 1.0) voice.Phase %= 1.0;

                        if (voice.Envelope <= 0 && voice.IsReleasing)
                        {
                            _voices.RemoveAt(i);
                        }
                    }

                    _lfoPhase = (_lfoPhase + lfoStep) % 1.0;
                }
            }
            return count;
        }

        private double NoteToFrequency(int noteNumber)
        {
            return 440.0 * Math.Pow(2.0, (noteNumber - 69) / 12.0);
        }
    }
}
