using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MeltySynth;

namespace SS14_MIDI_IDE
{
    public class FtmParser
    {
        public static (string, Dictionary<int, FtmInstrument>, Dictionary<int, FtmSequence>, int, Dictionary<(int, long), int>) Parse(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);

            int globalSpeed = 6;
            int globalTempo = 150;

            string magic = new string(reader.ReadChars(16)).TrimEnd('\0');
            if (magic != "FamiTracker Modu")
                throw new Exception("Not a valid FamiTracker module.");
            
            // "le" + 4 bytes version
            reader.ReadBytes(2);
            int version = reader.ReadInt32();

            int channelsAvailable = 5;
            int engineSpeed = 150;
            int trackCount = 1;
            int patternLength = 64;
            List<int> effectColumns = new List<int> { 0, 0, 0, 0, 0 }; // Default 5 channels, 0 additional FX
            List<PatternEvent> patternEvents = new List<PatternEvent>();
            List<List<int>> framePatterns = new List<List<int>>();
            var parsedSequences = new Dictionary<int, FtmSequence>();
            var parsedInstruments = new Dictionary<int, FtmInstrument>();

            while (ms.Position < ms.Length)
            {
                long blockStart = ms.Position;
                byte[] idBytes = reader.ReadBytes(16);
                if (idBytes.Length < 16) break;
                string id = Encoding.ASCII.GetString(idBytes).TrimEnd('\0');
                if (idBytes[0] == 0) break;

                int blockVersion = reader.ReadInt32();
                int blockSize = reader.ReadInt32();
                long dataStart = ms.Position;
                System.IO.File.AppendAllText("parser_debug.txt", $"Block: {id} V:{blockVersion} Size:{blockSize} Pos:{dataStart}\n");

                if (id == "PARAMS")
                {
                    if (blockVersion != 1) reader.ReadByte(); // expansion chip
                    channelsAvailable = reader.ReadInt32();
                    reader.ReadInt32(); // machine
                    if (blockVersion >= 7) {
                        reader.ReadInt32(); // rate type
                        reader.ReadInt32(); // rate
                    } else {
                        engineSpeed = reader.ReadInt32();
                    }
                    // ignore the rest
                }
                else if (id == "HEADER")
                {
                    if (blockVersion >= 2)
                    {
                        trackCount = reader.ReadByte() + 1;
                        if (blockVersion >= 3)
                        {
                            for (int i = 0; i < trackCount; i++) ReadFtmString(reader); // track names
                        }
                        
                        effectColumns.Clear();
                        for (int i = 0; i < channelsAvailable; i++)
                        {
                            reader.ReadByte(); // type
                            int fxCount = 0;
                            for (int j = 0; j < trackCount; j++)
                            {
                                int c = reader.ReadByte();
                                if (j == 0) fxCount = c;
                            }
                            effectColumns.Add(fxCount);
                        }
                    }
                }
                else if (id == "PATTERNS")
                {
                    int itemsRead = 0;
                    while (ms.Position < dataStart + blockSize)
                    {
                        int track = 0;
                        if (blockVersion > 1) track = reader.ReadInt32();
                        
                        int channel = reader.ReadInt32();
                        int pattern = reader.ReadInt32();
                        int items = reader.ReadInt32();
                        
                        for (int i = 0; i < items; i++)
                        {
                            int row = 0;
                            if (version == 0x0200 || blockVersion >= 6) row = reader.ReadByte();
                            else row = reader.ReadInt32();

                            byte note = reader.ReadByte();
                            byte octave = reader.ReadByte();
                            byte inst = reader.ReadByte();
                            byte vol = reader.ReadByte();

                            int fxCols = 1;
                            if (version == 0x0200) fxCols = 1;
                            else if (blockVersion >= 6) fxCols = 4; // MAX_EFFECT_COLUMNS = 4 in FamiTracker
                            else fxCols = (channel < effectColumns.Count ? effectColumns[channel] : 0) + 1;

                            var effects = new List<(int, int)>();
                            for (int f = 0; f < fxCols; f++)
                            {
                                byte efNum = reader.ReadByte();
                                byte efParam = 0;
                                if (efNum != 0)
                                {
                                    efParam = reader.ReadByte(); // efParam
                                    effects.Add((efNum, efParam));
                                }
                                else if (blockVersion < 6)
                                {
                                    reader.ReadByte(); // unused blank param
                                }
                            }

                            // Only care about track 0 for now
                            if (track == 0)
                            {
                                patternEvents.Add(new PatternEvent
                                {
                                    Channel = channel,
                                    Pattern = pattern,
                                    Row = row,
                                    Note = note,
                                    Octave = octave,
                                    Volume = vol,
                                    Instrument = inst,
                                    Effects = effects
                                });
                            }
                        }
                    }
                }

                else if (id == "FRAMES")
                {
                    if (blockVersion == 1) {
                        // ignore v1 frames for now, not seen in my dumps
                    }
                    else if (blockVersion > 1) {
                        for (int y = 0; y < trackCount; ++y) {
                            int frameCount = reader.ReadInt32();
                            int speed = reader.ReadInt32();
                            int tempo = 150;
                            if (blockVersion >= 3) {
                                tempo = reader.ReadInt32();
                            }

                            if (y == 0) {
                                globalSpeed = speed;
                                globalTempo = tempo;
                            }

                            patternLength = reader.ReadInt32();

                            // Read patterns for frames
                            for (int i = 0; i < frameCount; ++i) {
                                List<int> chPats = new List<int>();
                                for (int j = 0; j < channelsAvailable; ++j) {
                                    int patternIndex = reader.ReadByte();
                                    chPats.Add(patternIndex);
                                }
                                if (y == 0) framePatterns.Add(chPats);
                            }
                        }
                    }
                }
                else if (id == "SEQUENCES" || id == "SEQUENCES_VRC6")
                {
                    int chipType = id == "SEQUENCES" ? 1 : 2; // 1 = 2A03, 2 = VRC6
                    
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        int index = reader.ReadInt32();
                        int type = reader.ReadInt32();
                        byte seqCount = reader.ReadByte();
                        
                        var seq = new FtmSequence { Type = type };
                        
                        if (blockVersion >= 3)
                        {
                            seq.LoopPoint = reader.ReadInt32();
                            if (blockVersion >= 4) {
                                seq.ReleasePoint = reader.ReadInt32();
                                seq.Settings = reader.ReadInt32();
                            }
                        }
                        seq.Values = reader.ReadBytes(seqCount);
                        int globalSeqId = chipType * 10000 + type * 1000 + index;
                        parsedSequences[globalSeqId] = seq;
                    }
                }

