using System;
using System.IO;
using System.Text;

class Program
{
    static void Dummy(string[] args)
    {
        string path = @"F:\AAnew data\SS14-MIDI-IDE\TestFTMs\Pokemon Tower.ftm";
        if (args.Length > 0) path = args[0];

        using var ms = new MemoryStream(File.ReadAllBytes(path));
        using var reader = new BinaryReader(ms);

        string magic = new string(reader.ReadChars(16)).TrimEnd('\0');
        reader.ReadBytes(2);
        int version = reader.ReadInt32();
        Console.WriteLine($"Magic: {magic}, Version: {version:X4}");

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

            Console.WriteLine($"Block: {id}, Version: {blockVersion}, Size: {blockSize}");

            if (id == "SEQUENCES")
            {
                int count = reader.ReadInt32();
                Console.WriteLine($"  Sequences count: {count}");
                try {
                    for (int i = 0; i < count; i++)
                    {
                        int index = reader.ReadInt32();
                        int type = reader.ReadInt32();
                        byte seqCount = reader.ReadByte();
                        int loopPoint = reader.ReadInt32();
                        
                        if (blockVersion >= 4) {
                            int releasePoint = reader.ReadInt32();
                            int settings = reader.ReadInt32();
                        }

                        byte[] values = reader.ReadBytes(seqCount);
                        if (type == 4) { // Duty
                            Console.WriteLine($"    Duty Seq {index}: [{string.Join(", ", values)}]");
                        }
                    }
                } catch { }
            }
            else if (id == "INSTRUMENTS")
            {
                int count = reader.ReadInt32();
                Console.WriteLine($"  Instruments count: {count}");
                try {
                    for (int i = 0; i < count; i++)
                    {
                        int index = reader.ReadInt32();
                        byte type = reader.ReadByte();
                        if (type == 1) // 2A03
                        {
                            int seqCnt = reader.ReadInt32();
                            for (int s = 0; s < seqCnt; s++)
                            {
                                byte enable = reader.ReadByte();
                                byte seqIndex = reader.ReadByte();
                                if (s == 4 && enable == 1) {
                                    Console.WriteLine($"    Inst {index} uses Duty Seq {seqIndex}");
                                }
                            }
                            
                            // 2A03 specific
                            if (blockVersion >= 7) {
                                int assignCount = reader.ReadInt32();
                                for (int a=0; a<assignCount; a++) {
                                    reader.ReadByte(); // note
                                    reader.ReadByte(); // dpcm index
                                    reader.ReadByte(); // pitch
                                    reader.ReadByte(); // delta
                                }
                            } else {
                                // 6 octaves * 12 notes = 72
                                int octaves = (blockVersion == 1) ? 6 : 8;
                                for (int o=0; o<octaves; o++) {
                                    for (int n=0; n<12; n++) {
                                        reader.ReadByte(); // index
                                        reader.ReadByte(); // pitch
                                        if (version > 5) {
                                            reader.ReadByte(); // delta
                                        }
                                    }
                                }
                            }
                        }
                        
                        // name length and string
                        int nameLen = reader.ReadInt32();
                        string name = new string(reader.ReadChars(nameLen));
                        Console.WriteLine($"    Inst {index}: {name} (Type {type})");
                    }
                } catch (Exception ex) { Console.WriteLine("    Error parsing insts: " + ex.Message); }
            }

            ms.Position = dataStart + blockSize;
        }
    }
}
