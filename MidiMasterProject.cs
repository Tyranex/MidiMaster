using System;
using System.Collections.Generic;

namespace SS14_MIDI_IDE
{
    public class NoteInstrumentMapping
    {
        public int Channel { get; set; }
        public long Time { get; set; }
        public int InstrumentId { get; set; }
    }

    public class MidiMasterProjectData
    {
        public int EngineSpeed { get; set; }
        public Dictionary<int, FtmInstrument> CustomFtmInstruments { get; set; }
        public Dictionary<int, FtmSequence> CustomFtmSequences { get; set; }
        public List<NoteInstrumentMapping> FtmNoteInstruments { get; set; }
        public string ReferenceAudioFilename { get; set; }

        public MidiMasterProjectData()
        {
            CustomFtmInstruments = new Dictionary<int, FtmInstrument>();
            CustomFtmSequences = new Dictionary<int, FtmSequence>();
            FtmNoteInstruments = new List<NoteInstrumentMapping>();
        }
    }
}
