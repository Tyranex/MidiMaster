import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# Make sure _trackShowFx is populated in GenerateTrackerView
# Let's find _trackMuted.Add(item: false); inside GenerateTrackerView.
content = content.replace("_trackMuted.Add(item: false);", "_trackMuted.Add(item: false);\n\t\t\twhile (_trackShowFx.Count <= num2) _trackShowFx.Add(false);")

# Add the UI toggle button in TrackerHeaderCanvas
# TrackerHeaderCanvas.Children.Add(element);  <-- element is the TextBlock
ui_toggle_code = """			TrackerHeaderCanvas.Children.Add(element);
			
			TextBlock fxToggle = new TextBlock
			{
				Text = _trackShowFx[num2] ? "<" : ">",
				Foreground = Brushes.Gray,
				FontSize = 10.0,
				Cursor = Cursors.Hand
			};
			int trackIndexForToggle = num2;
			fxToggle.MouseDown += (s, e) => {
				_trackShowFx[trackIndexForToggle] = !_trackShowFx[trackIndexForToggle];
				GenerateTrackerView();
			};
			Canvas.SetLeft(fxToggle, num9 + num8 - 15.0);
			Canvas.SetTop(fxToggle, 2.0);
			TrackerHeaderCanvas.Children.Add(fxToggle);
"""
content = content.replace("TrackerHeaderCanvas.Children.Add(element);", ui_toggle_code, 1) # Only first match inside GenerateTrackerView

with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Patch 2 complete")
