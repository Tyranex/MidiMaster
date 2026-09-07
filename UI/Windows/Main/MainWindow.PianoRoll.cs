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
using System.Windows.Data;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace SS14_MIDI_IDE
{
	public class InstrumentItem
	{
		public string Name { get; set; }
		public int ProgramIndex { get; set; }
		public bool IsSelectable { get; set; }
		public override string ToString() => Name;
	}

	public partial class MainWindow : Window
	{
	private void DrawPianoRoll()
	{
		if (_loadedMidi == null)
		{
			return;
		}
		MidiFile loadedMidi = _loadedMidi;
		_maxBeats = 0.0;
		short num = 480;
		if (loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNoteTimeDivision)
		{
			num = ticksPerQuarterNoteTimeDivision.TicksPerQuarterNote;
		}
		foreach (var cachedTrackNotes in _cachedNotes)
		{
			foreach (Note note in cachedTrackNotes)
			{
				double num2 = (double)note.EndTime / (double)num;
				if (num2 > _maxBeats)
				{
					_maxBeats = num2;
				}
			}
		}
		List<UIElement> list = (from e in NoteCanvas.Children.OfType<UIElement>()
			where e != playheadLine
			select e).ToList();
		foreach (UIElement item in list)
		{
			NoteCanvas.Children.Remove(item);
		}
		TrackListPanel.Children.Clear();
		_trackCanvases.Clear();
		_trackMuted.Clear();
		_visualToAbsoluteTrackIndices.Clear();
		_trackShowFx.Clear();
		_speakerButtons.Clear();
		_eyeButtons.Clear();
		_preSoloMutedStates = null;
		_soloedAudioTracks.Clear();
		_preSoloHiddenStates = null;
		_soloedVisualTracks.Clear();
		Color[] array = new Color[7]
		{
			Color.FromRgb(0, 122, 204),
			Color.FromRgb(204, 0, 122),
			Color.FromRgb(122, 204, 0),
			Color.FromRgb(204, 122, 0),
			Color.FromRgb(153, 51, byte.MaxValue),
			Color.FromRgb(51, 204, byte.MaxValue),
			Color.FromRgb(byte.MaxValue, 204, 51)
		};
		_trackChannels.Clear();
		if (_trackColors.Count == 0)
		{
			for (int i = 0; i < 100; i++)
			{
				_trackColors.Add(array[i % array.Length]);
			}
		}
		List<TrackChunk> list2 = loadedMidi.GetTrackChunks().ToList();
		for (int num3 = 0; num3 < _cachedNotes.Count; num3++)
		{
			ICollection<Note> notes = _cachedNotes[num3];
			TrackChunk trackChunk = list2[num3];

			Color color = _trackColors[num3];
			string text = $"Track {num3 + 1}";
			SequenceTrackNameEvent sequenceTrackNameEvent = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
			if (sequenceTrackNameEvent != null && !string.IsNullOrWhiteSpace(sequenceTrackNameEvent.Text))
			{
				text = sequenceTrackNameEvent.Text;
			}
			Border border = new Border
			{
				BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
				BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				Background = new SolidColorBrush(Color.FromArgb(64, color.R, color.G, color.B)),
				Padding = new Thickness(5.0, 10.0, 5.0, 10.0),
				Margin = new Thickness(0.0),
				Cursor = Cursors.Hand
			};
			int trackIndex = _trackCanvases.Count;
			int absoluteTrackIndex = num3;
			border.MouseLeftButtonDown += delegate
			{
				_activeTrackIndex = absoluteTrackIndex;
				_activePolyphonyIndex = 0;
				_activeSubColumn = 0;
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
				UpdateActiveTrackVisuals();
			};



			
			// Main container for the track item
			Grid mainGrid = new Grid();
			mainGrid.Margin = new Thickness(0, 0, 0, 8); // Add vertical padding between tracks
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Auto) });
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Auto) });

			// Top bar: Drag, Color, Name, Eye, Speaker
			Grid topGrid = new Grid();
			topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Auto) });
			topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Auto) });
			topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
			topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Auto) });
			topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Auto) });

			Color comboBgColor = Color.FromRgb((byte)Math.Min(255, color.R * 0.2 + 20), (byte)Math.Min(255, color.G * 0.2 + 20), (byte)Math.Min(255, color.B * 0.2 + 20));
			Color comboBorderColor = Color.FromRgb((byte)Math.Min(255, color.R * 0.4 + 40), (byte)Math.Min(255, color.G * 0.4 + 40), (byte)Math.Min(255, color.B * 0.4 + 40));

			var programChanges = trackChunk.Events.OfType<ProgramChangeEvent>().ToList();
			bool hasMultiplePrograms = programChanges.Count > 1;

			var instrumentItems = new List<InstrumentItem>();
			instrumentItems.Add(new InstrumentItem { Name = "--- Soundfonts ---", ProgramIndex = -1, IsSelectable = false });
			
			for (int i = 0; i < AudioEngine.CustomInstrumentBaseIndex; i++)
			{
				instrumentItems.Add(new InstrumentItem { Name = $"  {i}: {InstrumentNames[i]}", ProgramIndex = i, IsSelectable = true });
			}
			
			if (InstrumentNames.Length > AudioEngine.CustomInstrumentBaseIndex)
			{
				instrumentItems.Add(new InstrumentItem { Name = "--- Generated Tones ---", ProgramIndex = -1, IsSelectable = false });
				for (int i = AudioEngine.CustomInstrumentBaseIndex; i < InstrumentNames.Length; i++)
				{
					instrumentItems.Add(new InstrumentItem { Name = $"  {i}: {InstrumentNames[i]}", ProgramIndex = i, IsSelectable = true });
				}
			}

			if (hasMultiplePrograms)
			{
				instrumentItems.Add(new InstrumentItem { Name = "(Multiple Instruments)", ProgramIndex = -2, IsSelectable = true });
			}

			Style containerStyle = new Style(typeof(ComboBoxItem));
			containerStyle.Setters.Add(new Setter(ComboBoxItem.IsEnabledProperty, new Binding("IsSelectable")));

			ComboBox instrumentCombo = new ComboBox
			{
				Margin = new Thickness(0.0, 5.0, 0.0, 0.0),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				Background = new SolidColorBrush(comboBgColor),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(comboBorderColor),
				ToolTip = "Select Instrument",
				ItemsSource = instrumentItems,
				ItemContainerStyle = containerStyle
			};

			Rectangle element = new Rectangle
			{
				Width = 12.0,
				Height = 12.0,
				Fill = new SolidColorBrush(color),
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				RadiusX = 2.0,
				RadiusY = 2.0,
				VerticalAlignment = VerticalAlignment.Center,
				Cursor = Cursors.Hand
			};
			element.MouseLeftButtonDown += (s, e) =>
			{
				e.Handled = true; // Prevent track selection
				var cd = new System.Windows.Forms.ColorDialog();
				cd.Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B);
				if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					Color newColor = Color.FromRgb(cd.Color.R, cd.Color.G, cd.Color.B);
					_trackColors[absoluteTrackIndex] = newColor;
					element.Fill = new SolidColorBrush(newColor);
					border.Background = new SolidColorBrush(Color.FromArgb(64, newColor.R, newColor.G, newColor.B));
					
					// Update ComboBox colors dynamically
					Color newComboBg = Color.FromRgb((byte)Math.Min(255, newColor.R * 0.2 + 20), (byte)Math.Min(255, newColor.G * 0.2 + 20), (byte)Math.Min(255, newColor.B * 0.2 + 20));
					Color newComboBorder = Color.FromRgb((byte)Math.Min(255, newColor.R * 0.4 + 40), (byte)Math.Min(255, newColor.G * 0.4 + 40), (byte)Math.Min(255, newColor.B * 0.4 + 40));
					instrumentCombo.Background = new SolidColorBrush(newComboBg);
					instrumentCombo.BorderBrush = new SolidColorBrush(newComboBorder);

					if (ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
				}
			};
			Grid.SetColumn(element, 1);

			TextBlock element2 = new TextBlock
			{
				Text = text,
				Foreground = new SolidColorBrush(Colors.LightGray),
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
				TextTrimming = TextTrimming.CharacterEllipsis // Ensure it truncates gracefully if too long
			};
			Grid.SetColumn(element2, 2);

			ContextMenu contextMenu = new ContextMenu();
			MenuItem renameItem = new MenuItem { Header = "Rename" };
			renameItem.Click += (s, e) =>
			{
				Window inputWindow = new Window
				{
					Title = "Rename Track",
					Width = 300,
					Height = 150,
					WindowStartupLocation = WindowStartupLocation.CenterOwner,
					Owner = this,
					ResizeMode = ResizeMode.NoResize,
					Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
					Foreground = Brushes.White
				};
				StackPanel stackPanel = new StackPanel { Margin = new Thickness(10) };
				TextBlock textBlock = new TextBlock { Text = "Enter new track name:", Foreground = Brushes.White };
				TextBox textBox = new TextBox { Text = text, Margin = new Thickness(0, 10, 0, 10), Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)), Foreground = Brushes.White };
				Button okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)), Foreground = Brushes.White };
				okButton.Click += (s2, e2) =>
				{
					inputWindow.DialogResult = true;
				};
				stackPanel.Children.Add(textBlock);
				stackPanel.Children.Add(textBox);
				stackPanel.Children.Add(okButton);
				inputWindow.Content = stackPanel;
				
				if (inputWindow.ShowDialog() == true)
				{
					string newName = textBox.Text;
					if (!string.IsNullOrWhiteSpace(newName))
					{
						var seqNameEvent = trackChunk.Events.OfType<SequenceTrackNameEvent>().FirstOrDefault();
						if (seqNameEvent != null)
						{
							seqNameEvent.Text = newName;
						}
						else
						{
							trackChunk.Events.Insert(0, new SequenceTrackNameEvent(newName) { DeltaTime = 0 });
						}
						element2.Text = newName;
						PushUndoState($"Rename track {absoluteTrackIndex + 1} to '{newName}'");
						_midiSaveTimer.Stop();
						_midiSaveTimer.Start();
					}
				}
			};
			contextMenu.Items.Add(renameItem);
			
			MenuItem deleteItem = new MenuItem { Header = "Delete Track", Foreground = Brushes.Red };
			deleteItem.Click += (s, e) => 
			{ 
				bool hasData = notes.Any();
				bool shouldDelete = true;
				
				if (hasData)
				{
					shouldDelete = SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show(
						"This track contains MIDI data. Are you sure you want to delete it?", 
						"Delete Track", 
						MessageBoxButton.YesNo, 
						MessageBoxImage.Warning) == MessageBoxResult.Yes;
				}
				
				if (shouldDelete)
				{
					DeleteTrack(absoluteTrackIndex); 
				}
			};
			contextMenu.Items.Add(deleteItem);
			border.ContextMenu = contextMenu;

			Canvas trackNotesCanvas = new Canvas();
			NoteCanvas.Children.Add(trackNotesCanvas);
			_trackCanvases.Add(trackNotesCanvas);
			_trackMuted.Add(item: false);
			_visualToAbsoluteTrackIndices.Add(absoluteTrackIndex);
			while (_trackShowFx.Count <= _trackCanvases.Count) _trackShowFx.Add(false);

			// Determine track channel: percussion tracks use 9, otherwise check existing notes/events or assign unique channel
			int channelForTrack = (notes.Any(n => n.Channel == (FourBitNumber)9)) ? 9 : -1;
			if (channelForTrack == -1)
			{
				var noteWithChannel = notes.FirstOrDefault();
				if (noteWithChannel != null)
				{
					channelForTrack = (int)noteWithChannel.Channel;
				}
				else
				{
					var chanEv = trackChunk.Events.OfType<ChannelEvent>().FirstOrDefault();
					if (chanEv != null)
					{
						channelForTrack = (int)chanEv.Channel;
					}
					else
					{
						channelForTrack = (absoluteTrackIndex % 16 == 9 ? (absoluteTrackIndex + 1) % 16 : absoluteTrackIndex % 16);
					}
				}
			}
			while (_trackChannels.Count <= absoluteTrackIndex) _trackChannels.Add(0);
			_trackChannels[absoluteTrackIndex] = channelForTrack;

			ToggleButton eyeButton = new ToggleButton
			{
				Content = "\ud83d\udc41",
				Width = 22.0,
				Height = 22.0,
				Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
				Background = Brushes.Transparent,
				Foreground = new SolidColorBrush(Colors.LightGray),
				BorderThickness = new Thickness(0.0),
				IsChecked = true,
				ToolTip = "Toggle Visibility",
				Style = (Style)FindResource("SimpleFlatToggleButton")
			};

			Grid.SetColumn(eyeButton, 3);
			_eyeButtons.Add(eyeButton);
			eyeButton.PreviewMouseLeftButtonDown += (s, e) =>
			{
				if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
				{
					e.Handled = true;
					HandleVisualSolo(trackIndex);
				}
			};

			eyeButton.Checked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Visible;
				eyeButton.Foreground = new SolidColorBrush(Colors.LightGray);
				if (!_isUpdatingVisualUI && ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
			};
			eyeButton.Unchecked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Hidden;
				eyeButton.Foreground = new SolidColorBrush(Colors.DimGray);
				if (!_isUpdatingVisualUI && ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
			};

			ToggleButton speakerButton = new ToggleButton
			{
				Content = "\ud83d\udd0a",
				Width = 22.0,
				Height = 22.0,
				Margin = new Thickness(2.0, 0.0, 2.0, 0.0),
				Background = Brushes.Transparent,
				Foreground = new SolidColorBrush(Colors.LightGray),
				BorderThickness = new Thickness(0.0),
				IsChecked = true,
				ToolTip = "Toggle Mute",
				Style = (Style)FindResource("SimpleFlatToggleButton")
			};

			Grid.SetColumn(speakerButton, 4);
			_speakerButtons.Add(speakerButton);
			speakerButton.PreviewMouseLeftButtonDown += (s, e) =>
			{
				if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
				{
					e.Handled = true;
					HandleAudioSolo(trackIndex);
				}
			};

			speakerButton.Checked += delegate
			{
				_trackMuted[trackIndex] = false;
				speakerButton.Content = "\ud83d\udd0a";
				speakerButton.Foreground = new SolidColorBrush(Colors.LightGray);
				if (!_isUpdatingMuteUI) 
				{
					_audioEngine.SetTrackMute(absoluteTrackIndex, false);
				}
			};
			speakerButton.Unchecked += delegate
			{
				_trackMuted[trackIndex] = true;
				speakerButton.Content = "\ud83d\udd07";
				speakerButton.Foreground = new SolidColorBrush(Colors.DimGray);
				if (!_isUpdatingMuteUI) 
				{
					_audioEngine.SetTrackMute(absoluteTrackIndex, true);
				}
			};

			instrumentCombo.Style = (Style)FindResource("SimpleFlatComboBox");
			
			int programNumber = 0;
			// Try to get the bank select and program change event from this track
			var bankSelect = trackChunk.Events.OfType<ControlChangeEvent>().LastOrDefault(e => e.ControlNumber == 0 && e.Channel == (FourBitNumber)(byte)channelForTrack);
			int bankNumber = bankSelect != null ? bankSelect.ControlValue : 0;
			var programChange = trackChunk.Events.OfType<ProgramChangeEvent>().LastOrDefault(e => e.Channel == (FourBitNumber)(byte)channelForTrack);
			if (programChange != null)
			{
				programNumber = programChange.ProgramNumber + (bankNumber * 128);
			}
			
			if (hasMultiplePrograms)
			{
				instrumentCombo.SelectedItem = instrumentItems.FirstOrDefault(x => x.ProgramIndex == -2);
			}
			else
			{
				instrumentCombo.SelectedItem = instrumentItems.FirstOrDefault(x => x.ProgramIndex == programNumber);
			}

			int currentChannel = channelForTrack;
			var currentChunk = trackChunk;
			instrumentCombo.SelectionChanged += delegate
			{
				_activeTrackIndex = absoluteTrackIndex;
				_activePolyphonyIndex = 0;
				_activeSubColumn = 0;
				_trackerSelectionStartRow = _trackerSelectionEndRow = null;
				_trackerSelectionStartLogicalCol = _trackerSelectionEndLogicalCol = null;
				UpdateActiveTrackVisuals();

				var selectedItem = instrumentCombo.SelectedItem as InstrumentItem;
				if (selectedItem == null || !selectedItem.IsSelectable) return;
				
				int selectedProgram = selectedItem.ProgramIndex;
				if (hasMultiplePrograms && selectedProgram == -2) return;

				_audioEngine?.SetProgram(absoluteTrackIndex, currentChannel, selectedProgram);
				
				int bank = selectedProgram / 128;
				int prog = selectedProgram % 128;

				// Remove all existing program changes if overriding multiple
				if (hasMultiplePrograms)
				{
					currentChunk.Events.RemoveAll(e => e is ProgramChangeEvent || (e is ControlChangeEvent cc && cc.ControlNumber == 0));
					hasMultiplePrograms = false;
					
					var multiItem = instrumentItems.FirstOrDefault(x => x.ProgramIndex == -2);
					if (multiItem != null) instrumentItems.Remove(multiItem);
					
					instrumentCombo.ItemsSource = null;
					instrumentCombo.ItemsSource = instrumentItems;
					instrumentCombo.SelectedItem = selectedItem;
				}

				var existingBankSelect = currentChunk.Events.OfType<ControlChangeEvent>().FirstOrDefault(e => e.ControlNumber == 0);
				if (existingBankSelect != null)
				{
					existingBankSelect.ControlValue = (SevenBitNumber)(byte)bank;
					existingBankSelect.Channel = (FourBitNumber)(byte)currentChannel;
				}
				else if (bank > 0)
				{
					currentChunk.Events.Insert(0, new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)(byte)bank)
					{
						Channel = (FourBitNumber)(byte)currentChannel,
						DeltaTime = 0
					});
				}

				var pChange = currentChunk.Events.OfType<ProgramChangeEvent>().FirstOrDefault();
				if (pChange != null)
				{
					pChange.ProgramNumber = (SevenBitNumber)(byte)prog;
					pChange.Channel = (FourBitNumber)(byte)currentChannel;
				}
				else
				{
					currentChunk.Events.Insert(bank > 0 ? 1 : 0, new ProgramChangeEvent((SevenBitNumber)(byte)prog)
					{
						Channel = (FourBitNumber)(byte)currentChannel,
						DeltaTime = 0
					});
				}
				_midiSaveTimer.Stop();
				_midiSaveTimer.Start();
			};
			
			// Set the initial program for this track in the synthesizer
			if (!hasMultiplePrograms)
			{
				_audioEngine?.SetProgram(absoluteTrackIndex, currentChannel, programNumber);
			}

			TextBlock ellipsis = new TextBlock
			{
				Text = "⋮",
				Foreground = Brushes.Gray,
				FontSize = 16,
				FontWeight = FontWeights.Bold,
				Margin = new Thickness(0, 0, 5, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Cursor = Cursors.SizeNS,
				Background = Brushes.Transparent // important for hit testing
			};
			ellipsis.PreviewMouseLeftButtonDown += (s, e) =>
			{
				_isDraggingTrack = true;
				_draggedTrackOriginalIndex = absoluteTrackIndex;
				_dragStartPoint = e.GetPosition(border);
				
				// Create a visual ghost of the track border
				int width = (int)Math.Max(1, border.ActualWidth);
				int height = (int)Math.Max(1, border.ActualHeight);
				var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
				
				var dv = new DrawingVisual();
				using (var ctx = dv.RenderOpen())
				{
					var vb = new VisualBrush(border);
					ctx.DrawRectangle(vb, null, new Rect(new Point(), new Size(border.ActualWidth, border.ActualHeight)));
				}
				rtb.Render(dv);
				
				var ghostBrush = new ImageBrush(rtb) { Opacity = 0.7 };
				_draggedGhostElement = new Border
				{
					Width = border.ActualWidth,
					Height = border.ActualHeight,
					Background = ghostBrush
				};
				
				var canvasPos = e.GetPosition(DragDropOverlayCanvas);
				Canvas.SetLeft(_draggedGhostElement, canvasPos.X - _dragStartPoint.X);
				Canvas.SetTop(_draggedGhostElement, canvasPos.Y - _dragStartPoint.Y);
				DragDropOverlayCanvas.Children.Add(_draggedGhostElement);

				border.Visibility = Visibility.Collapsed;
				TrackerDimOverlay.Visibility = Visibility.Visible;
				
				Mouse.Capture(MainWindow.GetWindow(this));
				e.Handled = true;
			};
			Grid.SetColumn(ellipsis, 0);
			topGrid.Children.Add(ellipsis);

			topGrid.Children.Add(element);
			topGrid.Children.Add(element2);
			topGrid.Children.Add(eyeButton);
			topGrid.Children.Add(speakerButton);

			Grid.SetRow(topGrid, 0);
			Grid.SetRow(instrumentCombo, 1);
			mainGrid.Children.Add(topGrid);
			mainGrid.Children.Add(instrumentCombo);

			border.Child = mainGrid;
			TrackListPanel.Children.Add(border);
			foreach (Note item2 in notes)
			{
				double length = (double)item2.Time / (double)num * (double)BeatWidth;
				double val = (double)item2.Length / (double)num * (double)BeatWidth;
				double length2 = (127 - (byte)item2.NoteNumber) * 20;
				NoteElement element3 = new NoteElement
				{
					Width = Math.Max(val, 2.0),
					Height = 20.0,
					Background = new SolidColorBrush(color),
					BorderBrush = new SolidColorBrush(Colors.White),
					BorderThickness = new Thickness(0.5),
					Tag = new Tuple<int, Note>(trackIndex, item2)
				};
				Color darkerColor = Color.FromRgb(
					(byte)Math.Max(0, color.R - 50),
					(byte)Math.Max(0, color.G - 50),
					(byte)Math.Max(0, color.B - 50));
				TextBlock noteText = new TextBlock
				{
					Text = GetNoteName(item2.NoteNumber),
					Foreground = new SolidColorBrush(darkerColor),
					FontSize = 10,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(2, 0, 0, 0),
					IsHitTestVisible = false
				};
				element3.Child = noteText;
				Canvas.SetLeft(element3, length);
				Canvas.SetTop(element3, length2);
				trackNotesCanvas.Children.Add(element3);
			}
		}
		UpdateCanvasWidth();
		UpdateActiveTrackVisuals();
		TempoMap tempoMap = loadedMidi.GetTempoMap();
		IEnumerable<ValueChange<Tempo>> tempoChanges = tempoMap.GetTempoChanges();
		if (tempoChanges.Any())
		{
			ValueChange<Tempo> valueChange = tempoChanges.First();
			double beatsPerMinute = valueChange.Value.BeatsPerMinute;
			TempoTextBox.Text = Math.Round(beatsPerMinute).ToString();
		}
	}

	private void ApplyEdits()
	{
		if (_loadedMidi == null) return;

		if (ViewPianoRollMenu.IsChecked)
		{
			List<UIElement> list = (from e in NoteCanvas.Children.OfType<UIElement>()
				where e != playheadLine
				select e).ToList();
			foreach (UIElement item in list)
			{
				NoteCanvas.Children.Remove(item);
			}
			DrawPianoRoll();
		}
		if (ViewTrackerMenu.IsChecked)
		{
			GenerateTrackerView();
		}

		_midiSaveTimer.Stop();
		_midiSaveTimer.Start();
	}

	private int GetTrackProgram(int trackIndex)
	{
		var chunk = _loadedMidi?.GetTrackChunks().ElementAtOrDefault(trackIndex);
		if (chunk == null) return -1;
		int ch = GetTrackChannel(trackIndex);
		var bankSelect = chunk.Events.OfType<Melanchall.DryWetMidi.Core.ControlChangeEvent>().LastOrDefault(e => e.ControlNumber == 0 && e.Channel == (FourBitNumber)(byte)ch);
		int bankNumber = bankSelect != null ? bankSelect.ControlValue : 0;
		var programChange = chunk.Events.OfType<Melanchall.DryWetMidi.Core.ProgramChangeEvent>().LastOrDefault(e => e.Channel == (FourBitNumber)(byte)ch);
		if (programChange != null) return programChange.ProgramNumber + (bankNumber * 128);
		return -1;
	}

	private void DrawPianoKeys()
	{
		KeysCanvas.Children.Clear();
		double height = 2560.0;
		KeysCanvas.Height = height;
		for (int i = 0; i < 128; i++)
		{
			int midiPitch = 127 - i;
			bool flag = IsBlackKey(midiPitch);
			Rectangle element = new Rectangle
			{
				Width = 60.0,
				Height = 20.0,
				Fill = new SolidColorBrush(flag ? Color.FromRgb(20, 20, 20) : Color.FromRgb(220, 220, 220)),
				Stroke = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
				StrokeThickness = 1.0,
				Tag = midiPitch
			};
			element.MouseLeftButtonDown += (s, e) => { _audioEngine?.PreviewNoteOn(_activeTrackIndex, (int)((Rectangle)s).Tag, GetTrackProgram(_activeTrackIndex)); };
			element.MouseLeftButtonUp += (s, e) => { _audioEngine?.PreviewNoteOff(_activeTrackIndex, (int)((Rectangle)s).Tag); };
			element.MouseLeave += (s, e) => { _audioEngine?.PreviewNoteOff(_activeTrackIndex, (int)((Rectangle)s).Tag); };
			Canvas.SetTop(element, i * 20);
			Canvas.SetLeft(element, 0.0);
			KeysCanvas.Children.Add(element);
			string noteName = GetNoteName(midiPitch);
			TextBlock element2 = new TextBlock
			{
				Text = noteName,
				Foreground = new SolidColorBrush(flag ? Colors.White : Colors.Black),
				FontSize = 10.0,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			Canvas.SetTop(element2, i * 20 + 2);
			Canvas.SetLeft(element2, 30.0);
			KeysCanvas.Children.Add(element2);
		}
	}

	private void UpdateCanvasWidth()
	{
		double num = 100 * BeatWidth;
		foreach (UIElement child in NoteCanvas.Children)
		{
			if (child is Rectangle rectangle && (object)rectangle.Parent == NoteCanvas)
			{
				double num2 = Canvas.GetLeft(rectangle) + rectangle.Width;
				if (num2 > num)
				{
					num = num2;
				}
			}
		}
		double num3 = num + (double)(4 * BeatWidth);
		if (BackgroundCanvas.Width != num3)
		{
			BackgroundCanvas.Width = num3;
			NoteCanvas.Width = num3;
			DrawGridLines(num3);
		}
	}

	private void GridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.VerticalChange != 0.0)
		{
			KeysScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
		}
		if (e.HorizontalChange != 0.0)
		{
			TimelineScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
			HorizontalWaveformScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
		}
	}

	private void GridScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)Keyboard.Modifiers == 2)
		{
			e.Handled = true;
			double offset = GridScrollViewer.HorizontalOffset - (double)e.Delta;
			GridScrollViewer.ScrollToHorizontalOffset(offset);
		}
	}

	private void NoteCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		Point pos = e.GetPosition(NoteCanvas);

		if (e.OriginalSource is NoteElement rectangle)
		{
			if (!selectedNotes.Contains(rectangle))
			{
				if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
				{
					ClearSelection();
				}
				SelectNote(rectangle);
			}
			currentNote = rectangle;
			isDraggingNote = true;
			clickPosition = pos;
			NoteCanvas.CaptureMouse();
		}
		else if (e.ClickCount == 2)
		{
			ClearSelection();
			NoteElement rectangle2 = AddNoteAt(pos);
			currentNote = rectangle2;
			isDraggingNote = true;
			clickPosition = pos;
			NoteCanvas.CaptureMouse();
		}
		else
		{
			// Seek playhead to clicked position on piano roll
			SeekFromAbsoluteX(pos.X);

			if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0)
			{
				ClearSelection();
			}
			selectionStartPoint = pos;
			isDragSelecting = true;
			selectionBox = new Rectangle
			{
				Stroke = new SolidColorBrush(Colors.LightBlue),
				StrokeThickness = 1.0,
				Fill = new SolidColorBrush(Color.FromArgb(50, 173, 216, 230)),
				StrokeDashArray = new DoubleCollection { 2.0, 2.0 },
				IsHitTestVisible = false
			};
			Canvas.SetLeft(selectionBox, selectionStartPoint.X);
			Canvas.SetTop(selectionBox, selectionStartPoint.Y);
			NoteCanvas.Children.Add(selectionBox);
			NoteCanvas.CaptureMouse();
		}
	}

	private void ClearSelection()
	{
		foreach (NoteElement selectedNote in selectedNotes)
		{
			selectedNote.BorderBrush = new SolidColorBrush(Colors.White);
			selectedNote.BorderThickness = new Thickness(0.5);
		}
		selectedNotes.Clear();
	}

	private void SelectNote(NoteElement note)
	{
		if (!selectedNotes.Contains(note))
		{
			selectedNotes.Add(note);
			note.BorderBrush = new SolidColorBrush(Colors.Yellow);
			note.BorderThickness = new Thickness(2.0);
		}
	}

	private void SelectNotesInBox(Rectangle box)
	{
		if (box == null) return;
		Rect val = new Rect(Canvas.GetLeft(box), Canvas.GetTop(box), box.Width, box.Height);
		if (val.Width < 1.0 && val.Height < 1.0) return; // Tiny click, don't select

		foreach (NoteElement allNote in GetAllNotes())
		{
			Point notePos = allNote.TranslatePoint(new Point(0, 0), NoteCanvas);
			Rect noteRect = new Rect(notePos.X, notePos.Y, allNote.Width, allNote.Height);
			if (val.IntersectsWith(noteRect))
			{
				SelectNote(allNote);
			}
		}
	}

	private void NoteCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (isDraggingNote)
		{
			Point position = e.GetPosition(NoteCanvas);
			double num = position.X - clickPosition.X;
			foreach (NoteElement selectedNote in selectedNotes)
			{
				double left = Canvas.GetLeft(selectedNote);
				Canvas.SetLeft(selectedNote, left + num);
			}
			clickPosition = position;
		}
		else if (isDragSelecting)
		{
			Point position2 = e.GetPosition(NoteCanvas);
			double length = Math.Min(position2.X, selectionStartPoint.X);
			double length2 = Math.Min(position2.Y, selectionStartPoint.Y);
			double width = Math.Abs(position2.X - selectionStartPoint.X);
			double height = Math.Abs(position2.Y - selectionStartPoint.Y);
			Canvas.SetLeft(selectionBox, length);
			Canvas.SetTop(selectionBox, length2);
			selectionBox.Width = width;
			selectionBox.Height = height;
		}
	}

	private void NoteCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (isDraggingNote)
		{
			foreach (NoteElement selectedNote in selectedNotes)
			{
				double left = Canvas.GetLeft(selectedNote);
				double val = Math.Round(left / (double)BeatWidth) * (double)BeatWidth;
				Canvas.SetLeft(selectedNote, Math.Max(0.0, val));
			}
			isDraggingNote = false;
			currentNote = null;
			NoteCanvas.ReleaseMouseCapture();
			UpdateCanvasWidth();
		}
		else if (isDragSelecting)
		{
			isDragSelecting = false;
			NoteCanvas.ReleaseMouseCapture();
			SelectNotesInBox(selectionBox);
			NoteCanvas.Children.Remove(selectionBox);
			selectionBox = null;
		}
	}

	private void NoteCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource is NoteElement rectangle)
		{
			if (selectedNotes.Contains(rectangle))
			{
				foreach (NoteElement selectedNote in selectedNotes)
				{
					if (selectedNote.Parent is Canvas canvas)
					{
						canvas.Children.Remove(selectedNote);
					}
				}
				selectedNotes.Clear();
			}
			else if (rectangle.Parent is Canvas canvas2)
			{
				canvas2.Children.Remove(rectangle);
			}
			UpdateCanvasWidth();
		}
		else
		{
			ClearSelection();
		}
	}

	private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Point position = e.GetPosition(TimelineCanvas);
		double playheadPosition = Math.Round(position.X / (double)BeatWidth) * (double)BeatWidth;
		SetPlayheadPosition(playheadPosition);
	}

	private void HandleAudioSolo(int trackIndex)
	{
		_isUpdatingMuteUI = true;

		if (_soloedAudioTracks.Count == 0)
		{
			_preSoloMutedStates = _speakerButtons.Select(b => !b.IsChecked.GetValueOrDefault()).ToList();
		}

		if (_soloedAudioTracks.Contains(trackIndex))
		{
			_soloedAudioTracks.Remove(trackIndex);
		}
		else
		{
			if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != System.Windows.Input.ModifierKeys.Control)
			{
				_soloedAudioTracks.Clear();
			}
			_soloedAudioTracks.Add(trackIndex);
		}

		if (_soloedAudioTracks.Count == 0)
		{
			// Restore pre-solo state
			for (int i = 0; i < _speakerButtons.Count; i++)
			{
				bool wasMuted = _preSoloMutedStates != null && i < _preSoloMutedStates.Count ? _preSoloMutedStates[i] : false;
				_speakerButtons[i].IsChecked = !wasMuted; // Checked means unmuted
				_trackMuted[i] = wasMuted;
				_speakerButtons[i].Content = wasMuted ? "\ud83d\udd07" : "\ud83d\udd0a";
				_speakerButtons[i].Foreground = wasMuted ? new SolidColorBrush(Colors.DimGray) : new SolidColorBrush(Colors.LightGray);
			}
			_preSoloMutedStates = null;
		}
		else
		{
			// Apply solo states
			for (int i = 0; i < _speakerButtons.Count; i++)
			{
				bool isSoloed = _soloedAudioTracks.Contains(i);
				_speakerButtons[i].IsChecked = isSoloed;
				_trackMuted[i] = !isSoloed;
				_speakerButtons[i].Content = !isSoloed ? "\ud83d\udd07" : "\ud83d\udd0a";
				_speakerButtons[i].Foreground = !isSoloed ? new SolidColorBrush(Colors.DimGray) : new SolidColorBrush(Colors.LightGray);
			}
		}
		
		for (int i = 0; i < _speakerButtons.Count; i++)
		{
			if (i < _visualToAbsoluteTrackIndices.Count)
			{
				_audioEngine.SetTrackMute(_visualToAbsoluteTrackIndices[i], _trackMuted[i]);
			}
		}
		
		_isUpdatingMuteUI = false;
	}

	private void HandleVisualSolo(int trackIndex)
	{
		_isUpdatingVisualUI = true;
		if (_soloedVisualTracks.Count == 0)
		{
			_preSoloHiddenStates = _eyeButtons.Select(b => !b.IsChecked.GetValueOrDefault()).ToList();
		}
		if (_soloedVisualTracks.Contains(trackIndex))
		{
			_soloedVisualTracks.Remove(trackIndex);
		}
		else
		{
			if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != System.Windows.Input.ModifierKeys.Control)
			{
				_soloedVisualTracks.Clear();
			}
			_soloedVisualTracks.Add(trackIndex);
		}
		if (_soloedVisualTracks.Count == 0)
		{
			// Restore pre-solo state
			for (int i = 0; i < _eyeButtons.Count; i++)
			{
				bool wasHidden = _preSoloHiddenStates != null && i < _preSoloHiddenStates.Count ? _preSoloHiddenStates[i] : false;
				_eyeButtons[i].IsChecked = !wasHidden;
				_trackCanvases[i].Visibility = wasHidden ? Visibility.Hidden : Visibility.Visible;
				_eyeButtons[i].Foreground = wasHidden ? new SolidColorBrush(Colors.DimGray) : new SolidColorBrush(Colors.LightGray);
			}
			_preSoloHiddenStates = null;
		}
		else
		{
			// Apply solo states
			for (int i = 0; i < _eyeButtons.Count; i++)
			{
				bool isSoloed = _soloedVisualTracks.Contains(i);
				_eyeButtons[i].IsChecked = isSoloed;
				_trackCanvases[i].Visibility = !isSoloed ? Visibility.Hidden : Visibility.Visible;
				_eyeButtons[i].Foreground = !isSoloed ? new SolidColorBrush(Colors.DimGray) : new SolidColorBrush(Colors.LightGray);
			}
		}
		_isUpdatingVisualUI = false;
		Dispatcher.InvokeAsync(() =>
		{
			if (ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
		}, System.Windows.Threading.DispatcherPriority.Background);
	}
}
}
