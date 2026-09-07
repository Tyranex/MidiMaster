
#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Linq;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Microsoft.Win32;
using NAudio.Wave;
using NVorbis;

namespace SS14_MIDI_IDE;

public class NoteElement : Border
{
	public NoteElement()
	{
		Cursor = Cursors.Hand;
		ClipToBounds = true;
	}
}

public partial class MainWindow : Window
{
	[DllImport("dwmapi.dll", PreserveSig = true)]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	public static void ApplyDarkTitleBar(Window window)
	{
		if (window == null) return;
		if (!OperatingSystem.IsWindows()) return;

		void Apply(IntPtr handle)
		{
			if (handle == IntPtr.Zero) return;
			try
			{
				int useDarkMode = 1;
				if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
				{
					DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
				}
			}
			catch { /* Ignore on platforms/wine where DWM isn't available */ }
		}

		if (window.IsLoaded)
		{
			var helper = new WindowInteropHelper(window);
			Apply(helper.Handle);
		}
		else
		{
			window.SourceInitialized += (s, ev) =>
			{
				var helper = new WindowInteropHelper(window);
				Apply(helper.Handle);
			};
		}
	}

	public Window CreateCustomDialogWindow(string title, double width, double height, UIElement content, bool allowResize = false)
	{
		Window dialog = new Window
		{
			Title = title,
			Width = width,
			Height = height,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Owner = this,
			ShowInTaskbar = false,
			WindowStyle = WindowStyle.None,
			Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
			Foreground = Brushes.White,
			ResizeMode = allowResize ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize
		};

		var chrome = new System.Windows.Shell.WindowChrome
		{
			CaptionHeight = 28,
			ResizeBorderThickness = allowResize ? new Thickness(6) : new Thickness(0),
			GlassFrameThickness = new Thickness(0),
			CornerRadius = new CornerRadius(0)
		};
		System.Windows.Shell.WindowChrome.SetWindowChrome(dialog, chrome);

		Grid rootGrid = new Grid();
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
		rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

		// Custom Dialog Title Bar
		Grid titleBar = new Grid { Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)) };
		titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		TextBlock titleBlock = new TextBlock
		{
			Text = title,
			Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
			FontSize = 11,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10, 0, 0, 0)
		};
		Grid.SetColumn(titleBlock, 0);
		titleBar.Children.Add(titleBlock);

        // Close button with shared style
        Button closeBtn = new Button
        {
            Content = "✕",
            Width = 36,
            Height = 28,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Style = (Style)FindResource("DialogCloseButtonStyle")
        };
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
        closeBtn.Click += (s, ev) => dialog.Close();
        closeBtn.MouseEnter += (s, e) => closeBtn.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)); // red on hover
        closeBtn.MouseLeave += (s, e) => closeBtn.Background = Brushes.Transparent;

		Grid.SetColumn(closeBtn, 1);
		titleBar.Children.Add(closeBtn);

		Grid.SetRow(titleBar, 0);
		rootGrid.Children.Add(titleBar);

		// Dialog Content Container with a thin border
		Border contentBorder = new Border
		{
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			BorderThickness = new Thickness(1, 0, 1, 1),
			Child = content
		};
		Grid.SetRow(contentBorder, 1);
		rootGrid.Children.Add(contentBorder);

		dialog.Content = rootGrid;
		return dialog;
	}

	private const int KeyHeight = 20;

	private int BeatWidth;

	private const int TotalKeys = 128;

	private double _maxBeats;

	private List<double> _trackXOffsets;
	private List<int> _trackerAbsoluteIndices = new List<int>();
	private List<int> _trackerCanvasIndices = new List<int>();
	
	private List<Color> _trackColors = new List<Color>();

	private List<int> _trackPolyphonies;
	private List<int> _trackPolyphonyOffsets = new List<int>();

	private bool _isDraggingVelocity;

	private Note _currentVelocityNote = default!;

	private Point _velocityDragStartPoint;

	private int _velocityDragStartValue;

	private const int TrackerRowHeight = 14;

	private const int TrackerColWidth = 70;

	private const int TrackerLinesPerBeat = 4;



	private NoteElement currentNote = default!;

	private Line playheadLine = default!;
	
	private Polygon playheadTriangle = default!;

	private DispatcherTimer playbackTimer = default!;

	private Stopwatch stopwatch = default!;

	private bool isDraggingNote;

	private Point clickPosition;

	private bool isDragSelecting;

	private Point selectionStartPoint;

	private Rectangle selectionBox = default!;

	private List<NoteElement> selectedNotes;

	private bool isPlaying;

	private double currentPlayheadX;

	private bool isMetronomeEnabled;

	private int lastBeatIndex;
	private string[] InstrumentNames;
	private MidiFile _loadedMidi = default!;
	private AudioEngine _audioEngine;
	private List<Canvas> _trackCanvases;
	private List<bool> _trackMuted;
	private List<int> _visualToAbsoluteTrackIndices = new List<int>();
	private List<bool> _trackShowFx = new List<bool>();
	private List<System.Windows.Controls.Primitives.ToggleButton> _speakerButtons = new List<System.Windows.Controls.Primitives.ToggleButton>();
	private List<System.Windows.Controls.Primitives.ToggleButton> _eyeButtons = new List<System.Windows.Controls.Primitives.ToggleButton>();
	private List<bool>? _preSoloMutedStates = null;
	private HashSet<int> _soloedAudioTracks = new HashSet<int>();
	private List<bool>? _preSoloHiddenStates = null;
	private HashSet<int> _soloedVisualTracks = new HashSet<int>();
	private bool _isUpdatingMuteUI = false;
	private bool _isUpdatingVisualUI = false;
	private List<ClipboardCell> _trackerClipboardCells = new List<ClipboardCell>();

	private DispatcherTimer _midiSaveTimer;
	private List<int> _trackChannels = new List<int>();
	
	// Caching parsed notes for performance
	private List<List<Note>> _cachedNotes = new List<List<Note>>();
	private Dictionary<int, VisualHost> _trackerVisualHosts = new Dictionary<int, VisualHost>();

	private bool _isDraggingTrackerSelection;

	private List<Note> _clipboardNotes = new List<Note>();
	public UndoRedoManager UndoManager { get; } = new UndoRedoManager();

	// Track dragging state
	private bool _isDraggingTrack = false;
	private int _draggedTrackOriginalIndex = -1;
	private int _currentHoverIndex = -1;
	private Border _draggedGhostElement = null;
	private Point _dragStartPoint;

	public struct TimeSignaturePoint
	{
		public long MetricTime; // In MIDI ticks
		public double BeatTime; // In quarter note beats
		public int Numerator;
		public int Denominator;
		public int RowsPerMeasure => Numerator * 16 / Denominator; // 16 rows per whole note (4 rows per beat)
		public double BeatLength => Numerator * 4.0 / Denominator;
	}

	public struct TempoPoint
	{
		public long MetricTime; // In MIDI ticks
		public double BeatTime; // In quarter note beats
		public int Bpm;
	}

	private int _timeSignatureNumerator = 4;
	private int _timeSignatureDenominator = 4;
	private List<TimeSignaturePoint> _timeSignatures = new List<TimeSignaturePoint>();
	private List<TempoPoint> _tempoChanges = new List<TempoPoint>();

	private void ParseTimeSignatures()
	{
		_timeSignatures.Clear();
		_tempoChanges.Clear();
		if (_loadedMidi == null)
		{
			_timeSignatures.Add(new TimeSignaturePoint { MetricTime = 0, BeatTime = 0, Numerator = _timeSignatureNumerator, Denominator = _timeSignatureDenominator });
			int defBpm = 120;
			if (int.TryParse(TempoTextBox?.Text, out var bVal)) defBpm = bVal;
			_tempoChanges.Add(new TempoPoint { MetricTime = 0, BeatTime = 0, Bpm = defBpm });
			UpdateTimeSignatureUI();
			return;
		}

		short tpqn = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNote)
		{
			tpqn = ticksPerQuarterNote.TicksPerQuarterNote;
		}

		var tsEvents = new List<Tuple<long, TimeSignatureEvent>>();
		var tempoEvents = new List<Tuple<long, SetTempoEvent>>();
		foreach (var chunk in _loadedMidi.GetTrackChunks())
		{
			long absTime = 0;
			foreach (var e in chunk.Events)
			{
				absTime += e.DeltaTime;
				if (e is TimeSignatureEvent tse)
				{
					tsEvents.Add(Tuple.Create(absTime, tse));
				}
				else if (e is SetTempoEvent ste)
				{
					tempoEvents.Add(Tuple.Create(absTime, ste));
				}
			}
		}

		tsEvents = tsEvents.OrderBy(t => t.Item1).ToList();
		tempoEvents = tempoEvents.OrderBy(t => t.Item1).ToList();

		if (tsEvents.Count == 0)
		{
			_timeSignatures.Add(new TimeSignaturePoint { MetricTime = 0, BeatTime = 0, Numerator = 4, Denominator = 4 });
		}
		else
		{
			int lastRow = -1;
			foreach (var item in tsEvents)
			{
				double bTime = (double)item.Item1 / tpqn;
				int row = (int)Math.Round(bTime * 4.0);
				if (row == lastRow && _timeSignatures.Count > 0)
				{
					_timeSignatures[_timeSignatures.Count - 1] = new TimeSignaturePoint
					{
						MetricTime = item.Item1,
						BeatTime = bTime,
						Numerator = item.Item2.Numerator,
						Denominator = item.Item2.Denominator
					};
				}
				else
				{
					_timeSignatures.Add(new TimeSignaturePoint
					{
						MetricTime = item.Item1,
						BeatTime = bTime,
						Numerator = item.Item2.Numerator,
						Denominator = item.Item2.Denominator
					});
					lastRow = row;
				}
			}

			if (_timeSignatures.Count == 0)
			{
				_timeSignatures.Add(new TimeSignaturePoint
				{
					MetricTime = 0,
					BeatTime = 0,
					Numerator = 4,
					Denominator = 4
				});
			}
			else if (_timeSignatures[0].MetricTime > 0)
			{
				_timeSignatures.Insert(0, new TimeSignaturePoint
				{
					MetricTime = 0,
					BeatTime = 0,
					Numerator = 4,
					Denominator = 4
				});
			}

			_timeSignatureNumerator = _timeSignatures[0].Numerator;
			_timeSignatureDenominator = _timeSignatures[0].Denominator;
		}

		if (tempoEvents.Count == 0)
		{
			int defBpm = 120;
			if (int.TryParse(TempoTextBox?.Text, out var bVal)) defBpm = bVal;
			_tempoChanges.Add(new TempoPoint { MetricTime = 0, BeatTime = 0, Bpm = defBpm });
		}
		else
		{
			int lastRow = -1;
			int lastBpm = -1;
			foreach (var item in tempoEvents)
			{
				int bpm = (int)Math.Round(60000000.0 / item.Item2.MicrosecondsPerQuarterNote);
				double bTime = (double)item.Item1 / tpqn;
				int row = (int)Math.Round(bTime * 4.0);

				if (bpm == lastBpm && row != 0) continue; // Skip identical consecutive tempos

				if (row == lastRow && _tempoChanges.Count > 0)
				{
					_tempoChanges[_tempoChanges.Count - 1] = new TempoPoint
					{
						MetricTime = item.Item1,
						BeatTime = bTime,
						Bpm = bpm
					};
					lastBpm = bpm;
				}
				else
				{
					_tempoChanges.Add(new TempoPoint
					{
						MetricTime = item.Item1,
						BeatTime = bTime,
						Bpm = bpm
					});
					lastRow = row;
					lastBpm = bpm;
				}
			}

			if (_tempoChanges.Count > 0 && _tempoChanges[0].MetricTime > 0)
			{
				_tempoChanges.Insert(0, new TempoPoint
				{
					MetricTime = 0,
					BeatTime = 0,
					Bpm = _tempoChanges[0].Bpm
				});
			}

			if (_tempoChanges.Count > 0 && TempoTextBox != null)
			{
				TempoTextBox.Text = _tempoChanges[0].Bpm.ToString();
			}
		}

		UpdateTimeSignatureUI();
	}

	public TempoPoint GetTempoAtBeat(double beat)
	{
		if (_tempoChanges == null || _tempoChanges.Count == 0)
		{
			int bpm = 120;
			if (int.TryParse(TempoTextBox?.Text, out var bVal)) bpm = bVal;
			return new TempoPoint { MetricTime = 0, BeatTime = 0, Bpm = bpm };
		}

		TempoPoint result = _tempoChanges[0];
		for (int i = 0; i < _tempoChanges.Count; i++)
		{
			if (_tempoChanges[i].BeatTime <= beat)
			{
				result = _tempoChanges[i];
			}
			else
			{
				break;
			}
		}
		return result;
	}

	public TempoPoint GetTempoAtRow(int row)
	{
		double beat = row / 4.0;
		return GetTempoAtBeat(beat);
	}

	public TimeSignaturePoint GetTimeSignatureAtBeat(double beat)
	{
		if (_timeSignatures == null || _timeSignatures.Count == 0)
		{
			return new TimeSignaturePoint { MetricTime = 0, BeatTime = 0, Numerator = _timeSignatureNumerator, Denominator = _timeSignatureDenominator };
		}

		TimeSignaturePoint result = _timeSignatures[0];
		for (int i = 0; i < _timeSignatures.Count; i++)
		{
			if (_timeSignatures[i].BeatTime <= beat)
			{
				result = _timeSignatures[i];
			}
			else
			{
				break;
			}
		}
		return result;
	}

	public TimeSignaturePoint GetTimeSignatureAtRow(int row)
	{
		double beat = row / 4.0;
		return GetTimeSignatureAtBeat(beat);
	}

	public void GetMeasureBoundsForRow(int row, out int measureStartRow, out int measureRowCount)
	{
		if (_timeSignatures == null || _timeSignatures.Count == 0)
		{
			int defaultRows = _timeSignatureNumerator * 16 / _timeSignatureDenominator;
			if (defaultRows <= 0) defaultRows = 16;
			measureStartRow = (row / defaultRows) * defaultRows;
			measureRowCount = defaultRows;
			return;
		}

		int curStart = 0;
		for (int i = 0; i < _timeSignatures.Count; i++)
		{
			var ts = _timeSignatures[i];
			int segStartRow = (int)Math.Round(ts.BeatTime * 4.0);
			int segEndRow = int.MaxValue;
			if (i + 1 < _timeSignatures.Count)
			{
				segEndRow = (int)Math.Round(_timeSignatures[i + 1].BeatTime * 4.0);
			}

			if (row < segEndRow)
			{
				int rowsPerM = ts.RowsPerMeasure;
				if (rowsPerM <= 0) rowsPerM = 16;

				int offset = row - segStartRow;
				if (offset < 0) offset = 0;
				int mIndex = offset / rowsPerM;
				measureStartRow = segStartRow + (mIndex * rowsPerM);
				measureRowCount = rowsPerM;
				return;
			}
		}

		int fallbackRows = _timeSignatureNumerator * 16 / _timeSignatureDenominator;
		if (fallbackRows <= 0) fallbackRows = 16;
		measureStartRow = (row / fallbackRows) * fallbackRows;
		measureRowCount = fallbackRows;
	}

	public void UpdateTimeSignatureUI()
	{
		if (TimeSignatureButton != null)
		{
			TimeSignatureButton.Content = $"{_timeSignatureNumerator}/{_timeSignatureDenominator}";
		}
	}

	public void SetTimeSignature(int numerator, int denominator, bool insertAtCursor = false)
	{
		_timeSignatureNumerator = Math.Clamp(numerator, 1, 32);
		_timeSignatureDenominator = Math.Clamp(denominator, 1, 32);

		if (_loadedMidi != null)
		{
			short tpqn = 480;
			if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNote)
			{
				tpqn = ticksPerQuarterNote.TicksPerQuarterNote;
			}

			long targetTick = 0;
			if (insertAtCursor && ViewTrackerMenu.IsChecked)
			{
				targetTick = (long)Math.Round((_trackerCursorRow / 4.0) * tpqn);
			}

			var firstChunk = _loadedMidi.GetTrackChunks().FirstOrDefault();
			if (firstChunk != null)
			{
				using (var manager = firstChunk.ManageTimedEvents())
				{
					// If replacing at time 0 or at cursor
					var existing = manager.Objects.FirstOrDefault(e => e.Event is TimeSignatureEvent && Math.Abs(e.Time - targetTick) < (tpqn / 4));
					if (existing != null)
					{
						((TimeSignatureEvent)existing.Event).Numerator = (byte)_timeSignatureNumerator;
						((TimeSignatureEvent)existing.Event).Denominator = (byte)_timeSignatureDenominator;
					}
					else
					{
						manager.Objects.Add(new TimedEvent(new TimeSignatureEvent((byte)_timeSignatureNumerator, (byte)_timeSignatureDenominator), targetTick));
					}
				}
			}

			string tsDesc = insertAtCursor ? $"Set Time Signature to {_timeSignatureNumerator}/{_timeSignatureDenominator} at {GetMeasureBeatString(_trackerCursorRow)}" : $"Set Time Signature to {_timeSignatureNumerator}/{_timeSignatureDenominator}";
			PushUndoState(tsDesc);

			ParseTimeSignatures();
			BuildNoteCache();
			DrawPianoRoll();
			if (ViewTrackerMenu.IsChecked)
			{
				GenerateTrackerView();
			}
			LoadMidiIntoAudioEngine();
		}
		else
		{
			UpdateTimeSignatureUI();
		}
	}

	public void SetBpm(int bpm, bool insertAtCursor = false)
	{
		bpm = Math.Clamp(bpm, 20, 999);

		if (_loadedMidi != null)
		{
			short tpqn = 480;
			if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNote)
			{
				tpqn = ticksPerQuarterNote.TicksPerQuarterNote;
			}

			long targetTick = 0;
			if (insertAtCursor && ViewTrackerMenu.IsChecked)
			{
				targetTick = (long)Math.Round((_trackerCursorRow / 4.0) * tpqn);
			}

			long microsecondsPerQuarterNote = (long)Math.Round(60000000.0 / bpm);
			var firstChunk = _loadedMidi.GetTrackChunks().FirstOrDefault();
			if (firstChunk != null)
			{
				using (var manager = firstChunk.ManageTimedEvents())
				{
					var existing = manager.Objects.FirstOrDefault(e => e.Event is SetTempoEvent && Math.Abs(e.Time - targetTick) < (tpqn / 4));
					if (existing != null)
					{
						((SetTempoEvent)existing.Event).MicrosecondsPerQuarterNote = microsecondsPerQuarterNote;
					}
					else
					{
						manager.Objects.Add(new TimedEvent(new SetTempoEvent(microsecondsPerQuarterNote), targetTick));
					}
				}
			}

			string bpmDesc = insertAtCursor ? $"Set Tempo to {bpm} BPM at {GetMeasureBeatString(_trackerCursorRow)}" : $"Set Tempo to {bpm} BPM";
			PushUndoState(bpmDesc);

			ParseTimeSignatures();
			BuildNoteCache();
			DrawPianoRoll();
			if (ViewTrackerMenu.IsChecked)
			{
				GenerateTrackerView();
			}
			LoadMidiIntoAudioEngine();
		}
		else
		{
			if (TempoTextBox != null)
			{
				TempoTextBox.Text = bpm.ToString();
			}
		}
	}

	public void SetTrackerVibrato(int trackIdx, int row, byte? val)
	{
		if (_loadedMidi == null || trackIdx < 0 || trackIdx >= _loadedMidi.GetTrackChunks().Count())
			return;

		short tpqn = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNote)
		{
			tpqn = ticksPerQuarterNote.TicksPerQuarterNote;
		}

		long targetTick = (long)Math.Round((row / 4.0) * tpqn);
		var trackChunk = _loadedMidi.GetTrackChunks().ElementAt(trackIdx);

		using (var manager = trackChunk.ManageTimedEvents())
		{
			var existing = manager.Objects.FirstOrDefault(e => e.Event is ControlChangeEvent cce && cce.ControlNumber == 1 && Math.Abs(e.Time - targetTick) < (tpqn / 16));
			if (existing != null)
			{
				if (val.HasValue)
				{
					((ControlChangeEvent)existing.Event).ControlValue = (Melanchall.DryWetMidi.Common.SevenBitNumber)val.Value;
				}
				else
				{
					manager.Objects.Remove(existing);
				}
			}
			else if (val.HasValue)
			{
				manager.Objects.Add(new TimedEvent(new ControlChangeEvent((Melanchall.DryWetMidi.Common.SevenBitNumber)1, (Melanchall.DryWetMidi.Common.SevenBitNumber)val.Value), targetTick));
			}
		}

		string desc = val.HasValue ? $"Set Vibrato at {GetMeasureBeatString(row)}" : $"Remove Vibrato at {GetMeasureBeatString(row)}";
		PushUndoState(desc);

		if (ViewTrackerMenu.IsChecked)
		{
			GenerateTrackerView();
		}
		LoadMidiIntoAudioEngine();
	}

	public void SetTrackerSlide(int trackIdx, int row, int? targetNote, double? durationBeats)
	{
		if (_loadedMidi == null || trackIdx < 0 || trackIdx >= _loadedMidi.GetTrackChunks().Count())
			return;

		short tpqn = 480;
		if (_loadedMidi.TimeDivision is TicksPerQuarterNoteTimeDivision ticksPerQuarterNote)
		{
			tpqn = ticksPerQuarterNote.TicksPerQuarterNote;
		}

		long rowTick = (long)Math.Round((row / 4.0) * tpqn);
		var trackChunk = _loadedMidi.GetTrackChunks().ElementAt(trackIdx);

		// First, remove any existing pitch bend slide events at this row
		using (var manager = trackChunk.ManageTimedEvents())
		{
			// Remove old RPN + pitch bend events associated with a slide at this row
			// We tag slide events by looking for clusters starting near rowTick
			var toRemove = new List<TimedEvent>();
			foreach (var te in manager.Objects)
			{
				if (te.Time >= rowTick && te.Time <= rowTick + (long)(((durationBeats ?? 1.0) + 0.5) * tpqn))
				{
					if (te.Event is PitchBendEvent)
					{
						toRemove.Add(te);
					}
					else if (te.Event is ControlChangeEvent cc &&
					         (cc.ControlNumber == 101 || cc.ControlNumber == 100 || cc.ControlNumber == 6 || cc.ControlNumber == 38) &&
					         Math.Abs(te.Time - rowTick) < (tpqn / 8))
					{
						toRemove.Add(te);
					}
				}
			}
			foreach (var r in toRemove) manager.Objects.Remove(r);

			if (targetNote.HasValue)
			{
				// Find the source note at this row
				int sourceNote = -1;
				if (trackIdx < _cachedNotes.Count)
				{
					foreach (var n in _cachedNotes[trackIdx])
					{
						int noteRow = (int)Math.Round(((double)n.Time / tpqn) * 4.0);
						if (noteRow == row)
						{
							sourceNote = (int)n.NoteNumber;
							if (!durationBeats.HasValue)
							{
								// Default duration = remaining note length in beats
								durationBeats = ((double)n.Length / tpqn);
							}
							break;
						}
					}
				}

				if (sourceNote < 0) sourceNote = 60; // fallback
				double duration = durationBeats ?? 1.0;

				int semitoneDist = Math.Abs(targetNote.Value - sourceNote);
				int bendRange = Math.Max(2, semitoneDist + 1); // At least ±2, enough to cover the slide
				if (bendRange > 24) bendRange = 24; // Cap at 2 octaves

				// Insert RPN to set pitch bend range
				manager.Objects.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)101, (SevenBitNumber)0), rowTick));
				manager.Objects.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)100, (SevenBitNumber)0), rowTick));
				manager.Objects.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)6, (SevenBitNumber)Math.Min(bendRange, 127)), rowTick));
				manager.Objects.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)38, (SevenBitNumber)0), rowTick));

				// Generate pitch bend ramp
				int steps = Math.Max(8, (int)(duration * 16)); // ~16 steps per beat for smooth slide
				long durationTicks = (long)(duration * tpqn);
				double semitoneShift = targetNote.Value - sourceNote;

				for (int s = 0; s <= steps; s++)
				{
					double progress = (double)s / steps;
					double currentSemitones = semitoneShift * progress;
					// Convert semitones to pitch bend value: center=8192, range maps to ±bendRange semitones
					double bendNormalized = currentSemitones / bendRange; // -1.0 to 1.0
					int bendValue = (int)(8192 + bendNormalized * 8191);
					bendValue = Math.Clamp(bendValue, 0, 16383);

					long eventTick = rowTick + (long)(progress * durationTicks);
					manager.Objects.Add(new TimedEvent(new PitchBendEvent((ushort)bendValue), eventTick));
				}

				// Reset pitch bend after slide
				long resetTick = rowTick + durationTicks + 1;
				manager.Objects.Add(new TimedEvent(new PitchBendEvent(8192), resetTick));

				// Store slide metadata
				if (!_slideData.ContainsKey(trackIdx))
					_slideData[trackIdx] = new Dictionary<int, (int, double)>();
				_slideData[trackIdx][row] = (targetNote.Value, duration);
			}
			else
			{
				// Remove slide metadata
				if (_slideData.ContainsKey(trackIdx))
					_slideData[trackIdx].Remove(row);

				// Reset pitch bend to center at this row
				manager.Objects.Add(new TimedEvent(new PitchBendEvent(8192), rowTick));
			}
		}

		string desc = targetNote.HasValue
			? $"Set Note Slide to {GetNoteName(targetNote.Value)} at {GetMeasureBeatString(row)}"
			: $"Remove Note Slide at {GetMeasureBeatString(row)}";
		PushUndoState(desc);

		if (ViewTrackerMenu.IsChecked)
		{
			GenerateTrackerView();
		}
		LoadMidiIntoAudioEngine();
	}

	public void OpenSlideEditDialog(int trackIdx, int row)
	{
		Grid grid = new Grid { Margin = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		TextBlock prompt = new TextBlock
		{
			Text = "Set Note Slide:",
			Foreground = Brushes.LightGray,
			FontSize = 13,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 12)
		};
		Grid.SetRow(prompt, 0);
		grid.Children.Add(prompt);

		// Target Note row
		StackPanel notePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
		TextBlock noteLabel = new TextBlock { Text = "Target Note: ", Foreground = Brushes.LightGray, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

		string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
		ComboBox noteCombo = new ComboBox
		{
			Width = 55,
			Height = 26,
			FontSize = 12,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66))
		};
		foreach (var n in noteNames) noteCombo.Items.Add(n);
		noteCombo.SelectedIndex = 0; // C

		ComboBox octaveCombo = new ComboBox
		{
			Width = 45,
			Height = 26,
			FontSize = 12,
			Margin = new Thickness(4, 0, 0, 0),
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66))
		};
		for (int oct = -1; oct <= 9; oct++) octaveCombo.Items.Add(oct.ToString());
		octaveCombo.SelectedIndex = 5; // Octave 4

		// Try to default to source note + 1 semitone if we can find the note at this row
		short tpqn = 480;
		if (_loadedMidi?.TimeDivision is TicksPerQuarterNoteTimeDivision td) tpqn = td.TicksPerQuarterNote;
		int defaultTarget = 60;
		double defaultDuration = 1.0;
		if (trackIdx < _cachedNotes.Count)
		{
			foreach (var n in _cachedNotes[trackIdx])
			{
				int noteRow = (int)Math.Round(((double)n.Time / tpqn) * 4.0);
				if (noteRow == row)
				{
					defaultTarget = Math.Min(127, (int)n.NoteNumber + 2);
					defaultDuration = Math.Max(0.25, (double)n.Length / tpqn);
					break;
				}
			}
		}

		// If there's existing slide data, use it
		if (_slideData.ContainsKey(trackIdx) && _slideData[trackIdx].ContainsKey(row))
		{
			var existing = _slideData[trackIdx][row];
			defaultTarget = existing.TargetNote;
			defaultDuration = existing.DurationBeats;
		}

		noteCombo.SelectedIndex = defaultTarget % 12;
		octaveCombo.SelectedIndex = (defaultTarget / 12 - 1) + 1; // offset by 1 since index 0 = octave -1
		if (octaveCombo.SelectedIndex < 0) octaveCombo.SelectedIndex = 0;
		if (octaveCombo.SelectedIndex >= octaveCombo.Items.Count) octaveCombo.SelectedIndex = octaveCombo.Items.Count - 1;

		notePanel.Children.Add(noteLabel);
		notePanel.Children.Add(noteCombo);
		notePanel.Children.Add(octaveCombo);
		Grid.SetRow(notePanel, 1);
		grid.Children.Add(notePanel);

		// Duration row
		StackPanel durPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
		TextBlock durLabel = new TextBlock { Text = "Duration (beats): ", Foreground = Brushes.LightGray, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
		TextBox durBox = new TextBox
		{
			Text = defaultDuration.ToString("F2"),
			Width = 60,
			Height = 26,
			FontSize = 12,
			FontWeight = FontWeights.Bold,
			TextAlignment = TextAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			VerticalContentAlignment = VerticalAlignment.Center
		};
		durPanel.Children.Add(durLabel);
		durPanel.Children.Add(durBox);
		Grid.SetRow(durPanel, 2);
		grid.Children.Add(durPanel);

		// Buttons
		StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
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
		Button clearBtn = new Button
		{
			Content = "Remove",
			Width = 75,
			Height = 26,
			Margin = new Thickness(0, 0, 8, 0),
			Background = new SolidColorBrush(Color.FromRgb(150, 50, 50)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand
		};

		var slideWindow = CreateCustomDialogWindow("Edit Note Slide", 320, 240, grid);

		okBtn.Click += (s, ev) =>
		{
			int noteIdx = noteCombo.SelectedIndex;
			int octIdx = octaveCombo.SelectedIndex;
			if (noteIdx < 0 || octIdx < 0) return;

			int midiNote = (octIdx - 1 + 1) * 12 + noteIdx; // octave -1 starts at MIDI 0
			midiNote = Math.Clamp(midiNote, 0, 127);

			if (double.TryParse(durBox.Text, out double dur) && dur >= 0.0625 && dur <= 64.0)
			{
				SetTrackerSlide(trackIdx, row, midiNote, dur);
				slideWindow.DialogResult = true;
				slideWindow.Close();
				return;
			}
			SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Please enter a valid duration between 0.0625 and 64 beats.", "Invalid Duration", MessageBoxButton.OK, MessageBoxImage.Warning);
		};

		clearBtn.Click += (s, ev) =>
		{
			SetTrackerSlide(trackIdx, row, null, null);
			slideWindow.DialogResult = true;
			slideWindow.Close();
		};

		cancelBtn.Click += (s, ev) => slideWindow.Close();

		btnPanel.Children.Add(clearBtn);
		btnPanel.Children.Add(okBtn);
		btnPanel.Children.Add(cancelBtn);
		Grid.SetRow(btnPanel, 5);
		grid.Children.Add(btnPanel);

		slideWindow.ShowDialog();
	}

	private short _ticksPerQuarterNote;
	private int _mothClickCount;
	private DateTime _lastMothClick;
	private bool _mothExploded;
	private BitmapSource _mothForward;
	private BitmapSource _mothLeft;
	private BitmapSource _mothRight;
	private BitmapSource _mothDead;

	private readonly Dictionary<Key, int> _keyboardToNoteOffset;
	private readonly Dictionary<Key, int> _keyboardToDrumMap;

	private bool _editMode = false;
	private Rectangle _editModeTintOverlay;

	private int _currentOctave;

	public MainWindow()
	{
		BeatWidth = 40;
		_maxBeats = 0.0;
		_trackXOffsets = new List<double>();
		_trackPolyphonies = new List<int>();
		_isDraggingVelocity = false;
		_currentVelocityNote = null;
		_velocityDragStartValue = 0;


		playheadLine = null;
		playheadTriangle = null;
		playbackTimer = new DispatcherTimer();
		stopwatch = new Stopwatch();
		isDraggingNote = false;
		isDragSelecting = false;
		selectionBox = null;
		selectedNotes = new List<NoteElement>();
		isPlaying = false;
		currentPlayheadX = 0.0;
		isMetronomeEnabled = false;
		lastBeatIndex = -1;
		InstrumentNames = null;
		_loadedMidi = null;
		_audioEngine = null;
		_trackCanvases = new List<Canvas>();
		_trackMuted = new List<bool>();
		_trackShowFx = new List<bool>();
		_activeTrackIndex = 0;
		_trackerCursorRow = 0;
		_trackLeds = new List<Rectangle>();
		_trackVuLevels = new List<double>();
		_activeTrackNotes = new List<List<Note>>();
		_ticksPerQuarterNote = 480;
		_mothClickCount = 0;
		_lastMothClick = DateTime.MinValue;
		_mothExploded = false;
		_mothForward = null;
		_mothLeft = null;
		_mothRight = null;
		_mothDead = null;
		_keyboardToDrumMap = new Dictionary<Key, int>
		{
			{ Key.Z, 35 }, { Key.X, 38 }, { Key.C, 42 }, { Key.V, 46 },
			{ Key.B, 49 }, { Key.N, 51 }, { Key.A, 41 }, { Key.S, 45 },
			{ Key.D, 50 }, { Key.Q, 39 }, { Key.W, 56 }, { Key.E, 54 }
		};
		_keyboardToNoteOffset = new Dictionary<Key, int>
		{
			{ (Key)69, 0 },
			{ (Key)62, 1 },
			{ (Key)67, 2 },
			{ (Key)47, 3 },
			{ (Key)46, 4 },
			{ (Key)65, 5 },
			{ (Key)50, 6 },
			{ (Key)45, 7 },
			{ (Key)51, 8 },
			{ (Key)57, 9 },
			{ (Key)53, 10 },
			{ (Key)56, 11 },
			{ (Key)142, 12 },
			{ (Key)55, 13 },
			{ (Key)144, 14 },
			{ (Key)140, 15 },
			{ (Key)145, 16 },
			{ (Key)60, 12 },
			{ (Key)36, 13 },
			{ (Key)66, 14 },
			{ (Key)37, 15 },
			{ (Key)48, 16 },
			{ (Key)61, 17 },
			{ (Key)39, 18 },
			{ (Key)63, 19 },
			{ (Key)40, 20 },
			{ (Key)68, 21 },
			{ (Key)41, 22 },
			{ (Key)64, 23 },
			{ (Key)52, 24 },
			{ (Key)43, 25 },
			{ (Key)58, 26 },
			{ (Key)34, 27 },
			{ (Key)59, 28 }
		};
		_currentOctave = 4;
		InitializeComponent();
		_ = UpdateChecker.CheckForUpdatesAsync(false);
		base.Loaded += MainWindow_Loaded;
		base.Closing += MainWindow_Closing;

		try
		{
			PreferencesManager.Load();
			var sf2Stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SS14_MIDI_IDE.GeneralUser-GS.sf2");
			_audioEngine = new AudioEngine(sf2Stream, 44100);
			InstrumentNames = _audioEngine.GetInstrumentNames();
		}
		catch (Exception ex)
		{
			System.IO.File.WriteAllText("crash_init.log", ex.ToString());
			throw;
		}

		if (InstrumentNames == null || InstrumentNames.Length == 0)
		{
			InstrumentNames = (from i in Enumerable.Range(0, 132)
				select $"Instrument {i}").ToArray();
		}
		for (int num = 0; num < InstrumentNames.Length; num++)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = $"{num}: {InstrumentNames[num]}",
				Tag = num
			};
			menuItem.Click += AddTrackMenuItem_Click;
			AddTrackMenuItem.Items.Add(menuItem);
		}
		var source = new BitmapImage();
		source.BeginInit();
		source.StreamSource = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SS14_MIDI_IDE.mothroach.png");
		source.EndInit();
		_mothForward = new CroppedBitmap(source, new Int32Rect(0, 0, 32, 32));
		_mothLeft = new CroppedBitmap(source, new Int32Rect(0, 32, 32, 32));
		_mothRight = new CroppedBitmap(source, new Int32Rect(32, 32, 32, 32));
		_mothDead = new CroppedBitmap(source, new Int32Rect(32, 0, 32, 32));
		playbackTimer.Tick += PlaybackTimer_Tick;
		_midiSaveTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		_midiSaveTimer.Tick += MidiSaveTimer_Tick;

		_editModeTintOverlay = new Rectangle
		{
			Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0)),
			IsHitTestVisible = false
		};

		UpdateTimeSignatureUI();

		stopwatch = new Stopwatch();
	}

	private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
	{
		await UpdateChecker.CheckForUpdatesAsync(true);
	}


	private void BuildNoteCache()
	{
		_cachedNotes.Clear();
		if (_loadedMidi == null) return;

		foreach (var chunk in _loadedMidi.GetTrackChunks())
		{
			_cachedNotes.Add(chunk.GetNotes().ToList());
		}
	}

	private void MidiSaveTimer_Tick(object sender, EventArgs e)
	{
		_midiSaveTimer.Stop();
		LoadMidiIntoAudioEngine();
	}

	private MemoryStream GenerateMetronomeStream()
	{
		var metronomeMidi = _loadedMidi.Clone();
		foreach (var trackChunk in metronomeMidi.GetTrackChunks())
		{
			trackChunk.Events.RemoveAll(e => e.EventType == MidiEventType.NoteOn || e.EventType == MidiEventType.NoteOff);
		}

		int maxBeat = (int)Math.Ceiling(_maxBeats);
		TrackChunk metronomeTrack = new TrackChunk();
		using (var manager = metronomeTrack.ManageNotes())
		{
			for (int i = 0; i <= maxBeat; i++)
			{
				int curRow = i * 4;
				GetMeasureBoundsForRow(curRow, out int mStart, out int _);
				bool isDownbeat = (curRow == mStart);
				int clickNote = isDownbeat ? 76 : 77;
				int velocity = isDownbeat ? 127 : 100;

				long tick = i * _ticksPerQuarterNote;
				manager.Objects.Add(new Note((Melanchall.DryWetMidi.Common.SevenBitNumber)clickNote, 100, tick) { Channel = (Melanchall.DryWetMidi.Common.FourBitNumber)9, Velocity = (Melanchall.DryWetMidi.Common.SevenBitNumber)velocity });
			}
		}
		metronomeMidi.Chunks.Add(metronomeTrack);
		
		MemoryStream ms = new MemoryStream();
		metronomeMidi.Write(ms);
		ms.Position = 0;
		return ms;
	}

	internal void LoadMidiIntoAudioEngine()
	{
		if (_loadedMidi == null) return;
		bool flag = _audioEngine.IsPlaying;
		double currentTimeSeconds = _audioEngine.CurrentTimeSeconds;
		
		var trackStreams = new System.Collections.Generic.List<System.IO.Stream>();
		var trackChunks = _loadedMidi.GetTrackChunks().ToList();

		// Build a synthetic tempo chunk containing all global tempo and time signature events.
		// This prevents notes in Track 0 from being duplicated if Track 0 is a standard note track,
		// and ensures tempo is preserved regardless of track order.
		var tempoChunk = new Melanchall.DryWetMidi.Core.TrackChunk();
		using (var tempoManager = tempoChunk.ManageTimedEvents())
		{
			foreach (var chunk in trackChunks)
			{
				using (var manager = chunk.ManageTimedEvents())
				{
					foreach (var ev in manager.Objects)
					{
						if (ev.Event is Melanchall.DryWetMidi.Core.SetTempoEvent || 
							ev.Event is Melanchall.DryWetMidi.Core.TimeSignatureEvent)
						{
							tempoManager.Objects.Add(new Melanchall.DryWetMidi.Interaction.TimedEvent(ev.Event.Clone(), ev.Time));
						}
					}
				}
			}
		}
		
		for (int i = 0; i < trackChunks.Count; i++)
		{
			var singleTrackMidi = new Melanchall.DryWetMidi.Core.MidiFile();
			singleTrackMidi.TimeDivision = _loadedMidi.TimeDivision;
			singleTrackMidi.Chunks.Add(tempoChunk.Clone());
			singleTrackMidi.Chunks.Add(trackChunks[i].Clone());
			var ms = new MemoryStream();
			singleTrackMidi.Write(ms);
			ms.Position = 0;
			trackStreams.Add(ms);
		}

		var metronomeStream = GenerateMetronomeStream();
		
		_audioEngine.LoadMidiTracks(trackStreams, metronomeStream);
		
		for (int i = 0; i < trackChunks.Count; i++)
		{
			int visualIndex = _visualToAbsoluteTrackIndices.IndexOf(i);
			if (visualIndex != -1 && visualIndex < _trackMuted.Count)
			{
				_audioEngine.SetTrackMute(i, _trackMuted[visualIndex]);
			}
		}

		if (flag)
		{
			_audioEngine.Seek(currentTimeSeconds);
			_audioEngine.Play();
		}
	}

	private bool IsPercussionTrack(int trackAbsoluteIndex)
	{
		if (_loadedMidi == null) return false;
		var trackChunk = _loadedMidi.GetTrackChunks().ElementAtOrDefault(trackAbsoluteIndex);
		if (trackChunk == null) return false;

		if (trackAbsoluteIndex < _cachedNotes.Count)
		{
			if (_cachedNotes[trackAbsoluteIndex].Any(n => n.Channel == (FourBitNumber)9))
				return true;
		}
		return false;
	}

	private FourBitNumber GetTrackChannel(int trackAbsoluteIndex)
	{
		if (_trackChannels != null && trackAbsoluteIndex >= 0 && trackAbsoluteIndex < _trackChannels.Count)
		{
			return (FourBitNumber)(byte)_trackChannels[trackAbsoluteIndex];
		}
		if (_loadedMidi == null) return (FourBitNumber)0;
		var chunks = _loadedMidi.GetTrackChunks().ToList();
		if (trackAbsoluteIndex >= chunks.Count) return (FourBitNumber)0;

		foreach (var ev in chunks[trackAbsoluteIndex].Events)
		{
			if (ev is ChannelEvent ce)
			{
				return ce.Channel;
			}
		}
		return (FourBitNumber)(byte)(trackAbsoluteIndex % 16 == 9 ? (trackAbsoluteIndex + 1) % 16 : trackAbsoluteIndex % 16);
	}

	private string MidiDrumToTrackerText(int noteNum)
	{
		switch (noteNum)
		{
			case 35: return "KCK"; case 36: return "BD1"; case 37: return "RSK"; case 38: return "SNR";
			case 39: return "HCP"; case 40: return "ESN"; case 41: return "LTF"; case 42: return "CHH";
			case 43: return "HTF"; case 44: return "PHH"; case 45: return "LTM"; case 46: return "OHH";
			case 47: return "LMT"; case 48: return "HMT"; case 49: return "CYC"; case 50: return "HTM";
			case 51: return "RDC"; case 52: return "CHC"; case 53: return "RDB"; case 54: return "TMB";
			case 55: return "SPC"; case 56: return "CWB"; case 57: return "CY2"; case 58: return "VBS";
			case 59: return "RCM"; case 60: return "HBG"; case 61: return "LBG"; case 62: return "HCG";
			case 63: return "OCG"; case 64: return "LCG"; case 65: return "HTM"; case 66: return "LTM";
			case 67: return "HAG"; case 68: return "LAG"; case 69: return "CAB"; case 70: return "MAR";
			default: return "DRM";
		}
	}

	private bool TryGetHexValue(Key key, out int hexValue)
	{
		hexValue = 0;
		if (key >= Key.D0 && key <= Key.D9) { hexValue = key - Key.D0; return true; }
		if (key >= Key.NumPad0 && key <= Key.NumPad9) { hexValue = key - Key.NumPad0; return true; }
		if (key >= Key.A && key <= Key.F) { hexValue = key - Key.A + 10; return true; }
		return false;
	}






	private void AdvancePlayheadOneRow()
	{
		double num = currentPlayheadX / (double)BeatWidth + 0.25;
		double playheadPosition = num * (double)BeatWidth;
		SetPlayheadPosition(playheadPosition);
		if (!isPlaying)
		{
			double seconds = BeatsToSeconds(num);
			_audioEngine.Seek(seconds);
			UpdateVisualPlayhead();
		}
	}

	private void MainWindow_Closing(object? sender, CancelEventArgs e)
	{
		_audioEngine?.Dispose();
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		ApplyDarkTitleBar(this);
		DrawPianoKeys();
		UpdateCanvasWidth();
		CreatePlayhead();

		if (_loadedMidi == null)
		{
			var dummyItem = new MenuItem { Tag = 0 };
			AddTrackMenuItem_Click(dummyItem, null);
		}
	}


	private void CreatePlayhead()
	{
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		playheadLine = new Line
		{
			X1 = 0.0,
			Y1 = 0.0,
			X2 = 0.0,
			Y2 = 2560.0,
			Stroke = new SolidColorBrush(Colors.Red),
			StrokeThickness = 2.0,
			IsHitTestVisible = false
		};
		Panel.SetZIndex(playheadLine, 9999);
		NoteCanvas.Children.Add(playheadLine);
		playheadTriangle = new Polygon
		{
			Points = new PointCollection
			{
				new Point(-5.0, 0.0),
				new Point(5.0, 0.0),
				new Point(0.0, 10.0)
			},
			Fill = new SolidColorBrush(Colors.Red),
			IsHitTestVisible = false
		};
		Canvas.SetLeft(playheadTriangle, 0.0);
		Canvas.SetTop(playheadTriangle, 20.0);
		TimelineCanvas.Children.Add(playheadTriangle);
	}

	private bool IsBlackKey(int midiPitch)
	{
		int num = midiPitch % 12;
		return num == 1 || num == 3 || num == 6 || num == 8 || num == 10;
	}

	private string GetNoteName(int midiPitch)
	{
		string[] array = new string[12]
		{
			"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A",
			"A#", "B"
		};
		int num = midiPitch % 12;
		int value = midiPitch / 12 - 1;
		return $"{array[num]}{value}";
	}

	private NoteElement AddNoteAt(Point pos)
	{
		double length = Math.Floor(pos.X / (double)BeatWidth) * (double)BeatWidth;
		double length2 = Math.Floor(pos.Y / 20.0) * 20.0;
		Color color = Color.FromRgb(0, 122, 204);
		NoteElement rectangle = new NoteElement
		{
			Width = BeatWidth,
			Height = 20.0,
			Background = new SolidColorBrush(color),
			BorderBrush = new SolidColorBrush(Colors.White),
			BorderThickness = new Thickness(0.5)
		};

		Color darkerColor = Color.FromRgb(
			(byte)Math.Max(0, color.R - 50),
			(byte)Math.Max(0, color.G - 50),
			(byte)Math.Max(0, color.B - 50));
		
		int midiPitch = 127 - (int)(length2 / 20.0);
		TextBlock noteText = new TextBlock
		{
			Text = GetNoteName(midiPitch),
			Foreground = new SolidColorBrush(darkerColor),
			FontSize = 10,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(2, 0, 0, 0),
			IsHitTestVisible = false
		};
		rectangle.Child = noteText;

		Canvas.SetLeft(rectangle, length);
		Canvas.SetTop(rectangle, length2);
		NoteCanvas.Children.Add(rectangle);
		SelectNote(rectangle);
		UpdateCanvasWidth();
		return rectangle;
	}

	private IEnumerable<NoteElement> GetAllNotes()
	{
		foreach (UIElement child in NoteCanvas.Children)
		{
			NoteElement rect = child as NoteElement;
			if (rect != null)
			{
				yield return rect;
			}
			else
			{
				if (!(child is Canvas canvas))
				{
					continue;
				}
				foreach (UIElement subChild in canvas.Children)
				{
					NoteElement subRect = subChild as NoteElement;
					if (subRect != null)
					{
						yield return subRect;
					}
				}
			}
		}
	}

	private void SetPlayheadPosition(double x)
	{
		currentPlayheadX = x;
		playheadLine.X1 = x;
		playheadLine.X2 = x;
		Canvas.SetLeft(playheadTriangle, x);
		double num = x / (double)BeatWidth;
		double num2 = Math.Floor(num * 4.0);
		int currentRow = (int)num2;
		double num3 = num2 * 14.0;
		TrackerPlayhead.Margin = new Thickness(0.0, num3, 0.0, 0.0);
		UpdateTrackerCursor();

		GetMeasureBoundsForRow(currentRow, out int measureStartRow, out int measureRowCount);
		double topOverlayHeight = measureStartRow * 14.0;
		double bottomOverlayTop = (measureStartRow + measureRowCount) * 14.0;

		TrackerDarkOverlayTop.Height = Math.Max(0.0, topOverlayHeight);
		TrackerDarkOverlayBottom.Margin = new Thickness(0.0, bottomOverlayTop, 0.0, 0.0);
		double num7 = TrackerBackgroundCanvas.Height;
		if (double.IsNaN(num7) || num7 <= 0.0)
		{
			num7 = 100000.0;
		}
		TrackerDarkOverlayBottom.Height = Math.Max(0.0, num7 - bottomOverlayTop);
	}



	private void MetronomeButton_Click(object sender, RoutedEventArgs e)
	{
		isMetronomeEnabled = MetronomeButton.IsChecked == true;
		MetronomeButton.Content = (isMetronomeEnabled ? "Metronome: ON" : "Metronome: OFF");
		_audioEngine.IsMetronomeEnabled = isMetronomeEnabled;
	}



	public string GetMeasureBeatString(int row)
	{
		GetMeasureBoundsForRow(row, out int measureStartRow, out int measureRowCount);
		int measureNumber = 1;
		int checkRow = 0;
		while (checkRow < measureStartRow)
		{
			GetMeasureBoundsForRow(checkRow, out int s, out int count);
			measureNumber++;
			checkRow = s + count;
		}

		int rowWithinMeasure = row - measureStartRow;
		int beatWithinMeasure = (rowWithinMeasure / 4) + 1;
		int subBeat = (rowWithinMeasure % 4) + 1;
		return $"Measure {measureNumber}, Beat {beatWithinMeasure}.{subBeat}";
	}

	public void PushUndoState(string description)
	{
		if (_loadedMidi == null) return;
		UndoManager.PushState(_loadedMidi, description, _trackerCursorRow, _activeTrackIndex);
		UpdateUndoUI();
	}

	public void PerformUndo()
	{
		var state = UndoManager.Undo();
		if (state != null)
		{
			RestoreMidiState(state);
		}
		UpdateUndoUI();
	}

	public void PerformRedo()
	{
		var state = UndoManager.Redo();
		if (state != null)
		{
			RestoreMidiState(state);
		}
		UpdateUndoUI();
	}

	public void RestoreMidiState(UndoAction action)
	{
		if (action == null || action.MidiState == null) return;

		try
		{
			using (var ms = new MemoryStream(action.MidiState))
			{
				_loadedMidi = MidiFile.Read(ms);
			}

			_trackerCursorRow = Math.Max(0, action.CursorRow);
			_activeTrackIndex = Math.Max(0, action.ActiveTrackIndex);

			ParseTimeSignatures();
			BuildNoteCache();
			DrawPianoRoll();
			if (ViewTrackerMenu.IsChecked)
			{
				GenerateTrackerView();
				SetPlayheadPosition((_trackerCursorRow / 4.0) * BeatWidth);
			}

			LoadMidiIntoAudioEngine();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("Failed to restore MIDI state: " + ex.Message);
		}
	}

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowMaximizeBtn != null)
            {
                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
                bool isMax = WindowState == WindowState.Maximized;
                if (isMax)
                {
                    WindowMaximizeBtn.Content = "\uE923"; // Restore icon (two overlapping squares)
                    WindowMaximizeBtn.ToolTip = "Restore";
                    // Add an 8px margin to compensate for Windows expanding the window off-screen when maximized
                    MainContentGrid.Margin = new Thickness(8);
                    if (chrome != null)
                        chrome.ResizeBorderThickness = new Thickness(0);
                }
                else
                {
                    WindowMaximizeBtn.Content = "\uE922"; // Maximize icon (single square)
                    WindowMaximizeBtn.ToolTip = "Maximize";
                    MainContentGrid.Margin = new Thickness(0);
                    if (chrome != null)
                        chrome.ResizeBorderThickness = new Thickness(6);
                }
            }
        }






	// Preferences are now opened as a dialog window (PreferencesWindow.xaml).
    // Opens the Preferences dialog window











	










	private void UpdateEditModeTint()
	{
		if (_editModeTintOverlay == null) return;

		if (TrackerBackgroundCanvas.Children.Contains(_editModeTintOverlay))
		{
			TrackerBackgroundCanvas.Children.Remove(_editModeTintOverlay);
		}

		if (_editMode)
		{
			_editModeTintOverlay.Width = Math.Max(TrackerBackgroundCanvas.Width, 100000.0);
			_editModeTintOverlay.Height = Math.Max(TrackerBackgroundCanvas.Height, 100000.0);
			TrackerBackgroundCanvas.Children.Add(_editModeTintOverlay);
		}
	}

	private void Mothroach_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_mothExploded)
		{
			return;
		}
		DateTime now = DateTime.Now;
		if ((now - _lastMothClick).TotalSeconds < 0.5)
		{
			_mothClickCount++;
		}
		else
		{
			_mothClickCount = 1;
		}

		if (_mothClickCount >= 10)
		{
			ExplodeMothroach();
			return;
		}
		try
		{
			var oggStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SS14_MIDI_IDE.moth_squeak.ogg");
			VorbisReader vorbisReader = new VorbisReader(oggStream, true);
			VorbisSampleProvider sampleProvider = new VorbisSampleProvider(vorbisReader);
			WaveOut waveOut = new WaveOut();
			waveOut.Init(sampleProvider);
			waveOut.PlaybackStopped += delegate
			{
				waveOut.Dispose();
				vorbisReader.Dispose();
			};
			waveOut.Play();
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Failed to play moth squeak: " + ex.Message);
		}
		// ---------- HEART ANIMATION ----------
		// Get click position relative to HeartCanvas.
		Point pos = e.GetPosition(HeartCanvas);

		var heartText = new TextBlock
		{
			Text = "\u2764", // ❤
			FontSize = 20,
			Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
			FontWeight = FontWeights.Bold,
			RenderTransform = new TranslateTransform(pos.X - 10, pos.Y - 10)
		};
		var fade = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(1.0));
		fade.Completed += (s, ev) => HeartCanvas.Children.Remove(heartText);
		heartText.BeginAnimation(UIElement.OpacityProperty, fade);

		var rise = new System.Windows.Media.Animation.DoubleAnimation(pos.Y - 10, pos.Y - 30, TimeSpan.FromSeconds(1.0));
		((TranslateTransform)heartText.RenderTransform).BeginAnimation(TranslateTransform.YProperty, rise);

		HeartCanvas.Children.Add(heartText);
	}


	private void ExplodeMothroach()
	{
		_mothExploded = true;
		MothroachImage.Source = _mothDead;
		SystemSounds.Hand.Play();
		DoubleAnimation animation = new DoubleAnimation
		{
			To = 10.0,
			Duration = TimeSpan.FromSeconds(0.5)
		};
		DoubleAnimation animation2 = new DoubleAnimation
		{
			To = 10.0,
			Duration = TimeSpan.FromSeconds(0.5)
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation
		{
			To = 0.0,
			Duration = TimeSpan.FromSeconds(0.5)
		};
		doubleAnimation.Completed += delegate
		{
			MothroachImage.Visibility = Visibility.Collapsed;
		};
		MothroachTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
		MothroachTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation2);
		MothroachImage.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	public void UpdateScrollLockUI()
	{
		if (ScrollLockStatusText != null)
		{
			bool isScrollLockOn = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.Scroll);
			ScrollLockStatusText.Visibility = isScrollLockOn ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	public double BeatsToSeconds(double beats)
	{
		if (_tempoChanges == null || _tempoChanges.Count == 0)
		{
			double bpm = 120.0;
			if (int.TryParse(TempoTextBox?.Text, out var bVal)) bpm = bVal;
			return beats * (60.0 / bpm);
		}

		double accumulatedSeconds = 0.0;
		for (int i = 0; i < _tempoChanges.Count; i++)
		{
			var cur = _tempoChanges[i];
			double nextBeat = (i + 1 < _tempoChanges.Count) ? _tempoChanges[i + 1].BeatTime : double.MaxValue;
			
			if (beats <= nextBeat)
			{
				double remainingBeats = beats - cur.BeatTime;
				if (remainingBeats < 0) remainingBeats = 0;
				return accumulatedSeconds + (remainingBeats * (60.0 / cur.Bpm));
			}

			accumulatedSeconds += (nextBeat - cur.BeatTime) * (60.0 / cur.Bpm);
		}
		
		var last = _tempoChanges[_tempoChanges.Count - 1];
		return accumulatedSeconds + ((beats - last.BeatTime) * (60.0 / last.Bpm));
	}

	public double SecondsToBeats(double seconds)
	{
		if (_tempoChanges == null || _tempoChanges.Count == 0)
		{
			double bpm = 120.0;
			if (int.TryParse(TempoTextBox?.Text, out var bVal)) bpm = bVal;
			return seconds * (bpm / 60.0);
		}

		// Iterate tempo segments to integrate elapsed time
		double accumulatedSeconds = 0.0;
		for (int i = 0; i < _tempoChanges.Count; i++)
		{
			var cur = _tempoChanges[i];
			double nextBeat = (i + 1 < _tempoChanges.Count) ? _tempoChanges[i + 1].BeatTime : double.MaxValue;
			double segBeats = nextBeat - cur.BeatTime;
			double segSecs = segBeats * (60.0 / cur.Bpm);

			if (seconds <= accumulatedSeconds + segSecs)
			{
				double remainingSecs = seconds - accumulatedSeconds;
				return cur.BeatTime + (remainingSecs * (cur.Bpm / 60.0));
			}

			accumulatedSeconds += segSecs;
		}

		var last = _tempoChanges[_tempoChanges.Count - 1];
		return last.BeatTime + ((seconds - accumulatedSeconds) * (last.Bpm / 60.0));
	}


	private void UpdateVisualPlayhead()
	{
		double currentTimeSeconds = _audioEngine.CurrentTimeSeconds - (PreferencesManager.Current.RenderDelayMs / 1000.0);
		if (currentTimeSeconds < 0.0)
		{
			currentTimeSeconds = 0.0;
		}
		double num2 = SecondsToBeats(currentTimeSeconds);
		currentPlayheadX = num2 * (double)BeatWidth;
		playheadLine.X1 = currentPlayheadX;
		playheadLine.X2 = currentPlayheadX;
		Canvas.SetLeft(playheadTriangle, currentPlayheadX);
		double num3 = Math.Floor(num2 * 4.0);
		int currentRow = (int)num3;
		double num4 = num3 * 14.0;
		TrackerPlayhead.Margin = new Thickness(0.0, num4, 0.0, 0.0);

		UpdateScrollLockUI();

		bool isScrollLockOn = System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.Scroll);
		if (isScrollLockOn && TrackerView.Visibility == Visibility.Visible)
		{
			_trackerSelectionStartRow = null;
			_trackerSelectionEndRow = null;
			_trackerCursorRow = currentRow;
			UpdateTrackerCursor();
		}
		GetMeasureBoundsForRow(currentRow, out int measureStartRow, out int measureRowCount);
		double topOverlayHeight = measureStartRow * 14.0;
		double bottomOverlayTop = (measureStartRow + measureRowCount) * 14.0;

		TrackerDarkOverlayTop.Height = Math.Max(0.0, topOverlayHeight);
		TrackerDarkOverlayBottom.Margin = new Thickness(0.0, bottomOverlayTop, 0.0, 0.0);

		var currentTs = GetTimeSignatureAtRow(currentRow);
		if (_timeSignatureNumerator != currentTs.Numerator || _timeSignatureDenominator != currentTs.Denominator)
		{
			_timeSignatureNumerator = currentTs.Numerator;
			_timeSignatureDenominator = currentTs.Denominator;
			UpdateTimeSignatureUI();
		}
		double num8 = TrackerBackgroundCanvas.Height;
		if (double.IsNaN(num8) || num8 <= 0.0)
		{
			num8 = 100000.0;
		}
		TrackerDarkOverlayBottom.Height = Math.Max(0.0, num8 - bottomOverlayTop);
		if (TrackerView.Visibility == Visibility.Visible && _trackLeds.Count > 0)
		{
			double num9 = num2 * (double)_ticksPerQuarterNote;
			double num10 = 50.0;
			for (int i = 0; i < _trackLeds.Count; i++)
			{
				int num11 = 0;
				var trackNotes = _activeTrackNotes[i];
				int count = trackNotes.Count;
				if (count > 0)
				{
					// Binary search to find the nearest note around the current tick
					int low = 0, high = count - 1, startIndex = 0;
					while (low <= high)
					{
						int mid = (low + high) / 2;
						if (trackNotes[mid].Time <= num9)
						{
							startIndex = mid;
							low = mid + 1;
						}
						else
						{
							high = mid - 1;
						}
					}

					// Check backwards to find any active overlapping notes
					for (int k = startIndex; k >= 0; k--)
					{
						var item = trackNotes[k];
						if ((double)item.EndTime > num9)
						{
							if ((byte)item.Velocity > num11)
							{
								num11 = (byte)item.Velocity;
							}
						}
						// If note end time is already before num9 by more than 2 beats, no earlier notes could overlap
						if (num9 - (double)item.EndTime > (_ticksPerQuarterNote * 4))
						{
							break;
						}
					}
				}
				_trackVuLevels[i] *= 0.85;
				if (num11 > 0)
				{
					double num12 = (double)num11 / 127.0;
					if (num12 > _trackVuLevels[i])
					{
						_trackVuLevels[i] = num12;
					}
				}
				
				double maxLedWidth = _trackLeds[i].MaxWidth;
				if (double.IsNaN(maxLedWidth) || double.IsInfinity(maxLedWidth) || maxLedWidth <= 0.0) 
				{
				    maxLedWidth = 50.0;
				}
				
				_trackLeds[i].Width = _trackVuLevels[i] * maxLedWidth;
				if (_trackVuLevels[i] > 0.8)
				{
					_trackLeds[i].Fill = Brushes.Red;
				}
				else if (_trackVuLevels[i] > 0.6)
				{
					_trackLeds[i].Fill = Brushes.Yellow;
				}
				else
				{
					_trackLeds[i].Fill = Brushes.LimeGreen;
				}
			}
		}
		if (TrackerView.Visibility == Visibility.Visible)
		{
			double num13 = num4 - TrackerGridScrollViewer.ViewportHeight / 2.0 + 7.0;
			if (num13 < 0.0)
			{
				num13 = 0.0;
			}
			TrackerGridScrollViewer.ScrollToVerticalOffset(num13);
		}
		else if (PianoRollView.Visibility == Visibility.Visible && (currentPlayheadX > GridScrollViewer.HorizontalOffset + GridScrollViewer.ViewportWidth * 0.9 || currentPlayheadX < GridScrollViewer.HorizontalOffset))
		{
			GridScrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, currentPlayheadX - GridScrollViewer.ViewportWidth * 0.1));
		}
		switch ((int)Math.Floor(num2 * 2.0) % 4)
		{
		case 0:
			MothroachImage.Source = _mothRight;
			break;
		case 2:
			MothroachImage.Source = _mothLeft;
			break;
		default:
			MothroachImage.Source = _mothForward;
			break;
		}
		MothroachTransform.ScaleX = 1.0;
	}

	// ---------------- Navigation helpers ----------------
	private void SeekFromAbsoluteX(double absoluteX)
	{
		// Determine tempo (fallback 120 BPM)
		if (!int.TryParse(TempoTextBox.Text, out var bpm))
			bpm = 120;

		// Convert pixel X to beats and seconds
		double beatPos = absoluteX / BeatWidth;
		double seconds = beatPos * 60.0 / bpm;
		seconds = Math.Max(0.0, seconds);
		double playheadX = seconds * bpm / 60.0 * BeatWidth;
		SetPlayheadPosition(playheadX);
		UpdateVisualPlayhead();
	}

	// Navigation and Mouse Handling for Piano Roll
	private void MainContentGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
	}

	private void PianoRollGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
	}

	// Time Signature button click & dialog

	public void OpenBpmEditDialog(bool defaultAtCursor = false)
	{
		Grid grid = new Grid { Margin = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		TextBlock prompt = new TextBlock
		{
			Text = "Set Project / Tracker Tempo (BPM):",
			Foreground = Brushes.LightGray,
			FontSize = 13,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 12)
		};
		Grid.SetRow(prompt, 0);
		grid.Children.Add(prompt);

		int curBpm = 120;
		if (ViewTrackerMenu.IsChecked)
		{
			curBpm = GetTempoAtRow(_trackerCursorRow).Bpm;
		}
		else if (int.TryParse(TempoTextBox.Text, out var bVal))
		{
			curBpm = bVal;
		}

		TextBox bpmBox = new TextBox
		{
			Text = curBpm.ToString(),
			Width = 80,
			Height = 28,
			FontSize = 14,
			FontWeight = FontWeights.Bold,
			TextAlignment = TextAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		Grid.SetRow(bpmBox, 1);
		grid.Children.Add(bpmBox);

		CheckBox atCursorCheck = new CheckBox
		{
			Content = "Apply at current tracker cursor row only",
			Foreground = Brushes.LightGray,
			FontSize = 11,
			Margin = new Thickness(0, 8, 0, 10),
			HorizontalAlignment = HorizontalAlignment.Center,
			IsChecked = defaultAtCursor,
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

		var bpmWindow = CreateCustomDialogWindow("Edit Tempo (BPM)", 300, 210, grid);

		okBtn.Click += (s, ev) =>
		{
			if (int.TryParse(bpmBox.Text, out int b) && b >= 20 && b <= 999)
			{
				bool atCursor = atCursorCheck.IsChecked == true;
				SetBpm(b, atCursor);
				bpmWindow.DialogResult = true;
				bpmWindow.Close();
				return;
			}
			SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Please enter a valid tempo between 20 and 999 BPM.", "Invalid Tempo", MessageBoxButton.OK, MessageBoxImage.Warning);
		};

		cancelBtn.Click += (s, ev) => bpmWindow.Close();

		btnPanel.Children.Add(okBtn);
		btnPanel.Children.Add(cancelBtn);
		Grid.SetRow(btnPanel, 4);
		grid.Children.Add(btnPanel);

		bpmWindow.ShowDialog();
	}

	public void OpenVibratoEditDialog(int trackIdx, int row)
	{
		Grid grid = new Grid { Margin = new Thickness(16), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		TextBlock prompt = new TextBlock
		{
			Text = "Set Vibrato (Modulation) Depth (0 - 127):",
			Foreground = Brushes.LightGray,
			FontSize = 13,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 12)
		};
		Grid.SetRow(prompt, 0);
		grid.Children.Add(prompt);

		TextBox valBox = new TextBox
		{
			Text = "64",
			Width = 80,
			Height = 28,
			FontSize = 14,
			FontWeight = FontWeights.Bold,
			TextAlignment = TextAlignment.Center,
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		Grid.SetRow(valBox, 1);
		grid.Children.Add(valBox);

		StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
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
		Button clearBtn = new Button
		{
			Content = "Remove",
			Width = 75,
			Height = 26,
			Margin = new Thickness(0, 0, 8, 0),
			Background = new SolidColorBrush(Color.FromRgb(150, 50, 50)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand
		};

		var vibWindow = CreateCustomDialogWindow("Edit Vibrato", 300, 200, grid);

		okBtn.Click += (s, ev) =>
		{
			if (int.TryParse(valBox.Text, out int v) && v >= 0 && v <= 127)
			{
				SetTrackerVibrato(trackIdx, row, (byte)v);
				vibWindow.DialogResult = true;
				vibWindow.Close();
				return;
			}
			SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("Please enter a valid vibrato depth between 0 and 127.", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
		};

		clearBtn.Click += (s, ev) =>
		{
			SetTrackerVibrato(trackIdx, row, null);
			vibWindow.DialogResult = true;
			vibWindow.Close();
		};

		cancelBtn.Click += (s, ev) => vibWindow.Close();

		btnPanel.Children.Add(clearBtn);
		btnPanel.Children.Add(okBtn);
		btnPanel.Children.Add(cancelBtn);
		Grid.SetRow(btnPanel, 4);
		grid.Children.Add(btnPanel);

		vibWindow.ShowDialog();
	}

	public void OpenUndoHistoryDialog()
	{
		Grid mainGrid = new Grid { Margin = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

		TextBlock headerText = new TextBlock
		{
			Text = "Action History (Double-click or select & jump to restore state):",
			Foreground = Brushes.LightGray,
			FontSize = 12,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0, 0, 0, 8)
		};
		Grid.SetRow(headerText, 0);
		mainGrid.Children.Add(headerText);

		ListBox historyList = new ListBox
		{
			Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
			Foreground = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			FontFamily = new FontFamily("Segoe UI"),
			FontSize = 12
		};

		var historyWindow = CreateCustomDialogWindow("Undo History", 440, 480, mainGrid, allowResize: true);

		void PopulateHistoryList()
		{
			historyList.Items.Clear();
			for (int i = 0; i < UndoManager.UndoStack.Count; i++)
			{
				var action = UndoManager.UndoStack[i];
				bool isCurrent = (i == UndoManager.UndoStack.Count - 1);
				
				ListBoxItem item = new ListBoxItem
				{
					Tag = i,
					Padding = new Thickness(6, 4, 6, 4),
					Background = isCurrent ? new SolidColorBrush(Color.FromRgb(45, 60, 80)) : Brushes.Transparent,
					Foreground = isCurrent ? Brushes.LightSkyBlue : Brushes.White
				};

				DockPanel dp = new DockPanel { LastChildFill = true };
				TextBlock timeBlock = new TextBlock
				{
					Text = action.Timestamp.ToString("HH:mm:ss"),
					Foreground = Brushes.Gray,
					Margin = new Thickness(0, 0, 10, 0)
				};
				DockPanel.SetDock(timeBlock, Dock.Right);
				dp.Children.Add(timeBlock);

				TextBlock descBlock = new TextBlock
				{
					Text = (isCurrent ? "► " : "   ") + action.Description,
					FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal
				};
				dp.Children.Add(descBlock);

				item.Content = dp;
				historyList.Items.Add(item);
			}

			if (historyList.Items.Count > 0)
			{
				historyList.SelectedIndex = historyList.Items.Count - 1;
				historyList.ScrollIntoView(historyList.SelectedItem);
			}
		}

		PopulateHistoryList();

		Grid.SetRow(historyList, 1);
		mainGrid.Children.Add(historyList);

		StackPanel btnPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0, 10, 0, 0)
		};

		Button jumpBtn = new Button
		{
			Content = "Jump to State",
			Width = 100,
			Height = 26,
			Margin = new Thickness(0, 0, 8, 0),
			Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			FontWeight = FontWeights.SemiBold,
			Cursor = Cursors.Hand
		};

		Button closeBtn = new Button
		{
			Content = "Close",
			Width = 75,
			Height = 26,
			Background = new SolidColorBrush(Color.FromRgb(62, 62, 66)),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand
		};

		void ExecuteJump()
		{
			if (historyList.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is int idx)
			{
				var state = UndoManager.JumpToHistoryIndex(idx);
				if (state != null)
				{
					RestoreMidiState(state);
				}
				UpdateUndoUI();
				PopulateHistoryList();
			}
		}

		historyList.MouseDoubleClick += (s, ev) => ExecuteJump();
		jumpBtn.Click += (s, ev) => ExecuteJump();
		closeBtn.Click += (s, ev) => historyWindow.Close();

		btnPanel.Children.Add(jumpBtn);
		btnPanel.Children.Add(closeBtn);
		Grid.SetRow(btnPanel, 2);
		mainGrid.Children.Add(btnPanel);

		historyWindow.ShowDialog();
	}


	// ==========================================
	// Reference Track Logic
	// ==========================================

	private float[] _referenceWaveformPeaks = Array.Empty<float>();
	private bool _isDraggingWaveform = false;
	private Point _waveformDragStartPoint;
	private double _waveformDragStartOffset;

	private string _referenceAudioPath = null;








	public void MoveTrack(int sourceIndex, int destIndex)
	{
		if (_loadedMidi == null) return;
		int trackCount = _loadedMidi.GetTrackChunks().Count();
		if (sourceIndex < 0 || sourceIndex >= trackCount || destIndex < 0 || destIndex >= trackCount || sourceIndex == destIndex) return;

		PushUndoState($"Move track {sourceIndex + 1} to {destIndex + 1}");

		var chunksList = _loadedMidi.Chunks.ToList();
		
		int actualSourceIdx = -1;
		int actualDestIdx = -1;
		int trackCounter = 0;

		for (int i = 0; i < chunksList.Count; i++)
		{
			if (chunksList[i] is TrackChunk)
			{
				if (trackCounter == sourceIndex) actualSourceIdx = i;
				if (trackCounter == destIndex) actualDestIdx = i;
				trackCounter++;
			}
		}

		if (actualSourceIdx != -1 && actualDestIdx != -1)
		{
			var chunkToMove = chunksList[actualSourceIdx];
			chunksList.RemoveAt(actualSourceIdx);
			
			if (actualSourceIdx < actualDestIdx)
			{
				actualDestIdx--;
			}
			
			chunksList.Insert(actualDestIdx, chunkToMove);
			
			_loadedMidi.Chunks.Clear();
			foreach (var c in chunksList) _loadedMidi.Chunks.Add(c);

			while (_trackColors.Count < trackCount) _trackColors.Add(Color.FromRgb(100, 100, 100));
			while (_trackChannels.Count < trackCount) _trackChannels.Add(0);
			while (_trackShowFx.Count < trackCount) _trackShowFx.Add(false);

			MoveListElement(_trackColors, sourceIndex, destIndex);
			MoveListElement(_trackChannels, sourceIndex, destIndex);
			MoveListElement(_trackShowFx, sourceIndex, destIndex);
			
			if (_activeTrackIndex == sourceIndex)
				_activeTrackIndex = destIndex;
			else if (sourceIndex < _activeTrackIndex && destIndex >= _activeTrackIndex)
				_activeTrackIndex--;
			else if (sourceIndex > _activeTrackIndex && destIndex <= _activeTrackIndex)
				_activeTrackIndex++;

			_trackerSelectionStartRow = null;
			_trackerSelectionEndRow = null;
			_trackerSelectionStartLogicalCol = null;
			_trackerSelectionEndLogicalCol = null;

			BuildNoteCache();
			DrawPianoRoll();
			GenerateTrackerView();
			
			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
	}

	public void DeleteTrack(int trackIndex)
	{
		if (_loadedMidi == null) return;
		int trackCount = _loadedMidi.GetTrackChunks().Count();
		if (trackIndex < 0 || trackIndex >= trackCount) return;

		PushUndoState($"Delete track {trackIndex + 1}");

		var chunksList = _loadedMidi.Chunks.ToList();
		
		int actualIdx = -1;
		int trackCounter = 0;

		for (int i = 0; i < chunksList.Count; i++)
		{
			if (chunksList[i] is TrackChunk)
			{
				if (trackCounter == trackIndex) 
				{
					actualIdx = i;
					break;
				}
				trackCounter++;
			}
		}

		if (actualIdx != -1)
		{
			chunksList.RemoveAt(actualIdx);
			
			_loadedMidi.Chunks.Clear();
			foreach (var c in chunksList) _loadedMidi.Chunks.Add(c);

			if (trackIndex < _trackColors.Count) _trackColors.RemoveAt(trackIndex);
			if (trackIndex < _trackChannels.Count) _trackChannels.RemoveAt(trackIndex);
			if (trackIndex < _trackShowFx.Count) _trackShowFx.RemoveAt(trackIndex);
			
			if (_activeTrackIndex == trackIndex)
			{
				_activeTrackIndex = Math.Max(0, trackIndex - 1);
			}
			else if (trackIndex < _activeTrackIndex)
			{
				_activeTrackIndex--;
			}

			_trackerSelectionStartRow = null;
			_trackerSelectionEndRow = null;
			_trackerSelectionStartLogicalCol = null;
			_trackerSelectionEndLogicalCol = null;

			BuildNoteCache();
			DrawPianoRoll();
			GenerateTrackerView();
			
			_midiSaveTimer.Stop();
			_midiSaveTimer.Start();
		}
	}




}

