import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add _trackShowFx and GetTrackColWidth
fields = """	private List<bool> _trackMuted = new List<bool>();
	private List<bool> _trackShowFx = new List<bool>();
	private double GetTrackColWidth(int trackIndex) => _trackShowFx != null && trackIndex < _trackShowFx.Count && _trackShowFx[trackIndex] ? 70.0 : 44.0;
"""
content = re.sub(r'private List<bool> _trackMuted;', fields, content)

# 2. Update GenerateTrackerView to initialize _trackShowFx and use GetTrackColWidth
# Find TrackerColWidth usage in GenerateTrackerView
# Let's replace `TrackerColWidth` with `GetTrackColWidth(j)` inside the loops.
# But wait, `TrackerColWidth` might be hardcoded as `70.0` or `TrackerColWidth` in the decompiled code!
# Let's check how ILSpy outputted it. ILSpy might have evaluated the constant!
