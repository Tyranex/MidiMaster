using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Media;
using System.Windows.Input;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Interaction;


namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void AddTrackMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (_loadedMidi == null)
		{
			_loadedMidi = new MidiFile();
			_loadedMidi.TimeDivision = new TicksPerQuarterNoteTimeDivision(480);
			_activeTrackIndex = 0;
			_timeSignatures.Clear();
			_timeSignatures.Add(new TimeSignaturePoint { MetricTime = 0, BeatTime = 0, Numerator = 4, Denominator = 4 });
			UndoManager.Clear();
		}

		if (_loadedMidi != null)
		{
			MenuItem menuItem = (MenuItem)sender;
			int num = (int)menuItem.Tag;
			TrackChunk trackChunk = new TrackChunk();
			int num2 = _loadedMidi.GetTrackChunks().Count();
			int num3 = num2 % 16;
			if (num3 == 9)
			{
				num3 = (num3 + 1) % 16;
			}
			trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)(byte)num)
			{
				DeltaTime = 0L,
				Channel = (FourBitNumber)(byte)num3
			});
			trackChunk.Events.Add(new SequenceTrackNameEvent($"Track {num2 + 1} ({InstrumentNames[num]})")
			{
				DeltaTime = 0L
			});
			_loadedMidi.Chunks.Add(trackChunk);
			BuildNoteCache();
			LoadMidiIntoAudioEngine();
			DrawPianoRoll();
			if (ViewTrackerMenu.IsChecked)
			{
				GenerateTrackerView();
			}
		}
	}


	private void WindowMinimize_Click(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}


	private void WindowMaximize_Click(object sender, RoutedEventArgs e)
	{
		WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
	}


	private void WindowClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}


    private void PreferencesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var prefsWindow = new PreferencesWindow();
        prefsWindow.Owner = this;
        if (prefsWindow.ShowDialog() == true)
        {
            if (TrackerView.Visibility == Visibility.Visible)
            {
                GenerateTrackerView();
            }
        }
    }


	private void EditCut_Click(object sender, RoutedEventArgs e)
	{
		if (TrackerView.Visibility == Visibility.Visible)
		{
			CutTrackerSelection();
		}
	}


	private void EditCopy_Click(object sender, RoutedEventArgs e)
	{
		if (TrackerView.Visibility == Visibility.Visible)
		{
			CopyTrackerSelection();
		}
	}


	private void EditPaste_Click(object sender, RoutedEventArgs e)
	{
		if (TrackerView.Visibility == Visibility.Visible)
		{
			PasteTrackerSelection();
		}
	}


	private void EditDelete_Click(object sender, RoutedEventArgs e)
	{
		if (TrackerView.Visibility == Visibility.Visible)
		{
			if (_trackerSelectionStartRow.HasValue && _trackerSelectionEndRow.HasValue)
			{
				DeleteTrackerSelection();
			}
			else
			{
				DeleteNoteAtPlayhead();
			}
		}
	}


	private void MaximizeVolume_Click(object sender, RoutedEventArgs e)
	{
		if (_loadedMidi == null) return;
		int maxVelocity = 0;
		foreach (var chunk in _loadedMidi.GetTrackChunks())
		{
			foreach (var note in chunk.GetNotes())
			{
				if (note.Velocity > maxVelocity) maxVelocity = note.Velocity;
			}
		}

		if (maxVelocity == 0 || maxVelocity == 127) return;

		double scale = 127.0 / maxVelocity;
		foreach (var chunk in _loadedMidi.GetTrackChunks())
		{
			using (var notesManager = chunk.ManageNotes())
			{
				foreach (var note in notesManager.Objects)
				{
					note.Velocity = (Melanchall.DryWetMidi.Common.SevenBitNumber)(byte)Math.Min(127, Math.Round(note.Velocity * scale));
				}
			}
		}

		PushUndoState($"Maximize Volume (Scale {scale:F2}x)");

		BuildNoteCache();
		DrawPianoRoll();
		if (ViewTrackerMenu.IsChecked) GenerateTrackerView();

		double percentage = (scale - 1.0) * 100.0;
		var dialog = new SuccessDialogWindow($"Volume successfully maximized!\nBoosted by {percentage:F0}%.");
		dialog.Owner = this;
		dialog.ShowDialog();
	}


	private void InstrumentEditor_Click(object sender, RoutedEventArgs e)
	{
		var editor = new InstrumentEditorWindow(_audioEngine);
		editor.Owner = this;
		editor.Show();
	}


	private void Changelog_Click(object sender, RoutedEventArgs e)
	{
		var changelogWindow = new ChangelogWindow();
		changelogWindow.Owner = this;
		changelogWindow.ShowDialog();
	}


	private void ViewMode_Click(object sender, RoutedEventArgs e)
	{
		if (sender == ViewPianoRollMenu)
		{
			ViewPianoRollMenu.IsChecked = true;
			ViewTrackerMenu.IsChecked = false;
			PianoRollView.Visibility = Visibility.Visible;
			TrackerView.Visibility = Visibility.Collapsed;
		}
		else if (sender == ViewTrackerMenu)
		{
			ViewTrackerMenu.IsChecked = true;
			ViewPianoRollMenu.IsChecked = false;
			TrackerView.Visibility = Visibility.Visible;
			PianoRollView.Visibility = Visibility.Collapsed;
			if (_loadedMidi != null)
			{
				GenerateTrackerView();
			}
		}
	}


	private void TimeSignatureButton_Click(object sender, RoutedEventArgs e)
	{
		Grid grid = new Grid { Margin = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		TextBlock prompt = new TextBlock
		{
			Text = "Set Project / Tracker Time Signature:",
			Foreground = Brushes.LightGray,
			FontSize = 13,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 12)
		};
		Grid.SetRow(prompt, 0);
		grid.Children.Add(prompt);

		StackPanel inputPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
		TextBox numBox = new TextBox
		{
			Text = _timeSignatureNumerator.ToString(),
			Width = 45,
			Height = 28,
			FontSize = 14,
			FontWeight = FontWeights.Bold,
			TextAlignment = TextAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			VerticalContentAlignment = VerticalAlignment.Center
		};
		TextBlock slash = new TextBlock
		{
			Text = " / ",
			FontSize = 18,
			FontWeight = FontWeights.Bold,
			Foreground = Brushes.White,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(6, 0, 6, 0)
		};
		TextBox denBox = new TextBox
		{
			Text = _timeSignatureDenominator.ToString(),
			Width = 45,
			Height = 28,
			FontSize = 14,
			FontWeight = FontWeights.Bold,
			TextAlignment = TextAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			VerticalContentAlignment = VerticalAlignment.Center
		};
		inputPanel.Children.Add(numBox);
		inputPanel.Children.Add(slash);
		inputPanel.Children.Add(denBox);
		Grid.SetRow(inputPanel, 1);
		grid.Children.Add(inputPanel);

		CheckBox atCursorCheck = new CheckBox
		{
			Content = "Apply at current tracker cursor row only",
			Foreground = Brushes.LightGray,
			FontSize = 11,
			Margin = new Thickness(0, 4, 0, 10),
			HorizontalAlignment = HorizontalAlignment.Center,
			Visibility = ViewTrackerMenu.IsChecked ? Visibility.Visible : Visibility.Collapsed
		};
		Grid.SetRow(atCursorCheck, 2);
		grid.Children.Add(atCursorCheck);

		StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
		Button okBtn = new Button
		{
			Content = "Apply",
			Width = 75,
			Height = 26,
			Margin = new Thickness(0, 0, 8, 0),
			Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			FontWeight = FontWeights.SemiBold,
			Cursor = Cursors.Hand
		};
		Button cancelBtn = new Button
		{
			Content = "Cancel",
			Width = 75,
			Height = 26,
			Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand
		};

		var tsWindow = CreateCustomDialogWindow("Edit Time Signature", 320, 220, grid);

		okBtn.Click += (s, ev) =>
		{
			if (int.TryParse(numBox.Text, out int n) && int.TryParse(denBox.Text, out int d))
			{
				if (n > 0 && d > 0)
				{
					bool atCursor = atCursorCheck.IsChecked == true;
					SetTimeSignature(n, d, atCursor);
					tsWindow.DialogResult = true;
					tsWindow.Close();
					return;
				}
			}
			MessageBox.Show("Please enter valid positive numbers for time signature (e.g. 4 / 4, 3 / 4, 6 / 8).", "Invalid Time Signature", MessageBoxButton.OK, MessageBoxImage.Warning);
		};

		cancelBtn.Click += (s, ev) => tsWindow.Close();

		btnPanel.Children.Add(okBtn);
		btnPanel.Children.Add(cancelBtn);
		Grid.SetRow(btnPanel, 4);
		grid.Children.Add(btnPanel);

		tsWindow.ShowDialog();
	}


	private void BpmCalculator_Click(object sender, RoutedEventArgs e)
	{
		BpmCalculatorWindow bpmWindow = new BpmCalculatorWindow();
		bpmWindow.Owner = this;
		bpmWindow.ShowDialog();
	}

    }
}
