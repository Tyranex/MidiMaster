using System;
using System.Windows;
using System.IO;
using Melanchall.DryWetMidi.Core;
using System.Linq;
using System.Text.Json;
using System.IO.Compression;
using System.Collections.Generic;
using Microsoft.Win32;
using Melanchall.DryWetMidi.Interaction;

namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void FileNewProject_Click(object sender, RoutedEventArgs e)
	{
		StopButton_Click(null, null);
		_loadedMidi = new MidiFile();
		_audioEngine.CustomFtmInstruments = new Dictionary<int, FtmInstrument>();
		_audioEngine.CustomFtmSequences = new Dictionary<int, FtmSequence>();
		_audioEngine.FtmNoteInstruments = new Dictionary<(int, long), int>();
		_audioEngine.EngineSpeed = 150;
		InstrumentNames = _audioEngine.GetInstrumentNames();
		
		UndoManager.Clear();
		UpdateUndoUI();
		
		BuildNoteCache();
		DrawPianoRoll();
		GenerateTrackerView();
	}


	private void FileSaveProject_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.SaveFileDialog();
		dialog.Filter = "MidiMaster Project (*.mmp)|*.mmp";
		if (dialog.ShowDialog() == true)
		{
			try
			{
				var projData = new MidiMasterProjectData
				{
					EngineSpeed = _audioEngine.EngineSpeed,
					CustomFtmInstruments = _audioEngine.CustomFtmInstruments ?? new Dictionary<int, FtmInstrument>(),
					CustomFtmSequences = _audioEngine.CustomFtmSequences ?? new Dictionary<int, FtmSequence>()
				};
				
				if (_audioEngine.FtmNoteInstruments != null)
				{
					foreach (var kvp in _audioEngine.FtmNoteInstruments)
					{
						projData.FtmNoteInstruments.Add(new NoteInstrumentMapping { Channel = kvp.Key.Item1, Time = kvp.Key.Item2, InstrumentId = kvp.Value });
					}
				}

				if (!string.IsNullOrEmpty(_referenceAudioPath) && File.Exists(_referenceAudioPath))
				{
					projData.ReferenceAudioFilename = System.IO.Path.GetFileName(_referenceAudioPath);
				}

				using (var fileStream = new FileStream(dialog.FileName, FileMode.Create))
				using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
				{
					var jsonEntry = archive.CreateEntry("project.json");
					using (var entryStream = jsonEntry.Open())
					{
						JsonSerializer.Serialize(entryStream, projData, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true });
					}

					var midiEntry = archive.CreateEntry("sequence.mid");
					using (var entryStream = midiEntry.Open())
					{
						_loadedMidi?.Write(entryStream);
					}

					if (!string.IsNullOrEmpty(projData.ReferenceAudioFilename) && File.Exists(_referenceAudioPath))
					{
						var audioEntry = archive.CreateEntry(projData.ReferenceAudioFilename);
						using (var entryStream = audioEntry.Open())
						using (var fs = new FileStream(_referenceAudioPath, FileMode.Open, FileAccess.Read))
						{
							fs.CopyTo(entryStream);
						}
					}
				}
			}
			catch (Exception ex)
			{
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}


	private async void FileOpenProject_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog();
		dialog.Filter = "MidiMaster Project (*.mmp)|*.mmp";
		if (dialog.ShowDialog() == true)
		{
			try
			{
				StopButton_Click(null, null);
				
				using (var fileStream = new FileStream(dialog.FileName, FileMode.Open))
				using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
				{
					var jsonEntry = archive.GetEntry("project.json");
					if (jsonEntry != null)
					{
						using (var entryStream = jsonEntry.Open())
						{
							var projData = JsonSerializer.Deserialize<MidiMasterProjectData>(entryStream, new JsonSerializerOptions { IncludeFields = true });
							if (projData != null)
							{
								_audioEngine.EngineSpeed = projData.EngineSpeed;
								_audioEngine.CustomFtmInstruments = projData.CustomFtmInstruments;
								_audioEngine.CustomFtmSequences = projData.CustomFtmSequences;
								_audioEngine.FtmNoteInstruments = new Dictionary<(int, long), int>();
								foreach (var mapping in projData.FtmNoteInstruments)
								{
									_audioEngine.FtmNoteInstruments[(mapping.Channel, mapping.Time)] = mapping.InstrumentId;
								}

								if (!string.IsNullOrEmpty(projData.ReferenceAudioFilename))
								{
									var audioEntry = archive.GetEntry(projData.ReferenceAudioFilename);
									if (audioEntry != null)
									{
										string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MidiMaster", projData.ReferenceAudioFilename);
										Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tempPath));
										audioEntry.ExtractToFile(tempPath, true);
										_referenceAudioPath = tempPath;
										_audioEngine.LoadReferenceTrack(tempPath);
										_referenceWaveformPeaks = await WaveformRenderer.GeneratePeaksAsync(tempPath, 1024);
										DrawWaveforms();
									}
								}
							}
						}
					}

					var midiEntry = archive.GetEntry("sequence.mid");
					if (midiEntry != null)
					{
						using (var entryStream = midiEntry.Open())
						using (var ms = new MemoryStream())
						{
							entryStream.CopyTo(ms);
							ms.Position = 0;
							_loadedMidi = MidiFile.Read(ms);
						}
					}
				}
				
				InstrumentNames = _audioEngine.GetInstrumentNames();
				UndoManager.Clear();
				UpdateUndoUI();
				
				ParseTimeSignatures();
				BuildNoteCache();
				LoadMidiIntoAudioEngine();
				DrawPianoRoll();
				GenerateTrackerView();
			}
			catch (Exception ex)
			{
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Error opening project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}


	private void ImportMidi_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*";
		if (openFileDialog.ShowDialog() == true)
		{
			LoadMidiFile(openFileDialog.FileName);
		}
	}


	private void FileImportFtm_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "FamiTracker Module (*.ftm)|*.ftm|All files (*.*)|*.*";
		if (openFileDialog.ShowDialog() == true)
		{
			try
			{
				var (importedMidiPath, instruments, sequences, engineSpeed, instrumentMap) = FtmParser.Parse(openFileDialog.FileName);
				_audioEngine.FtmNoteInstruments = instrumentMap;
				if (_audioEngine != null)
				{
					_audioEngine.CustomFtmInstruments = instruments;
					_audioEngine.CustomFtmSequences = sequences;
					_audioEngine.EngineSpeed = engineSpeed;
					InstrumentNames = _audioEngine.GetInstrumentNames();
				}
				LoadMidiFile(importedMidiPath);
			}
			catch (Exception ex)
			{
				System.IO.File.WriteAllText("crash.log", ex.ToString());
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Failed to import FTM file:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}


	private void ExportMidi_Click(object sender, RoutedEventArgs e)
	{
		if (_loadedMidi == null)
		{
			SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("No MIDI file is currently loaded.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
		}

		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*";
		saveFileDialog.DefaultExt = ".mid";
		saveFileDialog.Title = "Export as MIDI";

		if (saveFileDialog.ShowDialog() == true)
		{
			try
			{
				_loadedMidi.Write(saveFileDialog.FileName, true, format: MidiFileFormat.MultiTrack);
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("MIDI file exported successfully!", "Export Success", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Error exporting MIDI: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}


	private void LoadMidiFile(string filePath)
	{
		try
		{
			MidiFile loadedMidi;
			bool hadInvalidValues = false;
			try
			{
				// First try reading strictly
				loadedMidi = MidiFile.Read(filePath);
			}
			catch
			{
				// If it fails (due to invalid values like 150 velocity), read leniently
				hadInvalidValues = true;
				var settings = new ReadingSettings
				{
					InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.SnapToLimits,
					InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
					NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
					NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
					InvalidMetaEventParameterValuePolicy = InvalidMetaEventParameterValuePolicy.SnapToLimits
				};
				loadedMidi = MidiFile.Read(filePath, settings);
			}
			_loadedMidi = loadedMidi;

			// Strip pan (CC#10) events and warn user
			bool hadPanData = false;
			foreach (var chunk in _loadedMidi.GetTrackChunks())
			{
				using (var manager = chunk.ManageTimedEvents())
				{
					var panEvents = manager.Objects
						.Where(e => e.Event is ControlChangeEvent cce && cce.ControlNumber == 10)
						.ToList();
					if (panEvents.Count > 0)
					{
						hadPanData = true;
						foreach (var pe in panEvents)
							manager.Objects.Remove(pe);
					}
				}
			}

			UndoManager.Clear();
			UndoManager.PushState(_loadedMidi, "Open MIDI Project", 0, 0);
			UpdateUndoUI();
			ParseTimeSignatures();
			BuildNoteCache();
			LoadMidiIntoAudioEngine();
			DrawPianoRoll();
			if (ViewTrackerMenu.IsChecked)
			{
				GenerateTrackerView();
			}

			if (hadPanData)
			{
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show(
					"This MIDI contains stereo panning data (CC#10) which isn't read in-game. " +
					"The pan data has been automatically removed and all tracks have been set to mono.",
					"Stereo Data Removed",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}

			if (hadInvalidValues)
			{
				SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show(
					"This MIDI file contained illegal parameter values (such as Note Velocity > 127). " +
					"These values have been automatically clamped to their legal limits.",
					"Illegal Values Fixed",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}
		catch (Exception ex)
		{
			SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Error loading MIDI: " + ex.Message);
		}
	}

    }
}
