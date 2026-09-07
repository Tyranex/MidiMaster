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
		private Dictionary<int, Dictionary<int, Tuple<string, Brush>>> _trackFxMap = new Dictionary<int, Dictionary<int, Tuple<string, Brush>>>();
	private double GetTrackColWidth(int index) => _trackShowFx != null && index < _trackShowFx.Count && _trackShowFx[index] ? 110.0 : 60.0;

	private int _activeTrackIndex;
	private int _activePolyphonyIndex;
	private int _activeSubColumn;
	private TextBlock _velocityEditOverlay;

	private int _trackerCursorRow;

	private List<Rectangle> _trackLeds;

	private List<double> _trackVuLevels;

	private List<List<Note>> _activeTrackNotes;
	private int? _trackerSelectionStartRow;
	private int? _trackerSelectionEndRow;
	private int? _trackerSelectionStartLogicalCol;
	private int? _trackerSelectionEndLogicalCol;
	private int _lastRenderedStartRow = -1;
	private int _lastRenderedEndRow = -1;

	// Slide data: Key = absolute track index, Value = dict of (row → (targetNote, durationBeats))
	private Dictionary<int, Dictionary<int, (int TargetNote, double DurationBeats)>> _slideData = new Dictionary<int, Dictionary<int, (int TargetNote, double DurationBeats)>>();

	private struct ClipboardCell
	{
		public int RowOffset;
		public int LogicalColOffset;
		public int Value;
	}

	private void UpdateTrackerCursor()
	{
		if (_velocityEditOverlay != null && _velocityEditOverlay.Visibility == Visibility.Visible)
		{
			_velocityEditOverlay.Visibility = Visibility.Collapsed;
			GenerateTrackerView();
		}

		if (!ViewTrackerMenu.IsChecked || _loadedMidi == null)
		{
			return;
		}

		int num = -1;
		for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
		{
			if (_trackerAbsoluteIndices[m] == _activeTrackIndex)
			{
				num = m;
				break;
			}
		}

		if (num < 0)
		{
			TrackerCursor.Visibility = Visibility.Collapsed;
			if (TrackerActiveColumnHighlight != null)
			{
				TrackerActiveColumnHighlight.Visibility = Visibility.Collapsed;
			}
			return;
		}

		TrackerCursor.Visibility = Visibility.Visible;

		double num2 = (_trackXOffsets != null && num < _trackXOffsets.Count) ? _trackXOffsets[num] : (30 + num * GetTrackColWidth(_trackerCanvasIndices[num]));
		double colWidth = GetTrackColWidth(_trackerCanvasIndices[num]);
		double highlightStart = num2 + (_activePolyphonyIndex * colWidth);

		double subHighlightStart = highlightStart;
		double subHighlightWidth = colWidth;
		
		double cursorStart = highlightStart;
		double cursorWidth = colWidth;
		
		// The main vertical line is at 0 (thickness 1). Grid lines are at 24 and 42 (thickness 1).
		// We want the cursor to fit perfectly BETWEEN the lines.
		if (_activeSubColumn == 0) {
			subHighlightWidth = 24.0;
			cursorStart += 1.0;
			cursorWidth = 23.0;
		} else if (_activeSubColumn == 1) {
			subHighlightStart += 24.0;
			subHighlightWidth = 18.0;
			cursorStart += 25.0;
			cursorWidth = 17.0;
		} else if (_activeSubColumn == 2) {
			subHighlightStart += 42.0;
			subHighlightWidth = colWidth - 42.0;
			cursorStart += 43.0;
			cursorWidth = colWidth - 43.0;
		}

		if (_trackerSelectionStartLogicalCol.HasValue && _trackerSelectionEndLogicalCol.HasValue)
		{
			int startCol = Math.Min(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);
			int endCol = Math.Max(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);
			GetLogicalColumnXAndWidth(startCol, out double startX, out double startW);
			GetLogicalColumnXAndWidth(endCol, out double endX, out double endW);
			cursorStart = startX;
			cursorWidth = (endX + endW) - startX;
			// For multi-selection, make the column highlight match the selection
			subHighlightStart = cursorStart;
			subHighlightWidth = cursorWidth;
		}

		TrackerCursor.Width = cursorWidth;

		if (TrackerActiveColumnHighlight != null)
		{
			TrackerActiveColumnHighlight.Visibility = Visibility.Visible;
			Canvas.SetLeft(TrackerActiveColumnHighlight, subHighlightStart);
			TrackerActiveColumnHighlight.Width = subHighlightWidth;
		}

		double top = _trackerCursorRow * 14;
		if (_trackerSelectionStartRow.HasValue && _trackerSelectionEndRow.HasValue)
		{
			int start = Math.Min(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
			int end = Math.Max(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value);
			top = start * 14.0;
			TrackerCursor.Height = (end - start + 1) * 14.0;
		}
		else
		{
			TrackerCursor.Height = 14.0;
		}
		TrackerCursor.Width = cursorWidth;
		Canvas.SetLeft(TrackerCursor, cursorStart);
		Canvas.SetTop(TrackerCursor, top);

		double verticalOffset = TrackerGridScrollViewer.VerticalOffset;
		double viewportHeight = TrackerGridScrollViewer.ViewportHeight;

		if (top < verticalOffset)
		{
			TrackerGridScrollViewer.ScrollToVerticalOffset(top);
		}
		else if (top + 14.0 > verticalOffset + viewportHeight)
		{
			TrackerGridScrollViewer.ScrollToVerticalOffset(top + 14.0 - viewportHeight);
		}

		double horizOffset = TrackerGridScrollViewer.HorizontalOffset;
		double viewportWidth = TrackerGridScrollViewer.ViewportWidth;
		if (viewportWidth > 0)
		{
			if (subHighlightStart < horizOffset + 30.0)
			{
				TrackerGridScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, subHighlightStart - 30.0));
			}
			else if (subHighlightStart + subHighlightWidth > horizOffset + viewportWidth)
			{
				TrackerGridScrollViewer.ScrollToHorizontalOffset(subHighlightStart + subHighlightWidth - viewportWidth + 30.0);
			}
		}
	}

	private void UpdateActiveTrackVisuals()
	{
		int num = -1;
		if (_loadedMidi != null)
		{
			for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
			{
				if (_trackerAbsoluteIndices[m] == _activeTrackIndex)
				{
					num = m;
					break;
				}
			}
		}
		for (int j = 0; j < TrackListPanel.Children.Count; j++)
		{
			if (TrackListPanel.Children[j] is Border border)
			{
				if (j == num)
				{
					border.BorderBrush = new SolidColorBrush(Colors.White);
					border.BorderThickness = new Thickness(2.0, 0.0, 0.0, 1.0);
				}
				else
				{
					border.BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66));
					border.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
				}
			}
		}
		UpdateTrackerCursor();
	}

	private void TrackerScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.VerticalChange != 0.0)
		{
			TrackerHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
			VerticalWaveformScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
			GenerateTrackerView(null, true); // Trigger viewport virtualization
		}
		if (e.HorizontalChange != 0.0)
		{
			TrackerHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
		}
	}

	private void TrackerGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point position = e.GetPosition(TrackerBackgroundCanvas);
		double num = Math.Floor(position.Y / 14.0);

		// First, update the active track, polyphony, and sub-column from click X coordinate
		if (position.X >= 30.0 && _loadedMidi != null)
		{
			for (int i = 0; i < _trackXOffsets.Count; i++)
			{
				double num3 = _trackXOffsets[i];
				double num4 = num3 + (double)(_trackPolyphonies[i] * GetTrackColWidth(_trackerCanvasIndices[i]));
				if (!(position.X >= num3) || !(position.X < num4))
				{
					continue;
				}
				_activeTrackIndex = _trackerAbsoluteIndices[i];
				int num5 = (int)Math.Floor((position.X - num3) / GetTrackColWidth(_trackerCanvasIndices[i]));
				_activePolyphonyIndex = num5;

				double num6 = num3 + (double)(num5 * GetTrackColWidth(_trackerCanvasIndices[i]));
				double relativeX = position.X - num6;
				if (relativeX < 24) _activeSubColumn = 0;
				else if (relativeX < 42) _activeSubColumn = 1;
				else _activeSubColumn = 2;

				break;
			}
		}

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

		_trackerCursorRow = Math.Max(0, (int)num);

		if (isShift) {
			_trackerSelectionEndRow = _trackerCursorRow;
			_trackerSelectionEndLogicalCol = logicalCol;
		}
		else {
			_trackerSelectionStartRow = _trackerCursorRow;
			_trackerSelectionStartLogicalCol = logicalCol;
			_trackerSelectionEndRow = _trackerCursorRow;
			_trackerSelectionEndLogicalCol = logicalCol;
		}

		_isDraggingTrackerSelection = true;
		TrackerContentCanvas.CaptureMouse();

		double num2 = num / 4.0;
		double playheadPosition = num2 * (double)BeatWidth;
		SetPlayheadPosition(playheadPosition);
		if (isPlaying)
		{
			double seconds = num2 / 2.0;
			_audioEngine.Seek(seconds);
		}

		UpdateActiveTrackVisuals();
		e.Handled = true;
	}

	private void TrackerGrid_MouseMove(object sender, MouseEventArgs e)
	{
		if (_isDraggingTrackerSelection)
		{
			Point position = e.GetPosition(TrackerBackgroundCanvas);
			double num = Math.Floor(position.Y / 14.0);

			if (position.X >= 30.0 && _loadedMidi != null)
			{
				for (int i = 0; i < _trackXOffsets.Count; i++)
				{
					double num3 = _trackXOffsets[i];
					double num4 = num3 + (double)(_trackPolyphonies[i] * GetTrackColWidth(_trackerCanvasIndices[i]));
					if (!(position.X >= num3) || !(position.X < num4))
					{
						continue;
					}
					_activeTrackIndex = _trackerAbsoluteIndices[i];
					int num5 = (int)Math.Floor((position.X - num3) / GetTrackColWidth(_trackerCanvasIndices[i]));
					_activePolyphonyIndex = num5;

					double num6 = num3 + (double)(num5 * GetTrackColWidth(_trackerCanvasIndices[i]));
					double relativeX = position.X - num6;
					if (relativeX < 24) _activeSubColumn = 0;
					else if (relativeX < 42) _activeSubColumn = 1;
					else _activeSubColumn = 2;

					break;
				}
			}

			int newRow = Math.Max(0, (int)num);
			int newLogicalCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);

			if (newRow != _trackerCursorRow || newLogicalCol != _trackerSelectionEndLogicalCol)
			{
				_trackerCursorRow = newRow;
				_trackerSelectionEndRow = _trackerCursorRow;
				_trackerSelectionEndLogicalCol = newLogicalCol;
				SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
				UpdateActiveTrackVisuals();
			}
			return;
		}

		if (_isDraggingVelocity && _currentVelocityNote != null)
		{
			Point position = e.GetPosition(TrackerBackgroundCanvas);
			double num = _velocityDragStartPoint.Y - position.Y;
			int value = _velocityDragStartValue + (int)Math.Round(num / 2.0);
			value = Math.Clamp(value, 0, 127);
			if ((byte)_currentVelocityNote.Velocity != value)
			{
				_currentVelocityNote.Velocity = (SevenBitNumber)(byte)value;

				if (_loadedMidi != null && _activeTrackIndex >= 0 && _activeTrackIndex < _loadedMidi.GetTrackChunks().Count())
				{
					var chunk = _loadedMidi.GetTrackChunks().ElementAt(_activeTrackIndex);
					using (var notesManager = chunk.ManageNotes())
					{
						var noteToUpdate = notesManager.Objects.FirstOrDefault(n => n.Time == _currentVelocityNote.Time && n.NoteNumber == _currentVelocityNote.NoteNumber);
						if (noteToUpdate != null)
						{
							noteToUpdate.Velocity = (SevenBitNumber)(byte)value;
						}
					}
				}

				BuildNoteCache();
				GenerateTrackerView();
			}
		}
	}

	private void TrackerGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_isDraggingTrackerSelection)
		{
			_isDraggingTrackerSelection = false;
			TrackerContentCanvas.ReleaseMouseCapture();
		}
		if (_isDraggingVelocity)
		{
			_isDraggingVelocity = false;
			_currentVelocityNote = null;
			TrackerContentCanvas.ReleaseMouseCapture();
		}
	}

	private void TrackerGrid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point position = e.GetPosition(TrackerBackgroundCanvas);
		double num = Math.Floor(position.Y / 14.0);

		// Update cursor / position if clicking outside active selection
		if (position.X >= 30.0 && _loadedMidi != null)
		{
			for (int i = 0; i < _trackXOffsets.Count; i++)
			{
				double num3 = _trackXOffsets[i];
				double num4 = num3 + (double)(_trackPolyphonies[i] * GetTrackColWidth(_trackerCanvasIndices[i]));
				if (!(position.X >= num3) || !(position.X < num4))
				{
					continue;
				}
				_activeTrackIndex = _trackerAbsoluteIndices[i];
				int num5 = (int)Math.Floor((position.X - num3) / GetTrackColWidth(_trackerCanvasIndices[i]));
				_activePolyphonyIndex = num5;

				double num6 = num3 + (double)(num5 * GetTrackColWidth(_trackerCanvasIndices[i]));
				double relativeX = position.X - num6;
				if (relativeX < 24) _activeSubColumn = 0;
				else if (relativeX < 42) _activeSubColumn = 1;
				else _activeSubColumn = 2;

				break;
			}

			int clickedRow = Math.Max(0, (int)num);
			int clickedCol = GetLogicalColumnIndex(_activeTrackIndex, _activePolyphonyIndex, _activeSubColumn);

			bool insideSelection = _trackerSelectionStartRow.HasValue && _trackerSelectionEndRow.HasValue &&
			                       _trackerSelectionStartLogicalCol.HasValue && _trackerSelectionEndLogicalCol.HasValue &&
			                       clickedRow >= Math.Min(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value) &&
			                       clickedRow <= Math.Max(_trackerSelectionStartRow.Value, _trackerSelectionEndRow.Value) &&
			                       clickedCol >= Math.Min(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value) &&
			                       clickedCol <= Math.Max(_trackerSelectionStartLogicalCol.Value, _trackerSelectionEndLogicalCol.Value);

			if (!insideSelection)
			{
				_trackerCursorRow = clickedRow;
				_trackerSelectionStartRow = _trackerSelectionEndRow = clickedRow;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = clickedCol;
				SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
				UpdateActiveTrackVisuals();
			}
		}

		ContextMenu menu = new ContextMenu();
		if (TryFindResource("DarkContextMenuStyle") is Style ctxStyle)
		{
			menu.Style = ctxStyle;
		}
		if (TryFindResource("DarkContextMenuItemStyle") is Style itemStyle)
		{
			menu.Resources.Add(typeof(MenuItem), itemStyle);
		}
		if (TryFindResource("DarkContextMenuSeparatorStyle") is Style sepStyle)
		{
			menu.Resources.Add(typeof(Separator), sepStyle);
		}

		bool hasSelection = _trackerSelectionStartRow.HasValue && _trackerSelectionEndRow.HasValue &&
		                    _trackerSelectionStartLogicalCol.HasValue && _trackerSelectionEndLogicalCol.HasValue;
		bool hasClipboard = _trackerClipboardCells != null && _trackerClipboardCells.Count > 0;

		MenuItem cutItem = new MenuItem
		{
			Header = "Cut",
			InputGestureText = "Ctrl+X",
			IsEnabled = hasSelection
		};
		cutItem.Click += (s, ev) => CutTrackerSelection();
		menu.Items.Add(cutItem);

		MenuItem copyItem = new MenuItem
		{
			Header = "Copy",
			InputGestureText = "Ctrl+C",
			IsEnabled = hasSelection
		};
		copyItem.Click += (s, ev) => CopyTrackerSelection();
		menu.Items.Add(copyItem);

		MenuItem pasteItem = new MenuItem
		{
			Header = "Paste",
			InputGestureText = "Ctrl+V",
			IsEnabled = hasClipboard
		};
		pasteItem.Click += (s, ev) => PasteTrackerSelection();
		menu.Items.Add(pasteItem);

		menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)), Margin = new Thickness(4, 2, 4, 2) });

		MenuItem deleteItem = new MenuItem
		{
			Header = "Delete",
			InputGestureText = "Del",
			IsEnabled = hasSelection
		};
		deleteItem.Click += (s, ev) => DeleteTrackerSelection();
		menu.Items.Add(deleteItem);

		// Only show Functions submenu if specifically right clicking on a function/FX column (_activeSubColumn == 2)
		if (_activeSubColumn == 2)
		{
			menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)), Margin = new Thickness(4, 2, 4, 2) });

			MenuItem functionsSubMenu = new MenuItem
			{
				Header = "Functions"
			};

			MenuItem timeSigItem = new MenuItem
			{
				Header = "Change Time Signature Here..."
			};
			timeSigItem.Click += (s, ev) =>
			{
				TimeSignatureButton_Click(this, new RoutedEventArgs());
			};
			functionsSubMenu.Items.Add(timeSigItem);

			MenuItem bpmItem = new MenuItem
			{
				Header = "Change Tempo (BPM) Here..."
			};
			bpmItem.Click += (s, ev) =>
			{
				OpenBpmEditDialog(defaultAtCursor: true);
			};
			functionsSubMenu.Items.Add(bpmItem);

			MenuItem vibratoItem = new MenuItem
			{
				Header = "Edit Vibrato Here..."
			};
			vibratoItem.Click += (s, ev) =>
			{
				OpenVibratoEditDialog(_activeTrackIndex, _trackerCursorRow);
			};
			functionsSubMenu.Items.Add(vibratoItem);

			MenuItem slideItem = new MenuItem
			{
				Header = "Edit Note Slide Here..."
			};
			slideItem.Click += (s, ev) =>
			{
				OpenSlideEditDialog(_activeTrackIndex, _trackerCursorRow);
			};
			functionsSubMenu.Items.Add(slideItem);

			menu.Items.Add(functionsSubMenu);
		}

		menu.IsOpen = true;
		e.Handled = true;
	}

	private void GenerateTrackerView(int? targetTrackIdx = null, bool isScrollUpdate = false)
	{
		if (_loadedMidi == null)
		{
			return;
		}

		if (targetTrackIdx == null)
		{
			TrackerHeaderCanvas.Children.Clear();
			TrackerBackgroundCanvas.Children.Clear();
			TrackerContentCanvas.Children.Clear();
			_trackerVisualHosts.Clear();
			_trackLeds.Clear();
			_trackVuLevels.Clear();
			_activeTrackNotes.Clear();
			_trackXOffsets.Clear();
			_trackPolyphonies.Clear();
			_trackerAbsoluteIndices.Clear();
			_trackerCanvasIndices.Clear();
			_lastRenderedStartRow = -1;
			_lastRenderedEndRow = -1;
		}
		
		List<TrackChunk> list = _loadedMidi.GetTrackChunks().ToList();
		_ticksPerQuarterNote = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNoteTimeDivision)
		{
			_ticksPerQuarterNote = ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote;
		}

		if (targetTrackIdx == null)
		{
			int canvasIdx = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (i < _cachedNotes.Count)
				{
					if (canvasIdx < _trackCanvases.Count && _trackCanvases[canvasIdx].Visibility == Visibility.Visible)
					{
						_trackerAbsoluteIndices.Add(i);
						_trackerCanvasIndices.Add(canvasIdx);
					}
					canvasIdx++;
				}
			}
		}

		// Map FX commands across tracks: Time Signatures and BPM Changes
		// If a row already has a command on track 0, place it on the next available track and auto-expand its FX column
		double num = 0.0;
		double num3 = 30.0;
		double num10 = 0.0;
		double num11 = 0.0;
		bool polyphonyChanged = false;

		_trackFxMap.Clear();

		if (isScrollUpdate && _trackXOffsets.Count > 0)
		{
			num10 = TrackerBackgroundCanvas.Height;
			num11 = TrackerBackgroundCanvas.Width;
			goto RenderPhase;
		}

		void AddFxCommand(int row, string text, Brush brush)
		{
			if (_trackerCanvasIndices.Count == 0) return;
			for (int trkIdx = 0; trkIdx < _trackerCanvasIndices.Count; trkIdx++)
			{
				if (!_trackFxMap.ContainsKey(trkIdx)) _trackFxMap[trkIdx] = new Dictionary<int, Tuple<string, Brush>>();
				if (!_trackFxMap[trkIdx].ContainsKey(row))
				{
					_trackFxMap[trkIdx][row] = Tuple.Create(text, brush);
					// Ensure this track's FX column is visible on layout passes
					if (targetTrackIdx == null)
					{
						int cIdx = _trackerCanvasIndices[trkIdx];
						while (_trackShowFx.Count <= cIdx) _trackShowFx.Add(false);
						_trackShowFx[cIdx] = true;
					}
					break;
				}
			}
		}

		if (_timeSignatures != null)
		{
			foreach (var ts in _timeSignatures)
			{
				if (ts.MetricTime == 0 && _timeSignatures.Count == 1) continue; // Don't show redundant default 4/4 at row 0 if no changes
				int tsRow = (int)Math.Round(ts.BeatTime * 4.0);
				char numChar = ts.Numerator < 10 ? (char)('0' + ts.Numerator) : (char)('A' + (ts.Numerator - 10));
				char denChar = ts.Denominator < 10 ? (char)('0' + ts.Denominator) : (char)('A' + (ts.Denominator - 10));
				AddFxCommand(tsRow, $"T{numChar}{denChar}", Brushes.Cyan);
			}
		}

		if (_tempoChanges != null)
		{
			foreach (var tc in _tempoChanges)
			{
				if (tc.MetricTime == 0 && _tempoChanges.Count == 1) continue; // Don't show redundant default at row 0 if no changes
				int tcRow = (int)Math.Round(tc.BeatTime * 4.0);
				int bpmVal = Math.Clamp(tc.Bpm, 0, 255);
				string bpmHex = bpmVal.ToString("X2");
				AddFxCommand(tcRow, $"B{bpmHex}", Brushes.Yellow);
			}
		}


		
		for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
		{
			int i = _trackerAbsoluteIndices[m];
			int num2 = _trackerCanvasIndices[m];
			ICollection<Note> notes = _cachedNotes[i];
			HashSet<double> hashSet = new HashSet<double>();
			Dictionary<double, int> dictionary = new Dictionary<double, int>();
			int num4 = 1;
			foreach (Note item in notes)
			{
				double num5 = (double)item.Time / (double)_ticksPerQuarterNote;
				double num6 = Math.Round(num5 * 4.0);
				hashSet.Add(num6);
				if (!dictionary.ContainsKey(num6))
				{
					dictionary[num6] = 0;
				}
				dictionary[num6]++;
				if (dictionary[num6] > num4)
				{
					num4 = dictionary[num6];
				}
			}
			while (_trackPolyphonyOffsets.Count <= i) _trackPolyphonyOffsets.Add(0);
			num4 += _trackPolyphonyOffsets[i];
			
			foreach (Note item in notes)
			{
				double num7 = (double)item.EndTime / (double)_ticksPerQuarterNote;
				if (num7 > num)
				{
					num = num7;
				}
			}

			long absTime = 0;
			foreach (var e in list[i].Events)
			{
				absTime += e.DeltaTime;
				if (e is ControlChangeEvent cce && cce.ControlNumber == 1) // Modulation / Vibrato
				{
					double bTime = (double)absTime / (double)_ticksPerQuarterNote;
					int row = (int)Math.Round(bTime * 4.0);
					if (!_trackFxMap.ContainsKey(m)) _trackFxMap[m] = new Dictionary<int, Tuple<string, Brush>>();
					_trackFxMap[m][row] = Tuple.Create<string, Brush>($"M{cce.ControlValue:X2}", Brushes.Magenta);

					if (targetTrackIdx == null)
					{
						while (_trackShowFx.Count <= num2) _trackShowFx.Add(false);
						_trackShowFx[num2] = true;
					}
				}
			}

			// Detect pitch bend slides and portamento (CC65) → convert to Sxx FX commands
			{
				var pitchBendEvents = new List<(long Tick, int Value)>();
				var portamentoNotes = new List<(long Tick, int NoteNum, bool PortOn)>(); // notes with portamento active
				bool portamentoOn = false;
				long absTime2 = 0;
				int activeBendRange = 2; // Default ±2 semitones

				foreach (var ev in list[i].Events)
				{
					absTime2 += ev.DeltaTime;
					if (ev is PitchBendEvent pbe)
					{
						pitchBendEvents.Add((absTime2, (int)pbe.PitchValue));
					}
					else if (ev is ControlChangeEvent cc2)
					{
						if (cc2.ControlNumber == 65) // Portamento On/Off
						{
							portamentoOn = cc2.ControlValue >= 64;
						}
						else if (cc2.ControlNumber == 6) // Data Entry MSB (used by RPN for bend range)
						{
							activeBendRange = Math.Max(1, (int)cc2.ControlValue);
						}
					}
					else if (ev is NoteOnEvent non && non.Velocity > 0 && portamentoOn)
					{
						portamentoNotes.Add((absTime2, (int)non.NoteNumber, true));
					}
				}

				if (i < _cachedNotes.Count)
				{
					// --- Method 1: Detect pitch bend ramps near notes ---
					if (pitchBendEvents.Count >= 2)
					{
						foreach (var note in _cachedNotes[i])
						{
							long noteTick = note.Time;
							int noteRow = (int)Math.Round(((double)noteTick / _ticksPerQuarterNote) * 4.0);
							long tolerance = _ticksPerQuarterNote / 4; // 1/4 beat tolerance

							// Find pitch bend events near/within this note's duration (with tolerance)
							var bendsDuringNote = pitchBendEvents
								.Where(pb => pb.Tick >= (noteTick - tolerance) && pb.Tick <= (note.EndTime + tolerance))
								.OrderBy(pb => pb.Tick)
								.ToList();

							if (bendsDuringNote.Count >= 2)
							{
								int firstBend = bendsDuringNote.First().Value;
								int lastBend = bendsDuringNote.Last().Value;

								// Find the bend that deviates the most from center (8192)
								int maxDeviationBend = bendsDuringNote.OrderByDescending(b => Math.Abs(b.Value - 8192)).First().Value;
								int bendToUse = Math.Abs(lastBend - 8192) >= Math.Abs(maxDeviationBend - 8192) ? lastBend : maxDeviationBend;

								// Require the bend to be significant (at least ~quarter of a semitone)
								if (Math.Abs(bendToUse - 8192) > 200)
								{
									// Calculate target note from bend amount
									double bendSemitones = ((bendToUse - 8192.0) / 8192.0) * activeBendRange;
									int targetNote = (int)Math.Round((int)note.NoteNumber + bendSemitones);
									targetNote = Math.Clamp(targetNote, 0, 127);

									// Only create slide if target differs from source
									if (targetNote != (int)note.NoteNumber)
									{
										double durationBeats = (double)(bendsDuringNote.Last().Tick - Math.Max(noteTick, bendsDuringNote.First().Tick)) / _ticksPerQuarterNote;
										if (durationBeats < 0.0625) durationBeats = (double)note.Length / _ticksPerQuarterNote;

										// Store in slide data
										if (!_slideData.ContainsKey(i))
											_slideData[i] = new Dictionary<int, (int, double)>();
										_slideData[i][noteRow] = (targetNote, durationBeats);

										// Add to FX map
										if (!_trackFxMap.ContainsKey(m)) _trackFxMap[m] = new Dictionary<int, Tuple<string, Brush>>();
										if (!_trackFxMap[m].ContainsKey(noteRow))
										{
											string slideDurStr = durationBeats < 1.0 ? durationBeats.ToString("F1") : ((int)Math.Round(durationBeats)).ToString();
											_trackFxMap[m][noteRow] = Tuple.Create<string, Brush>($"{GetNoteName(targetNote)}{slideDurStr}", Brushes.LimeGreen);
										}

										if (targetTrackIdx == null)
										{
											while (_trackShowFx.Count <= num2) _trackShowFx.Add(false);
											_trackShowFx[num2] = true;
										}
									}
								}
							}
						}
					}

					// --- Method 2: Detect portamento (CC65) note-to-note slides ---
					// If portamento was active, look for consecutive notes and treat them as slides
					if (portamentoNotes.Count >= 2)
					{
						var sortedNotes = _cachedNotes[i].OrderBy(n => n.Time).ToList();
						for (int ni = 0; ni < sortedNotes.Count - 1; ni++)
						{
							var curNote = sortedNotes[ni];
							var nextNote = sortedNotes[ni + 1];
							long curTick = curNote.Time;
							int curRow = (int)Math.Round(((double)curTick / _ticksPerQuarterNote) * 4.0);

							// Check if portamento was active during this note
							bool portaActive = portamentoNotes.Any(pn => pn.Tick >= curTick && pn.Tick <= nextNote.Time);
							if (portaActive && (int)curNote.NoteNumber != (int)nextNote.NoteNumber)
							{
								int targetNote = (int)nextNote.NoteNumber;
								double durationBeats = (double)curNote.Length / _ticksPerQuarterNote;

								if (!_slideData.ContainsKey(i))
									_slideData[i] = new Dictionary<int, (int, double)>();
								if (!_slideData[i].ContainsKey(curRow))
								{
									_slideData[i][curRow] = (targetNote, durationBeats);

									if (!_trackFxMap.ContainsKey(m)) _trackFxMap[m] = new Dictionary<int, Tuple<string, Brush>>();
									if (!_trackFxMap[m].ContainsKey(curRow))
									{
										string portaDurStr = durationBeats < 1.0 ? durationBeats.ToString("F1") : ((int)Math.Round(durationBeats)).ToString();
										_trackFxMap[m][curRow] = Tuple.Create<string, Brush>($"{GetNoteName(targetNote)}{portaDurStr}", Brushes.LimeGreen);
									}

									if (targetTrackIdx == null)
									{
										while (_trackShowFx.Count <= num2) _trackShowFx.Add(false);
										_trackShowFx[num2] = true;
									}
								}
							}
						}
					}
				}
			}
			
			if (targetTrackIdx != null && i == targetTrackIdx.Value)
			{
				if (_trackPolyphonies[m] != num4)
				{
					polyphonyChanged = true;
					break; // Polyphony changed, we must abort the targeted render and do a full render
				}
			}

			if (targetTrackIdx == null)
			{
				_trackXOffsets.Add(num3);
				_trackPolyphonies.Add(num4);
				double num8 = GetTrackColWidth(num2) * num4;
				string text = $"Track {i + 1}";
				SequenceTrackNameEvent sequenceTrackNameEvent = list[i].Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
				if (sequenceTrackNameEvent != null && !string.IsNullOrWhiteSpace(sequenceTrackNameEvent.Text))
				{
					text = sequenceTrackNameEvent.Text;
				}
				double num9 = num3;
				
				Color trkColor = _trackColors.Count > i ? _trackColors[i] : Colors.Gray;
				Rectangle headerBg = new Rectangle
				{
					Width = num8,
					Height = 45,
					Fill = new SolidColorBrush(Color.FromArgb(64, trkColor.R, trkColor.G, trkColor.B))
				};
				Canvas.SetLeft(headerBg, num9);
				Canvas.SetTop(headerBg, 0);
				TrackerHeaderCanvas.Children.Add(headerBg);

				Rectangle rightBorder = new Rectangle
				{
					Width = 1,
					Height = 45,
					Fill = new SolidColorBrush(Color.FromRgb(62, 62, 66))
				};
				Canvas.SetLeft(rightBorder, num9 + num8 - 1);
				Canvas.SetTop(rightBorder, 0);
				TrackerHeaderCanvas.Children.Add(rightBorder);

				TextBlock element = new TextBlock
				{
					Text = text,
					Foreground = Brushes.LightGray,
					FontWeight = FontWeights.Bold,
					Width = num8 - 10.0,
					TextTrimming = TextTrimming.CharacterEllipsis,
					TextAlignment = TextAlignment.Center
				};
				Canvas.SetLeft(element, num9 + 5.0);
				Canvas.SetTop(element, 2.0);
				TrackerHeaderCanvas.Children.Add(element);

				bool fxActive = _trackShowFx.Count > num2 && _trackShowFx[num2];
				Button fxBtn = new Button
				{
					Content = "FX",
					Foreground = fxActive ? Brushes.White : Brushes.LightGray,
					Background = fxActive ? new SolidColorBrush(Color.FromRgb(0, 122, 204)) : new SolidColorBrush(Color.FromRgb(62, 62, 66)),
					BorderThickness = new Thickness(0),
					Padding = new Thickness(4, 0, 4, 0),
					FontSize = 10,
					Cursor = Cursors.Hand,
					Height = 16,
					Style = (Style)FindResource("SimpleFlatButton"),
					Margin = new Thickness(1, 0, 1, 0)
				};
				int trackIndexForToggle = num2;
				fxBtn.Click += (s, e) => {
					_trackShowFx[trackIndexForToggle] = !_trackShowFx[trackIndexForToggle];
					GenerateTrackerView();
				};

				int trackAbsoluteIdx = i; // The actual index of the track in the midi file
				Button addRowBtn = new Button
				{
					Content = "+",
					Foreground = Brushes.LightGray,
					Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
					BorderThickness = new Thickness(0),
					Padding = new Thickness(4, 0, 4, 0),
					FontSize = 10,
					Cursor = Cursors.Hand,
					Height = 16,
					Style = (Style)FindResource("SimpleFlatButton"),
					Margin = new Thickness(1, 0, 1, 0)
				};
				addRowBtn.Click += (s, e) => {
					_trackPolyphonyOffsets[trackAbsoluteIdx]++;
					GenerateTrackerView();
				};

				Button removeRowBtn = new Button
				{
					Content = "-",
					Foreground = Brushes.LightGray,
					Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
					BorderThickness = new Thickness(0),
					Padding = new Thickness(4, 0, 4, 0),
					FontSize = 10,
					Cursor = Cursors.Hand,
					Height = 16,
					Style = (Style)FindResource("SimpleFlatButton"),
					Margin = new Thickness(1, 0, 1, 0)
				};
				removeRowBtn.Click += (s, e) => {
					if (_trackPolyphonyOffsets[trackAbsoluteIdx] > 0) {
						_trackPolyphonyOffsets[trackAbsoluteIdx]--;
						GenerateTrackerView();
					} else {
					    SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Cannot remove this note row, as it contains note data.", "Cannot Remove Row", MessageBoxButton.OK, MessageBoxImage.Warning);
					}
				};

				StackPanel btnPanel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					HorizontalAlignment = HorizontalAlignment.Center
				};
				btnPanel.Children.Add(fxBtn);
				btnPanel.Children.Add(addRowBtn);
				btnPanel.Children.Add(removeRowBtn);

				Grid btnGrid = new Grid { Width = num8 };
				btnGrid.Children.Add(btnPanel);
				Canvas.SetLeft(btnGrid, num9);
				Canvas.SetTop(btnGrid, 20.0);
				TrackerHeaderCanvas.Children.Add(btnGrid);

				Rectangle element2 = new Rectangle
				{
					Width = num8 - 20.0,
					Height = 6.0,
					Fill = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
					RadiusX = 2.0,
					RadiusY = 2.0
				};
				Canvas.SetLeft(element2, num9 + 10.0);
				Canvas.SetTop(element2, 38.0);
				TrackerHeaderCanvas.Children.Add(element2);
				
				Rectangle rectangle = new Rectangle
				{
					Width = 0.0,
					MaxWidth = Math.Max(0.0, num8 - 20.0),
					Height = 6.0,
					Fill = Brushes.LimeGreen,
					RadiusX = 2.0,
					RadiusY = 2.0
				};
				Canvas.SetLeft(rectangle, num9 + 10.0);
				Canvas.SetTop(rectangle, 38.0);
				TrackerHeaderCanvas.Children.Add(rectangle);
				_trackLeds.Add(rectangle);
				_trackVuLevels.Add(0.0);
				_activeTrackNotes.Add(notes.ToList());
				num3 += num8;
			}
		}

		if (polyphonyChanged)
		{
			// A targeted render failed because track width changed. Fall back to full render.
			GenerateTrackerView(null);
			return;
		}

		if (targetTrackIdx == null)
		{
			num10 = Math.Max(100.0, num + 4.0) * 4.0 * 14.0;
			num11 = num3;
			TrackerHeaderCanvas.Width = num11;
			TrackerBackgroundCanvas.Width = num11;
			TrackerBackgroundCanvas.Height = num10;
			TrackerContentCanvas.Width = num11;
			TrackerContentCanvas.Height = num10;
		}

		RenderPhase:
		double scrollY = TrackerGridScrollViewer.VerticalOffset;
		double viewportH = TrackerGridScrollViewer.ViewportHeight;
		if (viewportH == 0) viewportH = 1000;
		int visibleStart = (int)(scrollY / 14.0);
		int visibleEnd = (int)((scrollY + viewportH) / 14.0);

		// If this is a scroll update (targetTrackIdx == null and not a forced re-layout)
		// and the current visible range is safely inside our rendered buffer, skip expensive text re-drawing
		if (targetTrackIdx == null && _trackerVisualHosts.Count > 0 && 
		    visibleStart >= _lastRenderedStartRow + 5 && visibleEnd <= _lastRenderedEndRow - 5)
		{
			return;
		}

		int startRow = Math.Max(0, visibleStart - 30);
		int endRow = visibleEnd + 30;
		_lastRenderedStartRow = startRow;
		_lastRenderedEndRow = endRow;

		Typeface typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		
		for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
		{
			int j = _trackerAbsoluteIndices[m];
			if (targetTrackIdx != null && j != targetTrackIdx.Value) continue;

			int num12 = _trackerCanvasIndices[m];
			ICollection<Note> notes2 = _cachedNotes[j];
			double num13 = _trackXOffsets[m];
			int num14 = _trackPolyphonies[m];
			HashSet<double> hashSet2 = new HashSet<double>();
			Dictionary<double, int> dictionary2 = new Dictionary<double, int>();
			
			if (!_trackerVisualHosts.ContainsKey(j))
			{
				_trackerVisualHosts[j] = new VisualHost();
				Canvas.SetLeft(_trackerVisualHosts[j], 0.0);
				Canvas.SetTop(_trackerVisualHosts[j], 0.0);
				TrackerContentCanvas.Children.Add(_trackerVisualHosts[j]);
			}
			VisualHost trackVisualHost = _trackerVisualHosts[j];
			DrawingVisual trackDrawingVisual = new DrawingVisual();

			using (DrawingContext drawingContext = trackDrawingVisual.RenderOpen())
			{
				long startTick = (long)((startRow / 4.0) * _ticksPerQuarterNote);
				long endTick = (long)(((endRow + 16) / 4.0) * _ticksPerQuarterNote);
				var visibleNotes = notes2.Where(n => n.Time <= endTick && n.EndTime >= startTick).ToList();
				
				foreach (Note item2 in visibleNotes)
				{
					double num15 = (double)item2.Time / (double)_ticksPerQuarterNote;
					hashSet2.Add(Math.Round(num15 * 4.0));
				}
				foreach (Note item3 in visibleNotes)
				{
					double num16 = (double)item3.Time / (double)_ticksPerQuarterNote;
					double num17 = Math.Round(num16 * 4.0);
					
					double num21 = (double)item3.EndTime / (double)_ticksPerQuarterNote;
					double num22 = Math.Round(num21 * 4.0);
					if (num22 <= num17)
					{
						num22 = num17 + 1.0;
					}

					if (!dictionary2.ContainsKey(num17))
					{
						dictionary2[num17] = 0;
					}
					int num19 = dictionary2[num17];
					dictionary2[num17]++;

					if (num17 > endRow || num22 < startRow)
					{
						continue;
					}

					double num18 = num17 * 14.0;
					double num20 = num13 + (double)(num19 * GetTrackColWidth(num12));
					
					if (num17 >= startRow && num17 <= endRow)
					{
						bool isPerc = IsPercussionTrack(j);
						string textToFormat = isPerc ? MidiDrumToTrackerText((byte)item3.NoteNumber).PadRight(3) : GetNoteName((byte)item3.NoteNumber).PadRight(3);
						string textToFormat2 = ((byte)item3.Velocity).ToString("X2");
						Brush noteBrush = isPerc ? Brushes.Orange : Brushes.White;
						FormattedText formattedText = new FormattedText(textToFormat, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12.0, noteBrush);
						FormattedText formattedText2 = new FormattedText(textToFormat2, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12.0, Brushes.LightSkyBlue);
						
						string fxTextStr = null;
						Brush fxBrush = Brushes.Cyan;
						if (num19 == 0 && _trackFxMap.TryGetValue(m, out var mFxDict) && mFxDict.TryGetValue((int)num17, out var fxTuple))
						{
							fxTextStr = fxTuple.Item1;
							fxBrush = fxTuple.Item2;
						}

						drawingContext.DrawText(formattedText, new Point(num20 + 2.0, num18));
						drawingContext.DrawText(formattedText2, new Point(num20 + 26.0, num18));
						if (!string.IsNullOrEmpty(fxTextStr) && _trackShowFx != null && num12 < _trackShowFx.Count && _trackShowFx[num12])
						{
							FormattedText formattedText3 = new FormattedText(fxTextStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12.0, fxBrush);
							drawingContext.DrawText(formattedText3, new Point(num20 + 44.0, num18));
						}
					}
					
					if (num22 >= startRow && num22 <= endRow)
					{
						double num23 = num22 * 14.0;
						FormattedText formattedText4 = new FormattedText("===", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12.0, Brushes.Gray);
						drawingContext.DrawText(formattedText4, new Point(num20 + 2.0, num23));
					}
				}

				// If there are standalone FX changes (Time Signature or BPM) on empty rows on this track, render them in the FX column too
				if (_trackFxMap.TryGetValue(m, out var currentTrackFx))
				{
					foreach (var kvp in currentTrackFx)
					{
						int fxRow = kvp.Key;
						if (fxRow >= startRow && fxRow <= endRow && !hashSet2.Contains(fxRow))
						{
							double fxY = fxRow * 14.0;
							string fxTextStr = kvp.Value.Item1;
							Brush fxBrush = kvp.Value.Item2;
							FormattedText formattedText3 = new FormattedText(fxTextStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12.0, fxBrush);
							if (_trackShowFx != null && num12 < _trackShowFx.Count && _trackShowFx[num12])
							{
								drawingContext.DrawText(formattedText3, new Point(num13 + 44.0, fxY));
							}
						}
					}
				}
			}
			trackVisualHost.Visual = trackDrawingVisual;
		}

		if (targetTrackIdx != null) return; // If we only targeted one track, we don't redraw the background grid

		DrawingVisual bgDrawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext2 = bgDrawingVisual.RenderOpen())
		{
			// Fill entire background canvas
			drawingContext2.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, new Rect(0.0, 0.0, num11, num10));
			Typeface typeface2 = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			Typeface typeface3 = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

			int totalRows = (int)Math.Ceiling(num10 / 14.0);
			int drawStart = startRow;
			int drawEnd = Math.Min(totalRows, endRow);

			int curMeasureStartRow = 0;
			int curMeasureRowCount = 16;
			GetMeasureBoundsForRow(drawStart, out curMeasureStartRow, out curMeasureRowCount);

			for (int l = drawStart; l < drawEnd; l++)
			{
				if (l >= curMeasureStartRow + curMeasureRowCount)
				{
					GetMeasureBoundsForRow(l, out curMeasureStartRow, out curMeasureRowCount);
				}

				bool isMeasureStart = (l == curMeasureStartRow);
				int rowInMeasure = l - curMeasureStartRow;
				bool isBeatStart = (rowInMeasure % 4 == 0);
				double num27 = l * 14.0;

				if (isBeatStart)
				{
					SolidColorBrush brush = new SolidColorBrush(isMeasureStart ? Color.FromArgb(20, byte.MaxValue, byte.MaxValue, byte.MaxValue) : Color.FromArgb(10, byte.MaxValue, byte.MaxValue, byte.MaxValue));
					drawingContext2.DrawRectangle(brush, null, new Rect(0.0, num27, num11, 14.0));
				}

				if (PreferencesManager.Current.ShowGridLines || isMeasureStart)
				{
					Pen pen = new Pen(new SolidColorBrush(isMeasureStart ? Color.FromRgb(120, 120, 120) : (isBeatStart ? Color.FromRgb(70, 70, 70) : Color.FromRgb(40, 40, 40))), isMeasureStart ? 2 : 1);
					drawingContext2.DrawLine(pen, new Point(0.0, num27), new Point(num11, num27));
				}

				// Check if there is a time signature marker at this row
				var tsAtRow = GetTimeSignatureAtRow(l);
				int tsStartRow = (int)Math.Round(tsAtRow.BeatTime * 4.0);
				bool hasTsChangeHere = (l == tsStartRow && l > 0);

				SolidColorBrush foreground = isMeasureStart ? Brushes.White : Brushes.Gray;
				Typeface typeface4 = isMeasureStart ? typeface3 : typeface2;

				if (hasTsChangeHere)
				{
					// Draw TS indicator tag
					FormattedText tsText = new FormattedText($"{tsAtRow.Numerator}/{tsAtRow.Denominator}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface3, 10.0, Brushes.Cyan);
					drawingContext2.DrawText(tsText, new Point(2.0, num27 + 1.0));
				}
				else
				{
					// Stylize row number with padding
					FormattedText formattedText5 = new FormattedText(rowInMeasure.ToString("X2"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface4, 12.0, foreground);
					drawingContext2.DrawText(formattedText5, new Point(12.0, num27 + 1.0)); // Shift right and down slightly for padding
				}
			}

			double yStart = drawStart * 14.0;
			double yEnd = drawEnd * 14.0;

			// Draw alternating striping for columns
			int globalColIndex = 0;
			for (int m = 0; m < _trackXOffsets.Count; m++)
			{
				double num28 = _trackXOffsets[m];
				int num29 = _trackPolyphonies[m];
				for (int n = 0; n < num29; n++)
				{
					double colWidth = GetTrackColWidth(_trackerCanvasIndices[m]);
					double num30 = num28 + (double)(n * colWidth);
					
					// Draw striping
					if (globalColIndex % 2 == 1)
					{
						drawingContext2.DrawRectangle(new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)), null, new Rect(num30, yStart, colWidth, yEnd - yStart));
					}
					globalColIndex++;
				}
			}

			Pen pen2 = new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 1.0);
			Pen pen3 = new Pen(new SolidColorBrush(Color.FromRgb(35, 35, 35)), 1.0);
			for (int m = 0; m < _trackXOffsets.Count; m++)
			{
				double num28 = _trackXOffsets[m];
				int num29 = _trackPolyphonies[m];
				for (int n = 0; n < num29; n++)
				{
					double num30 = num28 + (double)(n * GetTrackColWidth(_trackerCanvasIndices[m]));
					if (n == 0)
					{
						drawingContext2.DrawLine(pen2, new Point(num30, yStart), new Point(num30, yEnd));
					}
					else
					{
						drawingContext2.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(50, 50, 50)), 1.0), new Point(num30, yStart), new Point(num30, yEnd));
					}
					if (PreferencesManager.Current.ShowGridLines)
					{
						drawingContext2.DrawLine(pen3, new Point(num30 + 24.0, yStart), new Point(num30 + 24.0, yEnd));
						drawingContext2.DrawLine(pen3, new Point(num30 + 42.0, yStart), new Point(num30 + 42.0, yEnd));
					}
				}
			}
			drawingContext2.DrawLine(pen2, new Point(num11, yStart), new Point(num11, yEnd));
		}
		DrawingGroup dg = new DrawingGroup();
		using (var dc = dg.Open())
		{
			dc.DrawDrawing(bgDrawingVisual.Drawing);
		}
		TrackerBackgroundCanvas.Background = new DrawingBrush(dg)
		{
			Stretch = Stretch.None,
			AlignmentX = AlignmentX.Left,
			AlignmentY = AlignmentY.Top,
			TileMode = TileMode.None,
			Viewport = new Rect(0.0, 0.0, num11, num10),
			ViewportUnits = BrushMappingMode.Absolute
		};
		UpdateEditModeTint();
	}

	private void DrawGridLines(double targetWidth)
	{
		BackgroundCanvas.Children.Clear();
		double height = 2560.0;
		BackgroundCanvas.Height = height;
		NoteCanvas.Height = height;
		DrawTimeline(targetWidth);

		double totalWidth = targetWidth;
		DrawingGroup drawingGroup = new DrawingGroup();
		using (DrawingContext drawingContext = drawingGroup.Open())
		{
			// Horizontal pitch lines
			for (int i = 0; i < 128; i++)
			{
				int midiPitch = 127 - i;
				SolidColorBrush brush = new SolidColorBrush(IsBlackKey(midiPitch) ? Color.FromRgb(30, 30, 30) : Color.FromRgb(45, 45, 45));
				drawingContext.DrawRectangle(brush, null, new Rect(0.0, (double)(i * 20), totalWidth, 20.0));
				Pen pen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1.0);
				drawingContext.DrawLine(pen, new Point(0.0, (double)(i * 20)), new Point(totalWidth, (double)(i * 20)));
			}

			// Vertical beat and measure lines
			int totalBeats = (int)Math.Ceiling(totalWidth / (double)BeatWidth);
			Pen measurePen = new Pen(new SolidColorBrush(Color.FromRgb(100, 100, 100)), 2.0);
			Pen beatPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1.0);

			double curBeat = 0;
			int measureIndex = 0;
			while (curBeat <= totalBeats)
			{
				var ts = GetTimeSignatureAtBeat(curBeat);
				double beatsInM = ts.BeatLength;
				if (beatsInM <= 0) beatsInM = 4.0;

				double x = curBeat * BeatWidth;
				drawingContext.DrawLine(measurePen, new Point(x, 0.0), new Point(x, 2560.0));

				// Beat sub-lines inside this measure
				for (int b = 1; b < Math.Round(beatsInM) && (curBeat + b) <= totalBeats; b++)
				{
					double bx = (curBeat + b) * BeatWidth;
					drawingContext.DrawLine(beatPen, new Point(bx, 0.0), new Point(bx, 2560.0));
				}

				curBeat += beatsInM;
				measureIndex++;
			}
		}

		DrawingBrush background = new DrawingBrush(drawingGroup)
		{
			TileMode = TileMode.None,
			Stretch = Stretch.None,
			AlignmentX = AlignmentX.Left,
			AlignmentY = AlignmentY.Top,
			Viewport = new Rect(0.0, 0.0, totalWidth, 2560.0),
			ViewportUnits = BrushMappingMode.Absolute
		};
		BackgroundCanvas.Background = background;
	}

	private void DrawTimeline(double targetWidth)
	{
		TimelineCanvas.Children.Clear();
		if (playheadTriangle != null)
		{
			TimelineCanvas.Children.Add(playheadTriangle);
		}
		TimelineCanvas.Width = targetWidth;

		int totalBeats = (int)Math.Ceiling(targetWidth / (double)BeatWidth);
		double curBeat = 0;
		int measureNumber = 1;

		while (curBeat <= totalBeats)
		{
			var ts = GetTimeSignatureAtBeat(curBeat);
			double beatsInM = ts.BeatLength;
			if (beatsInM <= 0) beatsInM = 4.0;

			double x = curBeat * BeatWidth;

			TextBlock element = new TextBlock
			{
				Text = measureNumber.ToString(),
				Foreground = new SolidColorBrush(Colors.LightGray),
				FontSize = 10.0
			};
			Canvas.SetLeft(element, x + 2);
			Canvas.SetTop(element, 2.0);
			TimelineCanvas.Children.Add(element);

			Line element2 = new Line
			{
				X1 = x,
				Y1 = 15.0,
				X2 = x,
				Y2 = 30.0,
				Stroke = new SolidColorBrush(Colors.Gray),
				StrokeThickness = 1.5
			};
			TimelineCanvas.Children.Add(element2);

			curBeat += beatsInM;
			measureNumber++;
		}
	}
	}
}




