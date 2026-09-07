using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SS14_MIDI_IDE
{
    public partial class InstrumentEditorWindow : Window
    {
        private AudioEngine _audioEngine;
        private FtmInstrument _currentInstrument;
        
        public InstrumentEditorWindow(AudioEngine audioEngine)
        {
            InitializeComponent();
            _audioEngine = audioEngine;
            
            LoadInstruments();
        }
        
        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private void LoadInstruments()
        {
            if (_audioEngine.CustomFtmInstruments != null)
            {
                InstrumentListBox.ItemsSource = _audioEngine.CustomFtmInstruments.Values.OrderBy(i => i.Id).ToList();
            }
        }
        
        private void InstrumentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentInstrument = InstrumentListBox.SelectedItem as FtmInstrument;
            if (_currentInstrument != null)
            {
                EditorPanel.IsEnabled = true;
                InstNameBox.Text = _currentInstrument.Name;
                ChipComboBox.SelectedIndex = _currentInstrument.FtmType == 1 ? 0 : 1;
                
                // TODO: Render graph for selected macro
            }
            else
            {
                EditorPanel.IsEnabled = false;
            }
        }
        
        private void NewInst_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder
        }
        
        private void DeleteInst_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder
        }
        
        private void InstNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentInstrument != null)
            {
                _currentInstrument.Name = InstNameBox.Text;
                InstrumentListBox.Items.Refresh();
            }
        }
        
        private void ChipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Placeholder
        }
        
        private FtmSequence GetCurrentSequence()
        {
            if (_currentInstrument == null || _audioEngine.CustomFtmSequences == null) return null;
            
            var tab = MacroTabControl.SelectedItem as TabItem;
            if (tab == null) return null;
            
            string tag = tab.Tag.ToString();
            int seqIdx = -1;
            if (tag == "Volume") seqIdx = _currentInstrument.VolumeSeq;
            else if (tag == "Arpeggio") seqIdx = _currentInstrument.ArpeggioSeq;
            else if (tag == "Pitch") seqIdx = _currentInstrument.PitchSeq;
            else if (tag == "HiPitch") seqIdx = _currentInstrument.HiPitchSeq;
            else if (tag == "Duty") seqIdx = _currentInstrument.DutySeq;
            
            if (seqIdx != -1 && _audioEngine.CustomFtmSequences.TryGetValue(seqIdx, out var seq))
            {
                return seq;
            }
            return null;
        }

        private void RenderGraph()
        {
            GraphCanvas.Children.Clear();
            var seq = GetCurrentSequence();
            
            if (seq == null || seq.Values == null)
            {
                SeqLengthBox.Text = "0";
                SeqLoopBox.Text = "-1";
                SeqReleaseBox.Text = "-1";
                return;
            }
            
            SeqLengthBox.Text = seq.Values.Length.ToString();
            SeqLoopBox.Text = seq.LoopPoint.ToString();
            SeqReleaseBox.Text = seq.ReleasePoint.ToString();
            
            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;
            if (width == 0 || height == 0) return;
            
            int count = seq.Values.Length;
            if (count == 0) return;
            
            double barWidth = Math.Max(2, width / count);
            
            // Draw grid lines
            var midLine = new Line { X1 = 0, X2 = width, Y1 = height / 2, Y2 = height / 2, Stroke = Brushes.DarkSlateGray, StrokeThickness = 1 };
            GraphCanvas.Children.Add(midLine);

            for (int i = 0; i < count; i++)
            {
                sbyte val = (sbyte)seq.Values[i];
                // Map -128..127 to height, or 0..15 for volume
                var tab = MacroTabControl.SelectedItem as TabItem;
                string tag = tab?.Tag?.ToString();
                
                double barHeight = 0;
                double yPos = 0;
                
                if (tag == "Volume" || tag == "Duty")
                {
                    // 0 to 15
                    double norm = val / 15.0;
                    barHeight = norm * height;
                    yPos = height - barHeight;
                }
                else
                {
                    // -64 to 63 approx
                    double norm = val / 64.0; 
                    barHeight = Math.Abs(norm * (height / 2));
                    if (val >= 0) yPos = (height / 2) - barHeight;
                    else yPos = height / 2;
                }
                
                var rect = new Rectangle
                {
                    Width = barWidth - 1,
                    Height = Math.Max(1, barHeight),
                    Fill = (i == seq.LoopPoint) ? Brushes.Red : (i == seq.ReleasePoint ? Brushes.Yellow : Brushes.DodgerBlue)
                };
                
                Canvas.SetLeft(rect, i * barWidth);
                Canvas.SetTop(rect, yPos);
                GraphCanvas.Children.Add(rect);
            }
        }
        
        private void SeqLengthBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void SeqLoopBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void SeqReleaseBox_TextChanged(object sender, TextChangedEventArgs e) { }
        
        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) 
        {
            RenderGraph();
        }
        
        private void MacroTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderGraph();
        }
        
        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e) { }
        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
    }
}
