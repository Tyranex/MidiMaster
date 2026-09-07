using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SS14_MIDI_IDE.UI.Windows;

public partial class CustomMessageBox : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;

    public CustomMessageBox(string messageBoxText, string caption, MessageBoxButton button)
    {
        InitializeComponent();
        
        TitleText.Text = string.IsNullOrEmpty(caption) ? "MidiMaster" : caption;
        MessageText.Text = messageBoxText;

        SetupButtons(button);
    }

    private void SetupButtons(MessageBoxButton button)
    {
        ButtonPanel.Children.Clear();

        switch (button)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.OKCancel:
                AddButton("OK", MessageBoxResult.OK, true);
                AddButton("Cancel", MessageBoxResult.Cancel, false, true);
                break;
            case MessageBoxButton.YesNo:
                AddButton("Yes", MessageBoxResult.Yes, true);
                AddButton("No", MessageBoxResult.No, false, true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("Yes", MessageBoxResult.Yes, true);
                AddButton("No", MessageBoxResult.No);
                AddButton("Cancel", MessageBoxResult.Cancel, false, true);
                break;
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var btn = new Button
        {
            Content = text,
            IsDefault = isDefault,
            IsCancel = isCancel
        };

        btn.Click += (s, e) =>
        {
            _result = result;
            DialogResult = result == MessageBoxResult.OK || result == MessageBoxResult.Yes;
            Close();
        };

        ButtonPanel.Children.Add(btn);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Cancel;
        DialogResult = false;
        Close();
    }

    public static MessageBoxResult Show(string messageBoxText, string caption = "", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
    {
        var msgBox = new CustomMessageBox(messageBoxText, caption, button);
        msgBox.ShowDialog();
        
        if (msgBox._result == MessageBoxResult.None)
        {
            // Handle X button or Alt+F4
            if (button == MessageBoxButton.OK) return MessageBoxResult.OK;
            if (button == MessageBoxButton.YesNo) return MessageBoxResult.No;
            return MessageBoxResult.Cancel;
        }
        
        return msgBox._result;
    }
}
