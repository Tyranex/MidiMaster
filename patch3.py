import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace("private List<bool> _trackMuted;", "private List<bool> _trackMuted;\n\tprivate List<bool> _trackShowFx;\n\tprivate double GetTrackColWidth(int index) => _trackShowFx != null && index < _trackShowFx.Count && _trackShowFx[index] ? 70.0 : 44.0;")

content = content.replace("_trackMuted = new List<bool>();", "_trackMuted = new List<bool>();\n\t\t_trackShowFx = new List<bool>();")

content = content.replace("_trackMuted.Clear();", "_trackMuted.Clear();\n\t\t_trackShowFx.Clear();")

with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Patch 3 complete")
