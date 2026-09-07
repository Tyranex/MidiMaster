using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;

namespace SS14_MIDI_IDE;

public class UndoAction
{
	public string Description { get; set; }
	public DateTime Timestamp { get; set; }
	public byte[] MidiState { get; set; }
	public int CursorRow { get; set; }
	public int ActiveTrackIndex { get; set; }

	public UndoAction(string description, byte[] midiState, int cursorRow, int activeTrackIndex)
	{
		Description = description;
		Timestamp = DateTime.Now;
		MidiState = midiState;
		CursorRow = cursorRow;
		ActiveTrackIndex = activeTrackIndex;
	}

	public override string ToString()
	{
		return $"[{Timestamp:HH:mm:ss}] {Description}";
	}
}

public class UndoRedoManager
{
	public int MaxHistory { get; set; } = 100;
	public List<UndoAction> UndoStack { get; } = new List<UndoAction>();
	public List<UndoAction> RedoStack { get; } = new List<UndoAction>();

	public event EventHandler HistoryChanged;

	public bool CanUndo => UndoStack.Count > 1; // Needs at least 1 previous state to revert to
	public bool CanRedo => RedoStack.Count > 0;

	public void Clear()
	{
		UndoStack.Clear();
		RedoStack.Clear();
		HistoryChanged?.Invoke(this, EventArgs.Empty);
	}

		public void PushState(MidiFile midi, string description, int cursorRow, int activeTrackIndex)
		{
			if (midi == null) return;

			byte[] bytes;
			using (var ms = new MemoryStream())
			{
				midi.Write(ms);
				bytes = ms.ToArray();
			}

			// Don't push identical consecutive descriptions if states match
			if (UndoStack.Count > 0 && UndoStack[UndoStack.Count - 1].Description == description)
			{
				if (ByteArraysEqual(UndoStack[UndoStack.Count - 1].MidiState, bytes))
				{
					return;
				}
			}

			UndoStack.Add(new UndoAction(description, bytes, cursorRow, activeTrackIndex));
			if (UndoStack.Count > MaxHistory)
			{
				UndoStack.RemoveAt(0);
			}

			RedoStack.Clear();
			HistoryChanged?.Invoke(this, EventArgs.Empty);
		}

		public UndoAction Undo()
		{
			if (!CanUndo) return null;

			var currentState = UndoStack[UndoStack.Count - 1];
			UndoStack.RemoveAt(UndoStack.Count - 1);
			RedoStack.Add(currentState);

			var targetState = UndoStack[UndoStack.Count - 1];
			HistoryChanged?.Invoke(this, EventArgs.Empty);
			return targetState;
		}

		public UndoAction Redo()
		{
			if (!CanRedo) return null;

			var targetState = RedoStack[RedoStack.Count - 1];
			RedoStack.RemoveAt(RedoStack.Count - 1);
			UndoStack.Add(targetState);

			HistoryChanged?.Invoke(this, EventArgs.Empty);
			return targetState;
		}

		public UndoAction JumpToHistoryIndex(int index)
		{
			if (index < 0 || index >= UndoStack.Count) return null;
			if (index == UndoStack.Count - 1) return UndoStack[index]; // Current state

			while (UndoStack.Count - 1 > index)
			{
				var state = UndoStack[UndoStack.Count - 1];
				UndoStack.RemoveAt(UndoStack.Count - 1);
				RedoStack.Add(state);
			}

			HistoryChanged?.Invoke(this, EventArgs.Empty);
			return UndoStack[UndoStack.Count - 1];
		}

		private static bool ByteArraysEqual(byte[] a1, byte[] a2)
		{
			if (ReferenceEquals(a1, a2)) return true;
			if (a1 == null || a2 == null) return false;
			if (a1.Length != a2.Length) return false;
			for (int i = 0; i < a1.Length; i++)
			{
				if (a1[i] != a2[i]) return false;
			}
			return true;
		}
	}
