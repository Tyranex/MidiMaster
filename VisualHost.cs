using System;
using System.Windows;
using System.Windows.Media;

namespace SS14_MIDI_IDE;

public class VisualHost : FrameworkElement
{
	private DrawingVisual _visual;

	public DrawingVisual Visual
	{
		get
		{
			return _visual;
		}
		set
		{
			if (_visual != null)
			{
				RemoveVisualChild(_visual);
			}
			_visual = value;
			if (_visual != null)
			{
				AddVisualChild(_visual);
			}
			InvalidateVisual();
		}
	}

	protected override int VisualChildrenCount => (_visual != null) ? 1 : 0;

	protected override Visual GetVisualChild(int index)
	{
		if (_visual == null || index != 0)
		{
			throw new ArgumentOutOfRangeException("index");
		}
		return _visual;
	}
}
