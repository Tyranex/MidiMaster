using System;
using System.Windows;

namespace SS14_MIDI_IDE
{
    public partial class MainWindow : Window
    {
	private void UpdateUndoUI()
	{
		if (EditUndoMenu != null)
		{
			EditUndoMenu.IsEnabled = UndoManager.CanUndo;
			if (UndoManager.UndoStack.Count > 1)
			{
				var action = UndoManager.UndoStack[UndoManager.UndoStack.Count - 1];
				EditUndoMenu.Header = $"Undo {action.Description}";
			}
			else
			{
				EditUndoMenu.Header = "Undo";
			}
		}

		if (EditRedoMenu != null)
		{
			EditRedoMenu.IsEnabled = UndoManager.CanRedo;
			if (UndoManager.RedoStack.Count > 0)
			{
				var action = UndoManager.RedoStack[UndoManager.RedoStack.Count - 1];
				EditRedoMenu.Header = $"Redo {action.Description}";
			}
			else
			{
				EditRedoMenu.Header = "Redo";
			}
		}
	}


	private void EditUndo_Click(object sender, RoutedEventArgs e)
	{
		PerformUndo();
	}


	private void EditRedo_Click(object sender, RoutedEventArgs e)
	{
		PerformRedo();
	}


	private void EditUndoHistory_Click(object sender, RoutedEventArgs e)
	{
		OpenUndoHistoryDialog();
	}

    }
}
