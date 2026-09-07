using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void AudioEngine_PlaybackStopped(object sender, EventArgs e)
	{
		Dispatcher.Invoke(() =>
		{
			StopButton_Click(this, new RoutedEventArgs());
		});
	}


	private void PlayButton_Click(object sender, RoutedEventArgs e)
	{
		if (!isPlaying)
		{
			if (_loadedMidi == null)
			{
				return;
			}
			if (ViewTrackerMenu.IsChecked)
			{
				double num2 = _trackerCursorRow;
				double num3 = num2 / 4.0;
				double seconds = BeatsToSeconds(num3);
				if (Math.Abs(seconds - _audioEngine.CurrentTimeSeconds) > 0.1)
				{
					_audioEngine.Seek(seconds);
					UpdateVisualPlayhead();
				}
				UpdateTrackerCursor();
			}
			_audioEngine.Play();
			isPlaying = true;
			playbackTimer.Start();
			PlayButton.Content = "Pause";
		}
		else
		{
			_audioEngine.Pause();
			isPlaying = false;
			playbackTimer.Stop();
			stopwatch.Stop();
			PlayButton.Content = "Play";

			if (ViewTrackerMenu.IsChecked)
			{
				double rowBeats = _trackerCursorRow / 4.0;
				double seconds = BeatsToSeconds(rowBeats);
				_audioEngine.Seek(seconds);
				SetPlayheadPosition(rowBeats * BeatWidth);
				UpdateVisualPlayhead();
				UpdateTrackerCursor();
			}
		}
	}


	private void StopButton_Click(object sender, RoutedEventArgs e)
	{
		_audioEngine.Stop();
		isPlaying = false;
		playbackTimer.Stop();
		stopwatch.Stop();
		PlayButton.Content = "Play";
		SetPlayheadPosition(0.0);
		GridScrollViewer.ScrollToHorizontalOffset(0.0);
		TrackerGridScrollViewer.ScrollToVerticalOffset(0.0);
	}


	private void PlaybackTimer_Tick(object sender, EventArgs e)
	{
		if (isPlaying && _loadedMidi != null)
		{
			double currentTimeSeconds = _audioEngine.CurrentTimeSeconds - (PreferencesManager.Current.RenderDelayMs / 1000.0);
			if (currentTimeSeconds < 0.0)
			{
				currentTimeSeconds = 0.0;
			}
			double num2 = SecondsToBeats(currentTimeSeconds);
			if (_maxBeats > 0.0 && num2 >= _maxBeats)
			{
				_audioEngine.Seek(0.0);
				num2 = 0.0;
				lastBeatIndex = -1;
			}

			int currentBeat = (int)Math.Floor(num2);
			if (currentBeat != lastBeatIndex && currentBeat >= 0)
			{
				lastBeatIndex = currentBeat;
			}

			double playheadPosition = num2 * (double)BeatWidth;
			SetPlayheadPosition(playheadPosition);
			UpdateVisualPlayhead();
		}
	}


	private async void LoadReference_Click(object sender, RoutedEventArgs e)
	{
		var openFileDialog = new OpenFileDialog
		{
			Filter = "Audio Files (*.wav;*.mp3;*.aiff)|*.wav;*.mp3;*.aiff|All files (*.*)|*.*"
		};

		if (openFileDialog.ShowDialog() == true)
		{
			_referenceAudioPath = openFileDialog.FileName;
			_audioEngine.LoadReferenceTrack(openFileDialog.FileName);
			
			// Extract peaks
			_referenceWaveformPeaks = await WaveformRenderer.GeneratePeaksAsync(openFileDialog.FileName, 1024);
			
			// Redraw Waveforms
			DrawWaveforms();
		}
	}


	private void ReferenceMixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_audioEngine != null)
		{
			_audioEngine.ReferenceMix = e.NewValue;
		}
	}


	private void ReferenceOffsetTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_audioEngine != null && double.TryParse(ReferenceOffsetTextBox.Text, out double ms))
		{
			_audioEngine.ReferenceOffsetSeconds = ms / 1000.0;
			DrawWaveforms();
		}
	}

    }
}
