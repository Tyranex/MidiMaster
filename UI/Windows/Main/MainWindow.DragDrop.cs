using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Media.Animation;
using Melanchall.DryWetMidi.Core;

namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void MoveListElement<T>(List<T> list, int source, int dest)
	{
		if (source < 0 || source >= list.Count || dest < 0 || dest >= list.Count || source == dest) return;
		T item = list[source];
		list.RemoveAt(source);
		if (source < dest) dest--;
		list.Insert(dest, item);
	}


	private void AnimateTrackMargin(Border track, Thickness targetMargin)
	{
		var currentMargin = track.Margin;
		if (currentMargin == targetMargin) return;

		var anim = new System.Windows.Media.Animation.ThicknessAnimation
		{
			To = targetMargin,
			Duration = TimeSpan.FromMilliseconds(150),
			EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
		};
		track.BeginAnimation(FrameworkElement.MarginProperty, anim);
	}


	private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_isDraggingTrack && _draggedGhostElement != null)
		{
			Point mousePos = e.GetPosition(DragDropOverlayCanvas);
			Canvas.SetLeft(_draggedGhostElement, mousePos.X - _dragStartPoint.X);
			Canvas.SetTop(_draggedGhostElement, mousePos.Y - _dragStartPoint.Y);

			// Hit test to find track underneath
			int newHoverIndex = -1;
			bool topHalf = true;
			for (int i = 0; i < TrackListPanel.Children.Count; i++)
			{
				if (TrackListPanel.Children[i] is Border b && b.Visibility == Visibility.Visible)
				{
					Rect bounds = new Rect(0, 0, b.ActualWidth, b.ActualHeight);
					Point p = e.GetPosition(b);
					if (bounds.Contains(p))
					{
						newHoverIndex = i;
						topHalf = p.Y < b.ActualHeight / 2;
						break;
					}
				}
			}
			
			int insertIndex = -1;
			if (newHoverIndex != -1)
			{
				insertIndex = topHalf ? newHoverIndex : newHoverIndex + 1;
			}
			
			// Debounce animation: don't restart animations if the insertion index hasn't changed
			if (insertIndex == _currentHoverIndex)
			{
				e.Handled = true;
				return;
			}
			_currentHoverIndex = insertIndex;

			int lastVisibleIndex = -1;
			for (int i = TrackListPanel.Children.Count - 1; i >= 0; i--)
			{
				if (TrackListPanel.Children[i] is Border b && b.Visibility == Visibility.Visible)
				{
					lastVisibleIndex = i;
					break;
				}
			}

			for (int i = 0; i < TrackListPanel.Children.Count; i++)
			{
				if (TrackListPanel.Children[i] is Border b && b.Visibility == Visibility.Visible)
				{
					Thickness targetMargin = new Thickness(0);
					if (insertIndex != -1)
					{
						if (i == insertIndex)
						{
							targetMargin = new Thickness(0, 40, 0, 0);
						}
						else if (insertIndex > lastVisibleIndex && i == lastVisibleIndex)
						{
							targetMargin = new Thickness(0, 0, 0, 40);
						}
					}
					AnimateTrackMargin(b, targetMargin);
				}
			}
			
			e.Handled = true;
		}
	}


	private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_isDraggingTrack)
		{
			_isDraggingTrack = false;
			Mouse.Capture(null);

			if (_draggedGhostElement != null)
			{
				DragDropOverlayCanvas.Children.Remove(_draggedGhostElement);
				_draggedGhostElement = null;
			}

			TrackerDimOverlay.Visibility = Visibility.Collapsed;

			int destIndex = _currentHoverIndex;
			
			// Reset visibility and margins
			for (int i = 0; i < TrackListPanel.Children.Count; i++)
			{
				if (TrackListPanel.Children[i] is Border b)
				{
					b.BeginAnimation(FrameworkElement.MarginProperty, null);
					b.Margin = new Thickness(0);
					b.Visibility = Visibility.Visible;
				}
			}

			if (destIndex != -1)
			{
				// Cap destIndex
				if (destIndex >= TrackListPanel.Children.Count) 
					destIndex = TrackListPanel.Children.Count - 1;
					
				if (destIndex != _draggedTrackOriginalIndex)
				{
					MoveTrack(_draggedTrackOriginalIndex, destIndex);
				}
			}

			_currentHoverIndex = -1;
			e.Handled = true;
		}
	}

    }
}