                else if (id == "INSTRUMENTS")
                {
                    long posBackup = ms.Position;
                    byte[] raw = reader.ReadBytes(blockSize);
                    System.IO.File.WriteAllText("hex.txt", BitConverter.ToString(raw));
                    ms.Position = posBackup;
                    
                    int count = reader.ReadInt32();
                    System.IO.File.AppendAllText("parser_debug.txt", $"Inst count: {count}\n");
                    for (int i = 0; i < count; i++)
                    {
                        int index = reader.ReadInt32();
                        byte type = reader.ReadByte();
                        System.IO.File.AppendAllText("parser_debug.txt", $"Inst {index}, Type: {type}\n");
                        
                        FtmInstrument instData = null;
                        
                        if (type == 1) // 2A03
                        {
                            instData = new FtmInstrument2A03 { Id = index };
                            
                            int seqCnt = reader.ReadInt32();
                            for (int s = 0; s < seqCnt; s++)
                            {
                                byte enable = reader.ReadByte();
                                byte seqIndex = reader.ReadByte();
                                if (enable != 0)
                                {
                                    int globalSeqId = 1 * 10000 + s * 1000 + seqIndex;
                                    if (s == 0) instData.VolumeSeq = globalSeqId;
                                    else if (s == 1) instData.ArpeggioSeq = globalSeqId;
                                    else if (s == 2) instData.PitchSeq = globalSeqId;
                                    else if (s == 3) instData.HiPitchSeq = globalSeqId;
                                    else if (s == 4) instData.DutySeq = globalSeqId;
                                }
                            }
                            
                            if (blockVersion >= 7) {
                                int assignCount = reader.ReadInt32();
                                for (int a = 0; a < assignCount; a++) {
                                    reader.ReadByte(); // note
                                    reader.ReadByte(); // dpcm index
                                    reader.ReadByte(); // pitch
                                    reader.ReadByte(); // delta
                                }
                            } else {
                                int octaves = (blockVersion == 1) ? 6 : 8; // OCTAVE_RANGE
                                for (int o=0; o<octaves; o++) {
                                    for (int n=0; n<12; n++) {
                                        reader.ReadByte(); // dpcm index
                                        reader.ReadByte(); // pitch
                                        if (blockVersion > 5) {
                                            reader.ReadByte(); // delta
                                        }
                                    }
                                }
                            }
                        }
                        else if (type == 2) // VRC6
                        {
                            instData = new FtmInstrumentVRC6 { Id = index };
                            
                            int seqCnt = reader.ReadInt32();
                            for (int s = 0; s < seqCnt; s++)
                            {
                                byte enable = reader.ReadByte();
                                byte seqIndex = reader.ReadByte();
                                if (enable != 0)
                                {
                                    int globalSeqId = 2 * 10000 + s * 1000 + seqIndex;
                                    if (s == 0) instData.VolumeSeq = globalSeqId;
                                    else if (s == 1) instData.ArpeggioSeq = globalSeqId;
                                    else if (s == 2) instData.PitchSeq = globalSeqId;
                                    else if (s == 3) instData.HiPitchSeq = globalSeqId;
                                    else if (s == 4) instData.DutySeq = globalSeqId;
                                }
                            }
                        }
                        else
                        {
                            System.IO.File.AppendAllText("parser_debug.txt", $"Stopping instrument parsing due to unsupported type {type}\n");
                            break;
                        }
                        
                        int nameLen = reader.ReadInt32();
                        instData.Name = new string(reader.ReadChars(nameLen));
                        parsedInstruments[index] = instData;
                    }
                }
                
