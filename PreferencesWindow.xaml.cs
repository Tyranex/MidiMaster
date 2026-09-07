using System.Windows;
using System.Windows.Input;

namespace SS14_MIDI_IDE
{
    public partial class PreferencesWindow : Window
    {
        public PreferencesWindow()
        {
            InitializeComponent();
            LoadSettingsToUI();
        }

        private void LoadSettingsToUI()
        {
            DefaultVelocityTextBox.Text = PreferencesManager.Current.DefaultVelocity.ToString();
            RenderDelayTextBox.Text = PreferencesManager.Current.RenderDelayMs.ToString();
            ShowGridLinesCheckBox.IsChecked = PreferencesManager.Current.ShowGridLines;
        }

        private void SaveSettingsFromUI()
        {
            if (int.TryParse(DefaultVelocityTextBox.Text, out int vel))
            {
                PreferencesManager.Current.DefaultVelocity = System.Math.Clamp(vel, 0, 127);
            }
            if (int.TryParse(RenderDelayTextBox.Text, out int delay))
            {
                PreferencesManager.Current.RenderDelayMs = delay;
            }
            PreferencesManager.Current.ShowGridLines = ShowGridLinesCheckBox.IsChecked ?? true;
            
            PreferencesManager.Save();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUI();
            this.DialogResult = true;
            this.Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
