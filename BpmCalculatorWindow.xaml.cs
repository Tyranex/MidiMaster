using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SS14_MIDI_IDE
{
    public partial class BpmCalculatorWindow : Window
    {
        private List<DateTime> _taps = new List<DateTime>();
        private readonly TimeSpan _resetTimeout = TimeSpan.FromSeconds(2.5);

        public BpmCalculatorWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                RegisterTap();
                e.Handled = true;
                
                // Visual feedback for keyboard press
                TapButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => TapButton.ClearValue(BackgroundProperty));
                });
            }
        }

        private void TapButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            RegisterTap();
        }
        
        private void TapButton_Click(object sender, RoutedEventArgs e)
        {
            // Handled in preview to avoid focus delay issues
        }

        private void RegisterTap()
        {
            var now = DateTime.Now;

            if (_taps.Count > 0 && (now - _taps.Last()) > _resetTimeout)
            {
                _taps.Clear();
            }

            _taps.Add(now);

            // Keep only the last 10 taps to get a rolling average
            if (_taps.Count > 10)
            {
                _taps.RemoveAt(0);
            }

            UpdateBpmDisplay();
        }

        private void UpdateBpmDisplay()
        {
            if (_taps.Count < 2)
            {
                BpmText.Text = "---";
                return;
            }

            var intervals = new List<double>();
            for (int i = 1; i < _taps.Count; i++)
            {
                intervals.Add((_taps[i] - _taps[i - 1]).TotalSeconds);
            }

            double averageInterval = intervals.Average();
            if (averageInterval > 0)
            {
                double bpm = 60.0 / averageInterval;
                BpmText.Text = Math.Round(bpm).ToString();
            }
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
