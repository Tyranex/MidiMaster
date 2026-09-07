import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# Fix MainWindow.xaml.cs(1294,33): error CS0103: The name 'num2' does not exist in the current context
# Context: while (_trackShowFx.Count <= num2) _trackShowFx.Add(false);
# Should be: while (_trackShowFx.Count <= _trackCanvases.Count) _trackShowFx.Add(false);
content = content.replace("while (_trackShowFx.Count <= num2) _trackShowFx.Add(false);", "while (_trackShowFx.Count <= _trackCanvases.Count) _trackShowFx.Add(false);")

# Fix MainWindow.xaml.cs(1463,58): error CS0103: The name 'i' does not exist in the current context
# Context: TrackerActiveColumnHighlight.Width = GetTrackColWidth(i);
# Should be: GetTrackColWidth(num);
content = content.replace("TrackerActiveColumnHighlight.Width = GetTrackColWidth(i);", "TrackerActiveColumnHighlight.Width = GetTrackColWidth(num);")

# Fix MainWindow.xaml.cs(1702,35): error CS0103: The name 'num12' does not exist in the current context
# Context: double num8 = GetTrackColWidth(num12) * num4;
# Should be: GetTrackColWidth(i) * num4;
content = content.replace("double num8 = GetTrackColWidth(num12) * num4;", "double num8 = GetTrackColWidth(i) * num4;")

# Fix MainWindow.xaml.cs(1874,59): error CS0103: The name 'num12' does not exist in the current context
# Context: double num24 = num13 + (double)(k * GetTrackColWidth(num12));
# wait, there's another one on 1874! Let's just fix all GetTrackColWidth(num12) to GetTrackColWidth(j) inside DrawTrackerGrid!
content = content.replace("GetTrackColWidth(num12)", "GetTrackColWidth(j)")

with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Patch 4 complete")
