import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# GenerateTrackerView:
# double num8 = GetTrackColWidth(i) * num4;
# should be
# double num8 = GetTrackColWidth(num2) * num4;
content = content.replace("double num8 = GetTrackColWidth(i) * num4;", "double num8 = GetTrackColWidth(num2) * num4;")

# DrawTrackerGrid:
# double num20 = num13 + (double)(num19 * GetTrackColWidth(j));
# should be
# double num20 = num13 + (double)(num19 * GetTrackColWidth(num12));
content = content.replace("double num20 = num13 + (double)(num19 * GetTrackColWidth(j));", "double num20 = num13 + (double)(num19 * GetTrackColWidth(num12));")

# double num24 = num13 + (double)(k * GetTrackColWidth(j));
# should be
# double num24 = num13 + (double)(k * GetTrackColWidth(num12));
content = content.replace("double num24 = num13 + (double)(k * GetTrackColWidth(j));", "double num24 = num13 + (double)(k * GetTrackColWidth(num12));")

with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Patch 5 complete")
