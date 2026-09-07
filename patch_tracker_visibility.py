import re

with open("MainWindow.xaml.cs", "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add the lists
lists_declaration = """	private List<int> _trackerAbsoluteIndices = new List<int>();
	private List<int> _trackerCanvasIndices = new List<int>();"""

content = content.replace("private List<double> _trackXOffsets;", "private List<double> _trackXOffsets;\n" + lists_declaration)

# 2. Modify GenerateTrackerView to populate the lists and use them
# Find the start of GenerateTrackerView logic
gen_tracker_find = """		_trackXOffsets.Clear();
		_trackPolyphonies.Clear();
		double num = 0.0;
		int num2 = 0;
		double num3 = 30.0;
		for (int i = 0; i < list.Count; i++)
		{
			ICollection<Note> notes = list[i].GetNotes();
			if (!notes.Any())
			{
				continue;
			}"""

gen_tracker_replace = """		_trackXOffsets.Clear();
		_trackPolyphonies.Clear();
		_trackerAbsoluteIndices.Clear();
		_trackerCanvasIndices.Clear();
		
		int canvasIdx = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].GetNotes().Any())
			{
				if (canvasIdx < _trackCanvases.Count && _trackCanvases[canvasIdx].Visibility == Visibility.Visible)
				{
					_trackerAbsoluteIndices.Add(i);
					_trackerCanvasIndices.Add(canvasIdx);
				}
				canvasIdx++;
			}
		}

		double num = 0.0;
		double num3 = 30.0;
		for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
		{
			int i = _trackerAbsoluteIndices[m];
			int num2 = _trackerCanvasIndices[m];
			ICollection<Note> notes = list[i].GetNotes();"""

content = content.replace(gen_tracker_find, gen_tracker_replace)

# In GenerateTrackerView, remove the `num2++;` at the end of the loop
content = content.replace("			num2++;\n			num3 += num8;", "			num3 += num8;")


# 3. Modify DrawTrackerGrid
draw_tracker_find = """			int num12 = 0;
			for (int j = 0; j < list.Count; j++)
			{
				ICollection<Note> notes2 = list[j].GetNotes();
				if (!notes2.Any())
				{
					continue;
				}"""

draw_tracker_replace = """			for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
			{
				int j = _trackerAbsoluteIndices[m];
				int num12 = _trackerCanvasIndices[m];
				ICollection<Note> notes2 = list[j].GetNotes();"""

content = content.replace(draw_tracker_find, draw_tracker_replace)

# In DrawTrackerGrid, remove `num12++;` at the end of the loop
content = content.replace("				drawingContext2.DrawText(formattedText5, new Point(5.0, num27));\n				num12++;", "				drawingContext2.DrawText(formattedText5, new Point(5.0, num27));")
# just in case it didn't match perfectly, let's also do a fallback
content = content.replace("				num12++;\n			}\n			Pen pen2 = new Pen", "			}\n			Pen pen2 = new Pen")

# 4. Modify UpdateActiveTrackVisuals
update_visuals_find = """	private void UpdateActiveTrackVisuals()
	{
		int num = -1;
		if (_loadedMidi != null)
		{
			List<TrackChunk> list = _loadedMidi.GetTrackChunks().ToList();
			int num2 = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].GetNotes().Any())
				{
					if (i == _activeTrackIndex)
					{
						num = num2;
						break;
					}
					num2++;
				}
			}
		}"""

update_visuals_replace = """	private void UpdateActiveTrackVisuals()
	{
		int num = -1;
		if (_loadedMidi != null)
		{
			for (int m = 0; m < _trackerAbsoluteIndices.Count; m++)
			{
				if (_trackerAbsoluteIndices[m] == _activeTrackIndex)
				{
					num = m;
					break;
				}
			}
		}"""

content = content.replace(update_visuals_find, update_visuals_replace)

# 5. Fix GetTrackColWidth inside MouseMove and MouseDown
content = content.replace("GetTrackColWidth(i)", "GetTrackColWidth(_trackerCanvasIndices[i])")
# Wait, this might replace GetTrackColWidth(i) inside GenerateTrackerView?
# No, GenerateTrackerView uses GetTrackColWidth(num2) now.
# DrawTrackerGrid uses GetTrackColWidth(num12) and GetTrackColWidth(m) for the background grid!
# Oh wait, the background grid in DrawTrackerGrid:
# double num30 = num28 + (double)(n * GetTrackColWidth(m));
# Here, m is the loop over _trackXOffsets, which exactly corresponds to `_trackerCanvasIndices[m]`.
content = content.replace("GetTrackColWidth(m)", "GetTrackColWidth(_trackerCanvasIndices[m])")
# TrackerActiveColumnHighlight.Width = GetTrackColWidth(num)
content = content.replace("GetTrackColWidth(num)", "GetTrackColWidth(_trackerCanvasIndices[num])")

# 6. Fix _activeTrackIndex assignment in MouseDown
content = content.replace("_activeTrackIndex = i;", "_activeTrackIndex = _trackerAbsoluteIndices[i];")
content = content.replace("_activeTrackNotes[i]", "_activeTrackNotes[i]") # this is fine since _activeTrackNotes matches visible tracks

# 7. Add GenerateTrackerView() to eyeButton handlers
eye_checked_find = """			eyeButton.Checked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Visible;
				eyeButton.Foreground = new SolidColorBrush(Colors.LightGray);
			};"""

eye_checked_replace = """			eyeButton.Checked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Visible;
				eyeButton.Foreground = new SolidColorBrush(Colors.LightGray);
				if (ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
			};"""
content = content.replace(eye_checked_find, eye_checked_replace)

eye_unchecked_find = """			eyeButton.Unchecked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Hidden;
				eyeButton.Foreground = new SolidColorBrush(Colors.DimGray);
			};"""

eye_unchecked_replace = """			eyeButton.Unchecked += delegate
			{
				trackNotesCanvas.Visibility = Visibility.Hidden;
				eyeButton.Foreground = new SolidColorBrush(Colors.DimGray);
				if (ViewTrackerMenu != null && ViewTrackerMenu.IsChecked) GenerateTrackerView();
			};"""
content = content.replace(eye_unchecked_find, eye_unchecked_replace)


with open("MainWindow.xaml.cs", "w", encoding="utf-8") as f:
    f.write(content)

print("Refactoring for tracker visibility complete.")