                ms.Position = dataStart + blockSize;
            }

            string tempPath = Path.GetTempFileName();
            var instrumentMap = GenerateMidi(tempPath, patternEvents, framePatterns, channelsAvailable, patternLength, globalTempo, globalSpeed, parsedSequences, parsedInstruments);

            return (tempPath, parsedInstruments, parsedSequences, globalSpeed, instrumentMap);
        }

        private static Dictionary<(int, long), int> GenerateMidi(string tempPath, List<PatternEvent> events, List<List<int>> frames, int channelsAvailable, int patternLength, int initialTempo, int initialSpeed, Dictionary<int, FtmSequence> parsedSequences, Dictionary<int, FtmInstrument> parsedInstruments)
        {
            var ftmInstrumentMap = new Dictionary<(int, long), int>();
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // MThd
            bw.Write(new byte[] { 0x4D, 0x54, 0x68, 0x64 }); // MThd
            bw.WriteBE(6); // length
            bw.WriteBE((short)1); // format 1
            bw.WriteBE((short)(channelsAvailable + 1)); // track count (1 tempo + channels)
            bw.WriteBE((short)24); // ticks per quarter note

            // Group events by pattern
            var patEvents = new Dictionary<(int Channel, int Pattern), List<PatternEvent>>();
            foreach (var e in events) {
                var key = (e.Channel, e.Pattern);
                if (!patEvents.ContainsKey(key)) patEvents[key] = new List<PatternEvent>();
                patEvents[key].Add(e);
            }

            // Tempo Track
            bw.Write(new byte[] { 0x4D, 0x54, 0x72, 0x6B }); // MTrk
            long tempoLenPos = fs.Position;
            bw.WriteBE(0);
            
            long tempoTick = 0;
            long lastTempoTick = 0;
            int currentTempo = initialTempo;
            int currentSpeed = initialSpeed == 0 ? 6 : initialSpeed;

            int[] currentFtmInstruments = new int[channelsAvailable];
            for (int i = 0; i < currentFtmInstruments.Length; i++) currentFtmInstruments[i] = -1;

            void WriteTempoEvent(int tempo, int speed, long tick)
            {
                if (speed == 0) speed = 6;
                double bpm = (tempo * 6.0) / speed;
                if (bpm <= 0) bpm = 150;
                int microseconds = (int)(60000000 / bpm);
                
                WriteVlq(bw, (int)(tick - lastTempoTick));
                bw.Write((byte)0xFF); bw.Write((byte)0x51); bw.Write((byte)0x03);
                bw.Write((byte)((microseconds >> 16) & 0xFF));
                bw.Write((byte)((microseconds >> 8) & 0xFF));
                bw.Write((byte)(microseconds & 0xFF));
                lastTempoTick = tick;
            }

            // Initial tempo
            WriteTempoEvent(currentTempo, currentSpeed, 0);

            // Scan for Fxx tempo changes
            for (int f = 0; f < frames.Count; f++)
            {
                for (int r = 0; r < patternLength; r++)
                {
                    bool changed = false;
                    for (int ch = 0; ch < channelsAvailable; ch++)
                    {
                        int patIdx = frames[f][ch];
                        var evs = patEvents.ContainsKey((ch, patIdx)) ? patEvents[(ch, patIdx)] : null;
                        var e = evs?.Find(x => x.Row == r);
                        if (e != null)
                        {
                            foreach (var fx in e.Effects)
                            {
                                if (fx.Type == 1) // EF_SPEED (Fxx effect)
                                {
                                    if (fx.Param < 0x20) currentSpeed = fx.Param;
                                    else currentTempo = fx.Param;
                                    changed = true;
                                }
                            }
                        }
                    }
                    if (changed) WriteTempoEvent(currentTempo, currentSpeed, tempoTick);
                    tempoTick += 6;
                }
            }

            WriteVlq(bw, (int)(tempoTick - lastTempoTick));
            bw.Write((byte)0xFF); bw.Write((byte)0x2F); bw.Write((byte)0x00); // end of track

            bw.Flush();
            long curPos = fs.Position;
            fs.Position = tempoLenPos;
            bw.WriteBE((int)(curPos - tempoLenPos - 4));
            bw.Flush();
            fs.Position = curPos;

            for (int ch = 0; ch < channelsAvailable; ch++)
            {
                bw.Write(new byte[] { 0x4D, 0x54, 0x72, 0x6B }); // MTrk
                long trkLenPos = fs.Position;
                bw.WriteBE(0);

                int lastTick = 0;
                int currentTick = 0;
                int? lastNote = null;
                
                int currentPitchBend = 8192;
                int baseIdx = AudioEngine.CustomInstrumentBaseIndex;
                int initialProgram = baseIdx;
                if (ch == 2) initialProgram = baseIdx + 4; 
                else if (ch == 3) initialProgram = baseIdx + 6;
                else if (ch == 4) initialProgram = baseIdx + 6;
                else initialProgram = baseIdx + 2;
                int currentProgram = initialProgram;

                int midiCh = ch % 16;

                void WriteProgramChange(int tick, int program)
                {


                    int bank = program / 128;
                    int prog = program % 128;

                    // Write CC 0 (Bank Select)
                    WriteVlq(bw, tick - lastTick);
                    bw.Write((byte)(0xB0 | midiCh));
                    bw.Write((byte)0x00); // CC 0
                    bw.Write((byte)bank);
                    lastTick = tick;

                    // Write Program Change
                    WriteVlq(bw, 0); // 0 delta because we just advanced time
                    bw.Write((byte)(0xC0 | midiCh));
                    bw.Write((byte)prog);
                }

                WriteProgramChange(0, currentProgram);

                for (int f = 0; f < frames.Count; f++)
                {
                    int patIdx = frames[f][ch];
                    var evs = patEvents.ContainsKey((ch, patIdx)) ? patEvents[(ch, patIdx)] : null;

                    for (int r = 0; r < patternLength; r++)
                    {
                        var e = evs?.Find(x => x.Row == r);
                        if (e != null)
                        {
                            bool newNote = e.Note >= 1 && e.Note <= 12;

                            if (newNote)
                            {
                                // Note Off
                                if (lastNote != null)
                                {
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0x80 | midiCh));
                                    bw.Write((byte)lastNote.Value);
                                    bw.Write((byte)0);
                                    lastTick = currentTick;
                                }

                                // Reset pitch bend
                                if (currentPitchBend != 8192)
                                {
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0xE0 | midiCh));
                                    bw.Write((byte)(8192 & 0x7F));
                                    bw.Write((byte)((8192 >> 7) & 0x7F));
                                    lastTick = currentTick;
                                    currentPitchBend = 8192;
                                }

                                // Reset portamento if 3xx is not present
                                bool hasPortamento = e.Effects.Any(x => x.Type == 6);
                                if (!hasPortamento)
                                {
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0xB0 | midiCh));
                                    bw.Write((byte)65); // Portamento On
                                    bw.Write((byte)0); // OFF
                                    lastTick = currentTick;
                                }
                            }

                            // Handle FX
                            foreach (var fx in e.Effects)
                            {
                                if (fx.Type == 11) // 4xy Vibrato -> CC 1 (EF_VIBRATO)
                                {
                                    int depth = fx.Param & 0x0F;
                                    if (depth == 0 && fx.Param != 0) depth = fx.Param >> 4; // fallback
                                    
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0xB0 | midiCh));
                                    bw.Write((byte)1); // Modulation
                                    bw.Write((byte)(depth * 8)); // scale 0-15 to 0-120
                                    lastTick = currentTick;
                                }
                                else if (fx.Type == 6) // 3xx Portamento to note -> CC 65 (EF_PORTAMENTO)
                                {
                                    int speed = fx.Param;
                                    
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0xB0 | midiCh));
                                    bw.Write((byte)65); // Portamento On
                                    bw.Write((byte)127);
                                    lastTick = currentTick;
                                    
                                    WriteVlq(bw, 0); // simultaneous
                                    bw.Write((byte)(0xB0 | midiCh));
                                    bw.Write((byte)5); // Portamento Time
                                    bw.Write((byte)(speed & 0x7F));
                                    lastTick = currentTick;
                                }
                                else if (fx.Type == 16 || fx.Type == 17) // 1xx / 2xx Pitch Bend (EF_PORTA_UP / EF_PORTA_DOWN)
                                {
                                    int speed = fx.Param;
                                    if (fx.Type == 17) speed = -speed;
                                    
                                    currentPitchBend += (speed * 10);
                                    currentPitchBend = Math.Clamp(currentPitchBend, 0, 16383);
                                    
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0xE0 | midiCh));
                                    bw.Write((byte)(currentPitchBend & 0x7F)); // lsb
                                    bw.Write((byte)((currentPitchBend >> 7) & 0x7F)); // msb
                                    lastTick = currentTick;
                                }
                            }

                            if (newNote)
                            {
                                if (e.Instrument != 0 && e.Instrument != 255)
                                {
                                    int targetProgram = initialProgram;
                                    if (parsedInstruments.ContainsKey(e.Instrument - 1))
                                    {
                                        currentFtmInstruments[ch] = e.Instrument - 1;
                                        int orderIndex = parsedInstruments.Keys.OrderBy(k => k).ToList().IndexOf(e.Instrument - 1);
                                        targetProgram = AudioEngine.CustomInstrumentBaseIndex + 7 + orderIndex;
                                    }
                                    if (targetProgram != currentProgram)
                                    {
                                        WriteProgramChange(currentTick, targetProgram);
                                        currentProgram = targetProgram;
                                    }
                                }

                                int midiNote = 12 + (e.Octave * 12) + (e.Note - 1);
                                midiNote = Math.Clamp(midiNote, 0, 127);
                                
                                if (currentFtmInstruments[ch] != -1)
                                {
                                    ftmInstrumentMap[(ch + 1, currentTick)] = currentFtmInstruments[ch];
                                }
                                
                                WriteVlq(bw, currentTick - lastTick);
                                bw.Write((byte)(0x90 | midiCh));
                                bw.Write((byte)midiNote);
                                bw.Write((byte)100);
                                lastTick = currentTick;
                                lastNote = midiNote;
                            }
                            else if (e.Note == 13 || e.Note == 14) // Note Cut / Release
                            {
                                if (lastNote != null)
                                {
                                    WriteVlq(bw, currentTick - lastTick);
                                    bw.Write((byte)(0x80 | (ch % 16)));
                                    bw.Write((byte)lastNote.Value);
                                    bw.Write((byte)0);
                                    lastTick = currentTick;
                                    lastNote = null;
                                }
                            }
                        }
                        currentTick += 6; // 6 ticks per row (assuming 24 ticks/beat, 4 rows/beat)
                    }
                }
                
                if (lastNote != null)
                {
                    WriteVlq(bw, currentTick - lastTick);
                    bw.Write((byte)(0x80 | (ch % 16)));
                    bw.Write((byte)lastNote.Value);
                    bw.Write((byte)0);
                    lastTick = currentTick;
                }

                bw.Write((byte)0x00);
                bw.Write((byte)0xFF); bw.Write((byte)0x2F); bw.Write((byte)0x00);

                curPos = fs.Position;
                fs.Position = trkLenPos;
                bw.WriteBE((int)(curPos - trkLenPos - 4));
                fs.Position = curPos;
            }

            return ftmInstrumentMap;
        }

        private static void WriteVlq(BinaryWriter bw, int value)
        {
            int buffer = value & 0x7F;
            while ((value >>= 7) > 0)
            {
                buffer <<= 8;
                buffer |= 0x80;
                buffer += (value & 0x7F);
            }
            while (true)
            {
                bw.Write((byte)(buffer & 0xFF));
                if ((buffer & 0x80) != 0) buffer >>= 8;
                else break;
            }
        }

        private static string ReadFtmString(BinaryReader reader)
        {
            List<byte> bytes = new List<byte>();
            while (true)
            {
                byte b = reader.ReadByte();
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        class PatternEvent
        {
            public int Channel;
            public int Pattern;
            public int Row;
            public byte Note;
            public byte Octave;
            public int Volume;
            public int Instrument;
            public List<(int Type, int Param)> Effects = new List<(int, int)>();
        }
    }

    public static class BinaryWriterExtensions
    {
        public static void WriteBE(this BinaryWriter bw, int value)
        {
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value)));
        }

        public static void WriteBE(this BinaryWriter bw, short value)
        {
            bw.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value)));
        }
    }
}
