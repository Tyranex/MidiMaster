using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
using System.Globalization;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace SS14_MIDI_IDE
{
	public partial class MainWindow : Window
	{
	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Invalid comparison between Unknown and I4
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Invalid comparison between Unknown and I4
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Invalid comparison between Unknown and I4
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Invalid comparison between Unknown and I4
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Invalid comparison between Unknown and I4
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Invalid comparison between Unknown and I4
		//IL_02f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Invalid comparison between Unknown and I4
		if (e.OriginalSource is TextBox)
		{
			return;
		}
		int value;
		if (e.Key == Key.Enter)
		{
			if (isPlaying)
			{
				StopButton_Click(this, new RoutedEventArgs());
			}
			else
			{
				PlayButton_Click(this, new RoutedEventArgs());
			}
			e.Handled = true;
		}
		else if (e.Key == Key.PageDown && ViewTrackerMenu.IsChecked)
		{
			bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
			if (isShift && _trackerSelectionStartRow == null) {
				_trackerSelectionStartRow = _trackerCursorRow;
				_trackerSelectionStartLogicalCol = logicalCol;
			}
			else if (!isShift) {
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
			}

			GetMeasureBoundsForRow(_trackerCursorRow, out int curMeasureStart, out int curMeasureRowCount);
			int targetRow = curMeasureStart + curMeasureRowCount;
			_trackerCursorRow = Math.Max(0, targetRow);
			if (isShift) {
				_trackerSelectionEndRow = _trackerCursorRow;
				_trackerSelectionEndLogicalCol = logicalCol;
			}

			SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
			if (!isPlaying)
			{
				double tempo = 120.0;
				if (int.TryParse(TempoTextBox.Text, out var tResult)) tempo = tResult;
				double seconds = (_trackerCursorRow / 4.0) * (60.0 / tempo);
				_audioEngine?.Seek(seconds);
				UpdateVisualPlayhead();
			}
			e.Handled = true;
		}
		else if (e.Key == Key.PageUp && ViewTrackerMenu.IsChecked)
		{
			bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
			if (isShift && _trackerSelectionStartRow == null) {
				_trackerSelectionStartRow = _trackerCursorRow;
				_trackerSelectionStartLogicalCol = logicalCol;
			}
			else if (!isShift) {
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
			}

			GetMeasureBoundsForRow(_trackerCursorRow, out int curMeasureStart, out int curMeasureRowCount);
			int targetRow;
			if (_trackerCursorRow > curMeasureStart)
			{
				targetRow = curMeasureStart;
			}
			else if (_trackerCursorRow > 0)
			{
				GetMeasureBoundsForRow(_trackerCursorRow - 1, out int prevMeasureStart, out int _);
				targetRow = prevMeasureStart;
			}
			else
			{
				targetRow = 0;
			}
			_trackerCursorRow = Math.Max(0, targetRow);
			if (isShift) {
				_trackerSelectionEndRow = _trackerCursorRow;
				_trackerSelectionEndLogicalCol = logicalCol;
			}

			SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
			if (!isPlaying)
			{
				double tempo = 120.0;
				if (int.TryParse(TempoTextBox.Text, out var tResult)) tempo = tResult;
				double seconds = (_trackerCursorRow / 4.0) * (60.0 / tempo);
				_audioEngine?.Seek(seconds);
				UpdateVisualPlayhead();
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Home && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && ViewTrackerMenu.IsChecked)
		{
			bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
			if (isShift && _trackerSelectionStartRow == null) {
				_trackerSelectionStartRow = _trackerCursorRow;
				_trackerSelectionStartLogicalCol = logicalCol;
			}
			else if (!isShift) {
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
			}

			_trackerCursorRow = 0;

			if (isShift) {
				_trackerSelectionEndRow = _trackerCursorRow;
				_trackerSelectionEndLogicalCol = logicalCol;
			}

			SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
			if (!isPlaying)
			{
				_audioEngine?.Seek(0.0);
				UpdateVisualPlayhead();
			}
			e.Handled = true;
		}
		else if (e.Key == Key.End && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && ViewTrackerMenu.IsChecked)
		{
			bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
			if (isShift && _trackerSelectionStartRow == null) {
				_trackerSelectionStartRow = _trackerCursorRow;
				_trackerSelectionStartLogicalCol = logicalCol;
			}
			else if (!isShift) {
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
			}

			int maxRow = 0;
			if (_cachedNotes != null)
			{
				foreach (var trackNotes in _cachedNotes)
				{
					foreach (var n in trackNotes)
					{
						int endRow = (int)Math.Round(((double)n.EndTime / _ticksPerQuarterNote) * 4.0);
						if (endRow > maxRow) maxRow = endRow;
					}
				}
			}
			_trackerCursorRow = maxRow;

			if (isShift) {
				_trackerSelectionEndRow = _trackerCursorRow;
				_trackerSelectionEndLogicalCol = logicalCol;
			}

			SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
			if (!isPlaying)
			{
				double tempo = 120.0;
				if (int.TryParse(TempoTextBox.Text, out var tResult)) tempo = tResult;
				double seconds = (_trackerCursorRow / 4.0) * (60.0 / tempo);
				_audioEngine?.Seek(seconds);
				UpdateVisualPlayhead();
			}
			e.Handled = true;
		}
				else if (e.Key == Key.Back && TrackerView.Visibility == Visibility.Visible && _editMode)
		{
			PlaceMuteNoteAtPlayhead();
			e.Handled = true;
		}
		else if ((int)e.Key == 32)
		{
			if (_trackerSelectionStartRow.HasValue && _trackerSelectionEndRow.HasValue)
			{
				DeleteTrackerSelection();
			}
			else
			{
				DeleteNoteAtPlayhead();
			}
			e.Handled = true;
		}
		else if (TrackerView.Visibility == Visibility.Visible && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.PageUp || e.Key == Key.PageDown || (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
		{
			bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
			bool isShiftKey = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			if (isCtrl && e.Key == Key.Z)
			{
				if (isShiftKey)
				{
					PerformRedo();
				}
				else
				{
					PerformUndo();
				}
				e.Handled = true;
				return;
			}
			if (isCtrl && e.Key == Key.Y)
			{
				PerformRedo();
				e.Handled = true;
				return;
			}
			if (isCtrl && e.Key == Key.H)
			{
				OpenUndoHistoryDialog();
				e.Handled = true;
				return;
			}
			if (isCtrl && e.Key == Key.X)
			{
				CutTrackerSelection();
				e.Handled = true;
				return;
			}
			if (isCtrl && e.Key == Key.C)
			{
				CopyTrackerSelection();
				e.Handled = true;
				return;
			}
			if (isCtrl && e.Key == Key.V)
			{
				PasteTrackerSelection();
				e.Handled = true;
				return;
			}

			bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			if (e.Key == Key.Up)
			{
				if (_trackerCursorRow > 0)
				{
					int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
					if (isShift && _trackerSelectionStartRow == null) {
						_trackerSelectionStartRow = _trackerCursorRow;
						_trackerSelectionStartLogicalCol = logicalCol;
					}
					else if (!isShift) {
						_trackerSelectionStartRow = _trackerSelectionEndRow = null;
						_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
					}
					_trackerCursorRow--;
					if (isShift) {
						_trackerSelectionEndRow = _trackerCursorRow;
						_trackerSelectionEndLogicalCol = logicalCol;
					}
					SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Down)
			{
				int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
				if (isShift && _trackerSelectionStartRow == null) {
					_trackerSelectionStartRow = _trackerCursorRow;
					_trackerSelectionStartLogicalCol = logicalCol;
				}
				else if (!isShift) {
					_trackerSelectionStartRow = _trackerSelectionEndRow = null;
					_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
				}
				_trackerCursorRow++;
				if (isShift) {
					_trackerSelectionEndRow = _trackerCursorRow;
					_trackerSelectionEndLogicalCol = logicalCol;
				}
				SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
				e.Handled = true;
			}
			else if (e.Key == Key.Left)
			{
				int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);

				if (isShift && _trackerSelectionStartLogicalCol == null) {
					_trackerSelectionStartRow = _trackerCursorRow;
					_trackerSelectionStartLogicalCol = logicalCol;
				}
				else if (!isShift) {
					_trackerSelectionStartRow = _trackerSelectionEndRow = null;
					_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
				}

				if (_activeSubColumn > 0)
				{
					_activeSubColumn--;
					UpdateActiveTrackVisuals();
				}
				else
				{
					int num = -1;
					for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
					{
						if (_trackerAbsoluteIndices[m] == _activeTrackIndex)
						{
							num = m;
							break;
						}
					}
					if (num >= 0)
					{
						if (_activePolyphonyIndex > 0)
						{
							_activeSubColumn = 2;
							_activePolyphonyIndex--;
							UpdateActiveTrackVisuals();
						}
						else if (num > 0)
						{
							_activeTrackIndex = _trackerAbsoluteIndices[num - 1];
							_activePolyphonyIndex = _trackPolyphonies[num - 1] - 1;
							_activeSubColumn = 2;
							UpdateActiveTrackVisuals();
						}
					}
				}
				if (isShift) {
					_trackerSelectionEndRow = _trackerCursorRow;
					_trackerSelectionEndLogicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
				}
				e.Handled = true;
			}
			else if (e.Key == Key.Right)
			{
				int logicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);

				if (isShift && _trackerSelectionStartLogicalCol == null) {
					_trackerSelectionStartRow = _trackerCursorRow;
					_trackerSelectionStartLogicalCol = logicalCol;
				}
				else if (!isShift) {
					_trackerSelectionStartRow = _trackerSelectionEndRow = null;
					_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
				}

				if (_activeSubColumn < 2)
				{
					_activeSubColumn++;
					UpdateActiveTrackVisuals();
				}
				else
				{
					int num = -1;
					for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
					{
						if (_trackerAbsoluteIndices[m] == _activeTrackIndex)
						{
							num = m;
							break;
						}
					}
					if (num >= 0)
					{
						if (_activePolyphonyIndex < _trackPolyphonies[num] - 1)
						{
							_activeSubColumn = 0;
							_activePolyphonyIndex++;
							UpdateActiveTrackVisuals();
						}
						else if (num < _trackerAbsoluteIndices.Count - 1)
						{
							_activeTrackIndex = _trackerAbsoluteIndices[num + 1];
							_activePolyphonyIndex = 0;
							_activeSubColumn = 0;
							UpdateActiveTrackVisuals();
						}
					}
				}
				if (isShift) {
					_trackerSelectionEndRow = _trackerCursorRow;
					_trackerSelectionEndLogicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
				}
				e.Handled = true;
			}
		}
		else if (TrackerView.Visibility == Visibility.Visible && _activeSubColumn == 1 && TryGetHexValue(e.Key, out int hexValue))
		{
			UpdateVelocityAtCursor(hexValue);
			e.Handled = true;
		}
		else if (TrackerView.Visibility != Visibility.Visible || _activeSubColumn == 0)
		{
			if (e.Key == Key.Space)
			{
				_editMode = !_editMode;
				UpdateEditModeTint();
				e.Handled = true;
				return;
			}

			if (_activeSubColumn == 2 && TrackerView.Visibility == Visibility.Visible && e.Key == Key.M && _editMode)
			{
				OpenVibratoEditDialog(_activeTrackIndex, _trackerCursorRow);
				e.Handled = true;
				return;
			}

			if (_activeSubColumn == 2 && TrackerView.Visibility == Visibility.Visible && e.Key == Key.S && _editMode && (Keyboard.Modifiers & ModifierKeys.Control) == 0)
			{
				OpenSlideEditDialog(_activeTrackIndex, _trackerCursorRow);
				e.Handled = true;
				return;
			}

			bool isPerc = IsPercussionTrack(_activeTrackIndex);
			if (isPerc && _keyboardToDrumMap.TryGetValue(e.Key, out int drumValue))
			{
				if (drumValue >= 0 && drumValue <= 127)
				{
					if (!e.IsRepeat)
					{
						_audioEngine?.PreviewNoteOn(_activeTrackIndex, drumValue);
						if (_editMode)
						{
							if (TrackerView.Visibility == Visibility.Visible) { 
								_trackerSelectionStartRow = _trackerSelectionEndRow = null;
								_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
								SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth); 
							}
							InsertNoteAtPlayhead(drumValue);
							if (TrackerView.Visibility == Visibility.Visible) { _trackerCursorRow++; SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth); }
							else { AdvancePlayheadOneRow(); }
						}
					}
				}
				e.Handled = true;
			}
			else if (!isPerc && _keyboardToNoteOffset.TryGetValue(e.Key, out int noteValue))
			{
				int num6 = _currentOctave * 12 + noteValue;
				if (num6 >= 0 && num6 <= 127)
				{
					if (!e.IsRepeat)
					{
						int channel = GetTrackChannel(_activeTrackIndex);
						_audioEngine?.PreviewNoteOn(_activeTrackIndex, num6);
						if (_editMode)
						{
							if (TrackerView.Visibility == Visibility.Visible) { 
								_trackerSelectionStartRow = _trackerSelectionEndRow = null;
								_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
								SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth); 
							}
							InsertNoteAtPlayhead(num6);
							if (TrackerView.Visibility == Visibility.Visible) { _trackerCursorRow++; SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth); }
							else { AdvancePlayheadOneRow(); }
						}
					}
				}
				e.Handled = true;
			}
		}
	}

	private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
	{
		UpdateScrollLockUI();

		if (e.OriginalSource is TextBox || e.Key == Key.PageUp || e.Key == Key.PageDown)
		{
			return;
		}
		if (TrackerView.Visibility != Visibility.Visible || _activeSubColumn == 0)
		{
			bool isPerc = IsPercussionTrack(_activeTrackIndex);
			if (isPerc && _keyboardToDrumMap.TryGetValue(e.Key, out int drumValue))
			{
				if (drumValue >= 0 && drumValue <= 127)
				{
					_audioEngine?.PreviewNoteOff(_activeTrackIndex, drumValue);
				}
			}
			else if (!isPerc && _keyboardToNoteOffset.TryGetValue(e.Key, out int noteValue))
			{
				int num6 = _currentOctave * 12 + noteValue;
				if (num6 >= 0 && num6 <= 127)
				{
					int channel = GetTrackChannel(_activeTrackIndex);
					_audioEngine?.PreviewNoteOff(_activeTrackIndex, num6);
				}
			}
		}
	}

	private void CutTrackerSelection()
	{
		CopyTrackerSelection();
		DeleteTrackerSelection();
	}

	private void CopyTrackerSelection()
	{
		if (_loadedMidi == null || !_trackerSelectionStartRow.HasValue || !_trackerSelectionEndRow.HasValue || !_trackerSelectionStartLogicalCol.HasValue || !_trackerSelectionEndLogicalCol.HasValue)
			return;

		int startRow = Math.Min(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
		int endRow = Math.Max(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
		int startCol = Math.Min(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);
		int endCol = Math.Max(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);

		long startTime = (long)(startRow * _ticksPerQuarterNote / 4.0);
		long endTime = (long)((endRow + 1) * _ticksPerQuarterNote / 4.0);

		_trackerClipboardCells.Clear();

		for (int c = startCol; c <= endCol; c++)
		{
			GetLogicalColumnDetails(c, out int trackIdx, out int polyIdx, out int subCol);
			if (trackIdx >= 0 && trackIdx < _loadedMidi.GetTrackChunks().Count())
			{
				var chunk = _loadedMidi.GetTrackChunks().ElementAt(trackIdx);
				using (var notesManager = chunk.ManageNotes())
				{
					// Group notes by row
					Dictionary<int, List<Note>> notesByRow = new Dictionary<int, List<Note>>();
					Dictionary<int, List<Note>> mutesByRow = new Dictionary<int, List<Note>>();
					
					Dictionary<int, int> polyphonyCounter = new Dictionary<int, int>();
					Dictionary<Note, int> noteToPolyIndex = new Dictionary<Note, int>();
					
					foreach (var note in notesManager.Objects)
					{
						int noteStartRow = (int)Math.Round((double)note.Time / _ticksPerQuarterNote * 4.0);
						if (!polyphonyCounter.ContainsKey(noteStartRow)) polyphonyCounter[noteStartRow] = 0;
						noteToPolyIndex[note] = polyphonyCounter[noteStartRow];
						polyphonyCounter[noteStartRow]++;
					
						if (note.Time >= startTime && note.Time < endTime)
						{
							if (!notesByRow.ContainsKey(noteStartRow)) notesByRow[noteStartRow] = new List<Note>();
							notesByRow[noteStartRow].Add(note);
						}
						
						long endTick = note.EndTime;
						if (endTick >= startTime && endTick <= endTime)
						{
							int noteEndRow = (int)Math.Round((double)endTick / _ticksPerQuarterNote * 4.0);
							if (!mutesByRow.ContainsKey(noteEndRow)) mutesByRow[noteEndRow] = new List<Note>();
							mutesByRow[noteEndRow].Add(note);
						}
					}

					if (notesByRow.Count > 0)
					{
						foreach (var kvp in notesByRow)
						{
							foreach (var note in kvp.Value)
							{
								if (noteToPolyIndex[note] == polyIdx)
								{
									ClipboardCell cell = new ClipboardCell
									{
										RowOffset = kvp.Key - startRow,
										LogicalColOffset = c - startCol,
										Value = subCol == 0 ? note.NoteNumber : (subCol == 1 ? note.Velocity : note.Channel)
									};
									_trackerClipboardCells.Add(cell);
								}
							}
						}
					}
					
					if (subCol == 0 && mutesByRow.Count > 0) // Only store mutes on the Note column
					{
						foreach (var kvp in mutesByRow)
						{
							foreach (var muteNote in kvp.Value)
							{
								if (noteToPolyIndex[muteNote] == polyIdx)
								{
									// Check if there is a note starting at this exact row and polyIdx to prevent overlapping a note with a mute
									bool hasNoteHere = notesByRow.ContainsKey(kvp.Key) && notesByRow[kvp.Key].Any(n => noteToPolyIndex[n] == polyIdx);
									if (!hasNoteHere)
									{
										ClipboardCell cell = new ClipboardCell
										{
											RowOffset = kvp.Key - startRow,
											LogicalColOffset = c - startCol,
											Value = -1 // -1 means mute
										};
										_trackerClipboardCells.Add(cell);
									}
								}
							}
						}
					}
				}
			}
		}
	}

	private void PasteTrackerSelection()
	{
		if (_loadedMidi == null || _trackerClipboardCells.Count == 0)
			return;

		int baseLogicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);
		int baseRow = _trackerCursorRow;

		// Group cells by target Track
		var cellsByTrack = _trackerClipboardCells.GroupBy(c => {
			GetLogicalColumnDetails(baseLogicalCol + c.LogicalColOffset, out int tr, out _, out _);
			return tr;
		});

		foreach (var trackGroup in cellsByTrack)
		{
			int trackIdx = trackGroup.Key;
			if (trackIdx >= 0 && trackIdx < _loadedMidi.GetTrackChunks().Count())
			{
				var chunk = _loadedMidi.GetTrackChunks().ElementAt(trackIdx);
				using (var notesManager = chunk.ManageNotes())
				{
					Dictionary<int, List<Note>> notesByRow = new Dictionary<int, List<Note>>();
					foreach (var note in notesManager.Objects)
					{
						int r = (int)Math.Round((double)note.Time / _ticksPerQuarterNote * 4.0);
						if (!notesByRow.ContainsKey(r)) notesByRow[r] = new List<Note>();
						notesByRow[r].Add(note);
					}

					var cellsByRow = trackGroup.GroupBy(c => baseRow + c.RowOffset);
					foreach (var rowGroup in cellsByRow)
					{
						int targetRow = rowGroup.Key;
						if (!notesByRow.ContainsKey(targetRow)) notesByRow[targetRow] = new List<Note>();
						
						var rowNotes = notesByRow[targetRow];
						long targetTime = (long)(targetRow * _ticksPerQuarterNote / 4.0);

						foreach (var cell in rowGroup)
						{
							GetLogicalColumnDetails(baseLogicalCol + cell.LogicalColOffset, out int cellTrackIdx, out int polyIdx, out int subCol);
							
							FourBitNumber defaultChannel = IsPercussionTrack(cellTrackIdx) ? (FourBitNumber)9 : GetTrackChannel(cellTrackIdx);

							if (subCol == 0 && cell.Value == -1)
							{
								// This is a mute note, truncate active notes
								var activeNotes = notesManager.Objects.Where(n => n.Time < targetTime && n.EndTime > targetTime).ToList();
								foreach (var activeNote in activeNotes)
								{
									activeNote.Length = targetTime - activeNote.Time;
								}
								continue;
							}

							while (rowNotes.Count <= polyIdx)
							{
								var newNote = new Note((SevenBitNumber)60, (long)(_ticksPerQuarterNote / 4), targetTime)
								{
									Velocity = (SevenBitNumber)PreferencesManager.Current.DefaultVelocity,
									Channel = defaultChannel
								};
								rowNotes.Add(newNote);
								notesManager.Objects.Add(newNote);
							}
							
							var targetNote = rowNotes[polyIdx];
							if (subCol == 0) targetNote.NoteNumber = (SevenBitNumber)(byte)Math.Clamp(cell.Value, 0, 127);
							else if (subCol == 1) targetNote.Velocity = (SevenBitNumber)(byte)Math.Clamp(cell.Value, 0, 127);
							else if (subCol == 2) targetNote.Channel = (FourBitNumber)(byte)Math.Clamp(cell.Value, 0, 15);
						}
					}
				}
			}
		}

		int maxRowOffset = _trackerClipboardCells.Max(c => c.RowOffset);
		_trackerCursorRow += maxRowOffset + 1;
		_trackerSelectionStartRow = _trackerSelectionEndRow = null;
		_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
		SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
		
		if (this is MainWindow window)
		{
			string mbPos = window.GetMeasureBeatString(baseRow);
			window.PushUndoState($"Pasted {_trackerClipboardCells.Count} cell(s) starting at {mbPos}");
			window.BuildNoteCache();
			window.GenerateTrackerView();
			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
	}

	private void DeleteTrackerSelection()
	{
		if (_loadedMidi == null || !_trackerSelectionStartRow.HasValue || !_trackerSelectionEndRow.HasValue || !_trackerSelectionStartLogicalCol.HasValue || !_trackerSelectionEndLogicalCol.HasValue)
			return;

		int startRow = Math.Min(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
		int endRow = Math.Max(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
		int startCol = Math.Min(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);
		int endCol = Math.Max(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);

		long startTime = (long)(startRow * _ticksPerQuarterNote / 4.0);
		long endTime = (long)((endRow + 1) * _ticksPerQuarterNote / 4.0);

		List<int> affectedTracks = new List<int>();
		for (int c = startCol; c <= endCol; c++)
		{
			GetLogicalColumnDetails(c, out int t, out _, out _);
			if (!affectedTracks.Contains(t)) affectedTracks.Add(t);
		}

		foreach (int trackIdx in affectedTracks)
		{
			if (trackIdx >= 0 && trackIdx < _loadedMidi.GetTrackChunks().Count())
			{
				var chunk = _loadedMidi.GetTrackChunks().ElementAt(trackIdx);
				using (var notesManager = chunk.ManageNotes())
				{
					Dictionary<int, List<Note>> notesByRow = new Dictionary<int, List<Note>>();
					Dictionary<int, List<Note>> mutesByRow = new Dictionary<int, List<Note>>();
					
					Dictionary<int, int> polyphonyCounter = new Dictionary<int, int>();
					Dictionary<Note, int> noteToPolyIndex = new Dictionary<Note, int>();
					
					foreach (var note in notesManager.Objects)
					{
						int noteStartRow = (int)Math.Round((double)note.Time / _ticksPerQuarterNote * 4.0);
						if (!polyphonyCounter.ContainsKey(noteStartRow)) polyphonyCounter[noteStartRow] = 0;
						noteToPolyIndex[note] = polyphonyCounter[noteStartRow];
						polyphonyCounter[noteStartRow]++;
						
						if (note.Time >= startTime && note.Time < endTime)
						{
							if (!notesByRow.ContainsKey(noteStartRow)) notesByRow[noteStartRow] = new List<Note>();
							notesByRow[noteStartRow].Add(note);
						}
						
						long endTick = note.EndTime;
						if (endTick >= startTime && endTick <= endTime)
						{
							int noteEndRow = (int)Math.Round((double)endTick / _ticksPerQuarterNote * 4.0);
							if (!mutesByRow.ContainsKey(noteEndRow)) mutesByRow[noteEndRow] = new List<Note>();
							mutesByRow[noteEndRow].Add(note);
						}
					}

					List<Note> notesToDelete = new List<Note>();

					for (int r = startRow; r <= endRow; r++)
					{
						long rowTime = (long)(r * _ticksPerQuarterNote / 4.0);
						
						if (notesByRow.ContainsKey(r))
						{
							var rowNotes = notesByRow[r];
							for (int c = startCol; c <= endCol; c++)
							{
								GetLogicalColumnDetails(c, out int t, out int p, out int s);
								if (t != trackIdx) continue;
								
								var targetNote = rowNotes.FirstOrDefault(n => noteToPolyIndex[n] == p);
								if (targetNote != null)
								{
									if (s == 0 && !notesToDelete.Contains(targetNote))
									{
										notesToDelete.Add(targetNote);
									}
									else if (s == 1)
									{
										targetNote.Velocity = (SevenBitNumber)PreferencesManager.Current.DefaultVelocity;
									}
									else if (s == 2)
									{
										targetNote.Channel = (FourBitNumber)0;
									}
								}
							}
						}
						
						if (mutesByRow.ContainsKey(r))
						{
							var rowMutes = mutesByRow[r];
							for (int c = startCol; c <= endCol; c++)
							{
								GetLogicalColumnDetails(c, out int t, out int p, out int s);
								if (t != trackIdx || s != 0) continue; // Mutes only exist on the note column
								
								bool hasNoteHere = notesByRow.ContainsKey(r) && notesByRow[r].Any(n => noteToPolyIndex[n] == p);
								if (!hasNoteHere)
								{
									var targetNote = rowMutes.FirstOrDefault(n => noteToPolyIndex[n] == p);
									if (targetNote != null)
									{
										// Extend the note to "delete" the mute
										var nextNote = notesManager.Objects.Where(n => n.Time >= targetNote.EndTime && n != targetNote).OrderBy(n => n.Time).FirstOrDefault();
										if (nextNote != null)
											targetNote.Length = nextNote.Time - targetNote.Time;
										else
											targetNote.Length += _ticksPerQuarterNote; // Extend by 1 beat
									}
								}
							}
						}
					}
					
					foreach (var note in notesToDelete)
					{
						notesManager.Objects.Remove(note);
					}
				}
			}
		}

		if (this is MainWindow window)
		{
			string mbStart = window.GetMeasureBeatString(startRow);
			string mbEnd = window.GetMeasureBeatString(endRow);
			window.PushUndoState($"Deleted selection from {mbStart} to {mbEnd}");
			window.BuildNoteCache();
			window.GenerateTrackerView();
			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
		
		_trackerSelectionStartRow = _trackerSelectionEndRow = null;
		_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
		if (this is MainWindow window2) { window2.UpdateTrackerCursor(); }
	}

	private int GetLogicalColumnIndex(int absoluteTrackIndex, int polyphonyIndex, int subColumn)
	{
		int logicalCol = 0;
		for (int i = 0; i < _trackerAbsoluteIndices.Count; i++)
		{
			if (_trackerAbsoluteIndices[i] == absoluteTrackIndex)
			{
				logicalCol += (polyphonyIndex * 3) + subColumn;
				return logicalCol;
			}
			logicalCol += _trackPolyphonies[i] * 3;
		}
		return 0;
	}

	private void GetLogicalColumnDetails(int logicalCol, out int absoluteTrackIndex, out int polyphonyIndex, out int subColumn)
	{
		int currentCol = 0;
		for (int i = 0; i < _trackerAbsoluteIndices.Count; i++)
		{
			int trackCols = _trackPolyphonies[i] * 3;
			if (logicalCol < currentCol + trackCols)
			{
				absoluteTrackIndex = _trackerAbsoluteIndices[i];
				int relativeCol = logicalCol - currentCol;
				polyphonyIndex = relativeCol / 3;
				subColumn = relativeCol % 3;
				return;
			}
			currentCol += trackCols;
		}

		absoluteTrackIndex = _trackerAbsoluteIndices.Count > 0 ? _trackerAbsoluteIndices.Last() : 0;
		polyphonyIndex = _trackerAbsoluteIndices.Count > 0 ? _trackPolyphonies.Last() - 1 : 0;
		subColumn = 2;
	}

	private void GetLogicalColumnXAndWidth(int logicalCol, out double x, out double width)
	{
		GetLogicalColumnDetails(logicalCol, out int absoluteTrackIndex, out int polyphonyIndex, out int subColumn);

		int trackCanvasIndex = -1;
		int trackVisualIndex = -1;
		for (int i = 0; i < _trackerAbsoluteIndices.Count; i++)
		{
			if (_trackerAbsoluteIndices[i] == absoluteTrackIndex)
			{
				trackVisualIndex = i;
				trackCanvasIndex = _trackerCanvasIndices[i];
				break;
			}
		}

		if (trackVisualIndex < 0)
		{
			x = 30.0; width = 22.0; return;
		}

		double trackX = (_trackXOffsets != null && trackVisualIndex < _trackXOffsets.Count) ? _trackXOffsets[trackVisualIndex] : (30 + trackVisualIndex * GetTrackColWidth(trackCanvasIndex));
		double colWidth = GetTrackColWidth(trackCanvasIndex);
		double polyX = trackX + (polyphonyIndex * colWidth);

		if (subColumn == 0) {
			x = polyX + 1.0; width = 23.0;
		} else if (subColumn == 1) {
			x = polyX + 25.0; width = 17.0;
		} else {
			x = polyX + 43.0; width = colWidth - 43.0;
		}
	}
	}
}
