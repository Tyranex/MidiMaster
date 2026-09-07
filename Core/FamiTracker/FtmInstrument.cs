using System;
using System.Collections.Generic;

namespace SS14_MIDI_IDE
{
    public class FtmSequence
    {
        public int Type;
        public byte[] Values;
        public int LoopPoint = -1;
        public int ReleasePoint = -1;
        public int Settings;
    }

    [System.Text.Json.Serialization.JsonDerivedType(typeof(FtmInstrument2A03), typeDiscriminator: "2A03")]
    [System.Text.Json.Serialization.JsonDerivedType(typeof(FtmInstrumentVRC6), typeDiscriminator: "VRC6")]
    public abstract class FtmInstrument
    {
        public int Id;
        public string Name;
        
        public abstract byte FtmType { get; }
        
        // Sequence indices (-1 if none)
        public int VolumeSeq = -1;
        public int ArpeggioSeq = -1;
        public int PitchSeq = -1;
        public int HiPitchSeq = -1;
        public int DutySeq = -1;
    }

    public class FtmInstrument2A03 : FtmInstrument
    {
        public override byte FtmType => 1;
        // DPCM mappings could go here
    }

    public class FtmInstrumentVRC6 : FtmInstrument
    {
        public override byte FtmType => 2;
        // VRC6 specific settings
    }
}
