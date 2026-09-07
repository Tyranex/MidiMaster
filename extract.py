import sys

def extract_methods(filepath, method_names):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    extracted = []
    remaining = []
    i = 0
    while i < len(lines):
        line = lines[i]
        matched_method = None
        for m in method_names:
            if f" {m}(" in line or f" {m} (" in line:
                matched_method = m
                break
        
        if matched_method and ("private void" in line or "private async void" in line):
            # Start extracting this method
            method_lines = []
            brace_count = 0
            started = False
            
            while i < len(lines):
                cur_line = lines[i]
                method_lines.append(cur_line)
                
                if '{' in cur_line:
                    brace_count += cur_line.count('{')
                    started = True
                if '}' in cur_line:
                    brace_count -= cur_line.count('}')
                    
                i += 1
                
                if started and brace_count == 0:
                    break
            
            extracted.append("".join(method_lines))
        else:
            remaining.append(line)
            i += 1
            
    return "".join(remaining), "\n\n".join(extracted)

def main():
    filepath = "UI/Windows/Main/MainWindow.xaml.cs"
    
    file_handling_methods = [
        "FileNewProject_Click", "FileSaveProject_Click", "FileOpenProject_Click",
        "ImportMidi_Click", "ExportMidi_Click", "LoadMidiFile",
        "FileImportFtm_Click", "FileExportFtm_Click"
    ]
    
    playback_methods = [
        "PlayButton_Click", "StopButton_Click", "StopPlayback",
        "PlaybackTimer_Tick", "AudioEngine_PlaybackStopped",
        "LoadReference_Click", "ReferenceMixSlider_ValueChanged", "ReferenceOffsetTextBox_TextChanged"
    ]
    
    undo_redo_methods = [
        "PushUndoState", "PerformUndo", "PerformRedo",
        "UpdateUndoUI", "RestoreMidiState",
        "EditUndo_Click", "EditRedo_Click", "EditUndoHistory_Click"
    ]

    # File Handling
    rem, ext = extract_methods(filepath, file_handling_methods)
    with open(filepath, 'w', encoding='utf-8') as f: f.write(rem)
    with open("UI/Windows/Main/MainWindow.FileHandling.cs", 'w', encoding='utf-8') as f:
        f.write("using System;\nusing System.Windows;\nusing System.IO;\nusing Melanchall.DryWetMidi.Core;\nusing System.Linq;\nusing System.Text.Json;\nusing System.IO.Compression;\nusing SS14_MIDI_IDE.Core;\nusing System.Collections.Generic;\n\nnamespace SS14_MIDI_IDE\n{\n    public partial class MainWindow : Window\n    {\n" + ext + "\n    }\n}\n")

    # Playback
    rem, ext = extract_methods(filepath, playback_methods)
    with open(filepath, 'w', encoding='utf-8') as f: f.write(rem)
    with open("UI/Windows/Main/MainWindow.Playback.cs", 'w', encoding='utf-8') as f:
        f.write("using System;\nusing System.Windows;\nusing System.Windows.Controls;\nusing Microsoft.Win32;\n\nnamespace SS14_MIDI_IDE\n{\n    public partial class MainWindow : Window\n    {\n" + ext + "\n    }\n}\n")

    # Undo/Redo
    rem, ext = extract_methods(filepath, undo_redo_methods)
    with open(filepath, 'w', encoding='utf-8') as f: f.write(rem)
    with open("UI/Windows/Main/MainWindow.UndoRedo.cs", 'w', encoding='utf-8') as f:
        f.write("using System;\nusing System.Windows;\n\nnamespace SS14_MIDI_IDE\n{\n    public partial class MainWindow : Window\n    {\n" + ext + "\n    }\n}\n")

if __name__ == "__main__":
    main()
