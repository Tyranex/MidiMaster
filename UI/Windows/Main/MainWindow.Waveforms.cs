using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Collections.Generic;


namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_isDraggingWaveform = true;
		var canvas = (Canvas)sender;
		_waveformDragStartPoint = e.GetPosition(canvas);
		_waveformDragStartOffset = _audioEngine.ReferenceOffsetSeconds;
		canvas.CaptureMouse();
	}


	private void WaveformCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (_isDraggingWaveform)
		{
			var canvas = (Canvas)sender;
			Point currentPos = e.GetPosition(canvas);
			
			// Horizontal view (Piano Roll) vs Vertical view (Tracker)
			double deltaPixels;
			double pixelsPerSecond;

			if (canvas == HorizontalWaveformCanvas)
			{
				deltaPixels = currentPos.X - _waveformDragStartPoint.X;
				// In Piano Roll, 1 beat = BeatWidth pixels
				// 1 beat = 60 / BPM seconds
				// So pixels per second = BeatWidth / (60 / BPM)
				// We need current tempo
				int currentBpm = _timeSignatures.Count > 0 ? GetTempoAtRow(0).Bpm : 120;
				pixelsPerSecond = BeatWidth / (60.0 / currentBpm);
			}
			else
			{
				// Tracker View (Vertical)
				deltaPixels = currentPos.Y - _waveformDragStartPoint.Y;
				// 1 row = TrackerRowHeight
				// rows per beat = TrackerLinesPerBeat (4)
				// 1 beat = 4 * TrackerRowHeight
				int currentBpm = _timeSignatures.Count > 0 ? GetTempoAtRow(0).Bpm : 120;
				pixelsPerSecond = (TrackerRowHeight * TrackerLinesPerBeat) / (60.0 / currentBpm);
			}

			double deltaSeconds = deltaPixels / pixelsPerSecond;
			double newOffset = _waveformDragStartOffset + deltaSeconds;
			
			_audioEngine.ReferenceOffsetSeconds = newOffset;
			ReferenceOffsetTextBox.Text = (newOffset * 1000.0).ToString("0");
			
			DrawWaveforms();
		}
	}


	private void WaveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_isDraggingWaveform)
		{
			_isDraggingWaveform = false;
			var canvas = (Canvas)sender;
			canvas.ReleaseMouseCapture();
			
			// Re-seek audio engine to apply offset shift if playing
			if (isPlaying)
			{
				_audioEngine.Seek(_audioEngine.CurrentTimeSeconds);
			}
		}
	}


	private void DrawWaveforms()
	{
		HorizontalWaveformCanvas.Children.Clear();
		VerticalWaveformCanvas.Children.Clear();

		if (_referenceWaveformPeaks == null || _referenceWaveformPeaks.Length == 0) return;
		
		int currentBpm = _timeSignatures.Count > 0 ? GetTempoAtRow(0).Bpm : 120;
		double pixelsPerSecondHorizontal = BeatWidth / (60.0 / currentBpm);
		double pixelsPerSecondVertical = (TrackerRowHeight * TrackerLinesPerBeat) / (60.0 / currentBpm);

		// The peak array was created with 1024 samples per peak.
		// At 44100 Hz, 1 peak = 1024 / 44100 = 0.0232 seconds.
		double secondsPerPeak = 1024.0 / 44100.0;
		
		double offsetSeconds = _audioEngine != null ? _audioEngine.ReferenceOffsetSeconds : 0;
		
		// Draw Horizontal
		double offsetXHorizontal = offsetSeconds * pixelsPerSecondHorizontal;
		Polyline horizPoly = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)), StrokeThickness = 1 };
		for (int i = 0; i < _referenceWaveformPeaks.Length; i++)
		{
			double time = i * secondsPerPeak;
			double x = offsetXHorizontal + (time * pixelsPerSecondHorizontal);
			double y = 40 - (_referenceWaveformPeaks[i] * 40); // 40 is height
			horizPoly.Points.Add(new Point(x, y));
		}
		
		// Fill bottom
		horizPoly.Points.Add(new Point(horizPoly.Points.Last().X, 40));
		horizPoly.Points.Add(new Point(horizPoly.Points.First().X, 40));
		
		Polygon horizFill = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(100, 0, 122, 204)) };
		foreach(var pt in horizPoly.Points) horizFill.Points.Add(pt);

		HorizontalWaveformCanvas.Children.Add(horizFill);
		HorizontalWaveformCanvas.Children.Add(horizPoly);
		HorizontalWaveformCanvas.Width = Math.Max(0, horizPoly.Points.Max(p => p.X));

		// Draw Vertical
		double offsetYVertical = offsetSeconds * pixelsPerSecondVertical;
		Polyline vertPoly = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)), StrokeThickness = 1 };
		for (int i = 0; i < _referenceWaveformPeaks.Length; i++)
		{
			double time = i * secondsPerPeak;
			double y = offsetYVertical + (time * pixelsPerSecondVertical);
			double x = (_referenceWaveformPeaks[i] * 60); // 60 is width
			vertPoly.Points.Add(new Point(x, y));
		}
		
		vertPoly.Points.Add(new Point(0, vertPoly.Points.Last().Y));
		vertPoly.Points.Add(new Point(0, vertPoly.Points.First().Y));

		Polygon vertFill = new Polygon { Fill = new SolidColorBrush(Color.FromArgb(100, 0, 122, 204)) };
		foreach(var pt in vertPoly.Points) vertFill.Points.Add(pt);

		VerticalWaveformCanvas.Children.Add(vertFill);
		VerticalWaveformCanvas.Children.Add(vertPoly);
		VerticalWaveformCanvas.Height = Math.Max(0, vertPoly.Points.Max(p => p.Y));
	}

    }
}
