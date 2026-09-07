import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# Add _trackShowFx
content = content.replace("private List<bool> _trackMuted = new List<bool>();", "private List<bool> _trackMuted = new List<bool>();\n\tprivate List<bool> _trackShowFx = new List<bool>();\n\tprivate double GetTrackColWidth(int index) => _trackShowFx != null && index < _trackShowFx.Count && _trackShowFx[index] ? 70.0 : 44.0;")

# In TrackerGrid_MouseMove (UpdateActiveTrackVisuals probably inline)
# Replace `num * 70` with `num * GetTrackColWidth(i)`?
# Actually, it's easier to just do it via exact string replace since ILSpy gave consistent variable names.

content = content.replace("double num2 = 30 + num * 70;", "double num2 = 30 + num * GetTrackColWidth(num);")

content = content.replace("TrackerActiveColumnHighlight.Width = 70.0;", "TrackerActiveColumnHighlight.Width = GetTrackColWidth(i);")

content = content.replace("double num4 = num3 + (double)(_trackPolyphonies[i] * 70);", "double num4 = num3 + (double)(_trackPolyphonies[i] * GetTrackColWidth(i));")

content = content.replace("int num5 = (int)Math.Floor((position.X - num3) / 70.0);", "int num5 = (int)Math.Floor((position.X - num3) / GetTrackColWidth(i));")

content = content.replace("double num6 = num3 + (double)(num5 * 70);", "double num6 = num3 + (double)(num5 * GetTrackColWidth(i));")

content = content.replace("double num8 = 70 * num4;", "double num8 = GetTrackColWidth(num12) * num4;")

content = content.replace("double num20 = num13 + (double)(num19 * 70);", "double num20 = num13 + (double)(num19 * GetTrackColWidth(num12));")

content = content.replace("double num24 = num13 + (double)(k * 70);", "double num24 = num13 + (double)(k * GetTrackColWidth(num12));")

content = content.replace("double num30 = num28 + (double)(n * 70);", "double num30 = num28 + (double)(n * GetTrackColWidth(num12));")

with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Patch 1 complete")
