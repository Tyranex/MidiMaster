using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Common;
using System.Linq;
using Melanchall.DryWetMidi.Core;


namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void UpdateVelocityAtCursor(int hexValue)
	{
		if (_loadedMidi == null) return;
		var tracks = _loadedMidi.GetTrackChunks().ToList();
		if (_activeTrackIndex >= tracks.Count) return;

		int trackAbsoluteIndex = _activeTrackIndex;
		var chunk = tracks[trackAbsoluteIndex];

		int visualIndex = _trackerAbsoluteIndices.IndexOf(trackAbsoluteIndex);
		if (visualIndex < 0 || visualIndex >= _activeTrackNotes.Count) return;

		var notes = _activeTrackNotes[visualIndex];

		Dictionary<double, int> dictionary = new Dictionary<double, int>();
		Note targetNote = null;
		foreach (Note item in notes)
		{
			double num7 = (double)item.Time / (double)_ticksPerQuarterNote;
			double num8 = Math.Round(num7 * 4.0);
			if (!dictionary.ContainsKey(num8))
			{
				dictionary[num8] = 0;
			}
			int num9 = dictionary[num8]++;
			if (num8 == (double)_trackerCursorRow && num9 == _activePolyphonyIndex)
			{
				targetNote = item;
				break;
			}
		}

		if (targetNote != null)
		{
			int currentVelocity = targetNote.Velocity;
			int newVelocity = ((currentVelocity & 0x0F) << 4) | hexValue;
			newVelocity &= 0x7F;

			using (var notesManager = chunk.ManageNotes())
			{
				var noteToUpdate = notesManager.Objects.FirstOrDefault(n => n.Time == targetNote.Time && n.NoteNumber == targetNote.NoteNumber);
				if (noteToUpdate != null)
				{
					noteToUpdate.Velocity = (SevenBitNumber)(byte)newVelocity;
				}
			}

			string mbPos = GetMeasureBeatString(_trackerCursorRow);
			PushUndoState($"Set Velocity to {newVelocity:X2} on Track {_activeTrackIndex + 1} at {mbPos}");

			BuildNoteCache();

			// Instant visual feedback via overlay
			if (_velocityEditOverlay == null)
			{
				_velocityEditOverlay = new TextBlock
				{
					Foreground = Brushes.LightSkyBlue,
					FontFamily = new FontFamily("Consolas"),
					FontSize = 12.0,
					Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
				};
				Panel.SetZIndex(_velocityEditOverlay, 100);
				TrackerContentCanvas.Children.Add(_velocityEditOverlay);
			}

			_velocityEditOverlay.Text = newVelocity.ToString("X2");

			double colWidth = GetTrackColWidth(_trackerCanvasIndices[visualIndex]);
			double num2 = (_trackXOffsets != null && visualIndex < _trackXOffsets.Count) ? _trackXOffsets[visualIndex] : (30 + visualIndex * colWidth);
			double highlightStart = num2 + (_activePolyphonyIndex * colWidth);

			Canvas.SetLeft(_velocityEditOverlay, highlightStart + 26.0);
			Canvas.SetTop(_velocityEditOverlay, _trackerCursorRow * 14.0);
			_velocityEditOverlay.Visibility = Visibility.Visible;

		}
	}


	private void InsertNoteAtPlayhead(int noteNum)
	{
		if (_loadedMidi == null)
		{
			return;
		}
		List<TrackChunk> list = _loadedMidi.GetTrackChunks().ToList();
		if (_activeTrackIndex < list.Count)
		{
			TrackChunk trackChunk = list[_activeTrackIndex];
			short num = 480;
			if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNoteTimeDivision)
			{
				num = ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote;
			}
			long num2 = num / 4;
			long num3 = (long)(currentPlayheadX / (double)BeatWidth * (double)num);
			num3 = num3 / num2 * num2;
			using (TimedObjectsManager<Note> timedObjectsManager = trackChunk.ManageNotes())
			{
				                Note note = new Note(new SevenBitNumber((byte)noteNum), num2, num3);
                note.Velocity = (SevenBitNumber)(byte)PreferencesManager.Current.DefaultVelocity;
                // Assign correct channel: percussion tracks use channel 9, otherwise use the channel assigned to the active track
                if (IsPercussionTrack(_activeTrackIndex))
                {
                    note.Channel = (FourBitNumber)9;
                }
                else if (_trackChannels != null && _trackChannels.Count > _activeTrackIndex)
                {
                    note.Channel = (FourBitNumber)_trackChannels[_activeTrackIndex];
                }
                timedObjectsManager.Objects.Add(note);
			}

			bool isPerc = IsPercussionTrack(_activeTrackIndex);
			string noteLabel = isPerc ? MidiDrumToTrackerText((byte)noteNum) : GetNoteName((byte)noteNum);
			string mbPos = GetMeasureBeatString(_trackerCursorRow);
			PushUndoState($"Placed {noteLabel} on Track {_activeTrackIndex + 1} at {mbPos}");

			BuildNoteCache();

			if (TrackerCursor.Visibility == Visibility.Visible)
			{
				TextBlock tempNote = new TextBlock
				{
					Text = isPerc ? MidiDrumToTrackerText((byte)noteNum) : GetNoteName((byte)noteNum),
					Foreground = isPerc ? Brushes.Orange : Brushes.White,
					FontFamily = new FontFamily("Consolas"),
					FontSize = 12.0
				};
				double left = Canvas.GetLeft(TrackerCursor);
				if (_activeSubColumn == 1) left -= 22.0;
				if (_activeSubColumn == 2) left -= 44.0;
				Canvas.SetLeft(tempNote, left + 2.0);
				Canvas.SetTop(tempNote, Canvas.GetTop(TrackerCursor));
				TrackerContentCanvas.Children.Add(tempNote);
			}

			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
	}


	private void DeleteNoteAtPlayhead()
	{
		if (_loadedMidi == null) return;
		List<TrackChunk> list = _loadedMidi.GetTrackChunks().ToList();
		if (_activeTrackIndex >= list.Count) return;

		TrackChunk trackChunk = list[_activeTrackIndex];
		short num = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision timeDiv)
			num = timeDiv.TicksPerQuarterNote;
			
		long num2 = num / 4;
		long time = (long)(currentPlayheadX / (double)BeatWidth * (double)num);
		time = (time / num2) * num2;

		bool modified = false;
		using (TimedObjectsManager<Note> timedObjectsManager = trackChunk.ManageNotes())
		{
			// First, try to delete notes STARTING at this time
			var notesStartingHere = timedObjectsManager.Objects.Where(n => n.Time == time).ToList();
			if (notesStartingHere.Count > 0)
			{
				PushUndoState($"Deleted note(s) on Track {_activeTrackIndex + 1} at {GetMeasureBeatString(_trackerCursorRow)}");
				foreach (Note item in notesStartingHere)
					timedObjectsManager.Objects.Remove(item);
				modified = true;
			}
			else if (ViewTrackerMenu.IsChecked)
			{
				// If no notes start here, try to delete a "mute note" (a note ending at this time)
				// We extend the note to the next note's start time, or if none, by 1 beat.
				var notesEndingHere = timedObjectsManager.Objects.Where(n => n.EndTime == time).ToList();
				if (notesEndingHere.Count > 0)
				{
					PushUndoState($"Deleted mute note(s) on Track {_activeTrackIndex + 1} at {GetMeasureBeatString(_trackerCursorRow)}");
					foreach (Note item in notesEndingHere)
					{
						// Find next note on same track
						var nextNote = timedObjectsManager.Objects.Where(n => n.Time >= time && n != item).OrderBy(n => n.Time).FirstOrDefault();
						if (nextNote != null)
							item.Length = nextNote.Time - item.Time;
						else
							item.Length += num; // Extend by 1 quarter note
					}
					modified = true;
				}
			}
		}

		if (modified)
		{
			BuildNoteCache();

			if (TrackerCursor.Visibility == Visibility.Visible)
			{
				Rectangle eraser = new Rectangle
				{
					Width = 22.0,
					Height = 14.0,
					Fill = new SolidColorBrush(Color.FromRgb(30, 30, 30))
				};
				double left = TrackerCursor.Margin.Left;
				if (_activeSubColumn == 1) left -= 22.0;
				if (_activeSubColumn == 2) left -= 44.0;
				Canvas.SetLeft(eraser, left);
				Canvas.SetTop(eraser, TrackerCursor.Margin.Top);
				TrackerContentCanvas.Children.Add(eraser);
			}

			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
	}


	private void PlaceMuteNoteAtPlayhead()
	{
		if (_loadedMidi == null || !ViewTrackerMenu.IsChecked) return;
		List<TrackChunk> list = _loadedMidi.GetTrackChunks().ToList();
		if (_activeTrackIndex >= list.Count) return;

		TrackChunk trackChunk = list[_activeTrackIndex];
		short ticksPerQuarterNote = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision timeDiv)
			ticksPerQuarterNote = timeDiv.TicksPerQuarterNote;
			
		long rowTicks = ticksPerQuarterNote / 4;
		long time = _trackerCursorRow * rowTicks;

		using (TimedObjectsManager<Note> notesManager = trackChunk.ManageNotes())
		{
			// Find the active note on the active polyphony channel that overlaps or ends after this time
			// We group notes by row to find polyphony indices
			var activeNotes = notesManager.Objects.Where(n => n.Time < time && n.EndTime > time).ToList();
			if (activeNotes.Count == 0) return; // No notes playing

			PushUndoState($"Placed mute note on Track {_activeTrackIndex + 1} at {GetMeasureBeatString(_trackerCursorRow)}");
			
			// We ideally just truncate ALL notes on this track that extend past this point if we can't reliably resolve polyphony
			// Since Tracker often has 1 note per polyphony channel, let's just truncate the ones that are active
			foreach (var note in activeNotes)
			{
				note.Length = time - note.Time;
			}
		}

		BuildNoteCache();
		GenerateTrackerView();
		
		_midiSaveTimer.Stop();
		_midiSaveTimer.Start();
	}


	private void VelocityRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		PianoRollGrid.Focus();
	}

    }
}
