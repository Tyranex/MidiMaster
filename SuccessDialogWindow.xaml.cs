using System.Windows;
using System.Windows.Input;

namespace SS14_MIDI_IDE
{
    public partial class SuccessDialogWindow : Window
    {
        public SuccessDialogWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
