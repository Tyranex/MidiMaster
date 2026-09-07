using System;
using System.IO;
using System.Text.Json;

namespace SS14_MIDI_IDE
{
    public class UserPreferences
    {
        public int DefaultVelocity { get; set; } = 100;
        public bool ShowGridLines { get; set; } = true;
        public int RenderDelayMs { get; set; } = 150;
    }

    public static class PreferencesManager
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preferences.json");
        public static UserPreferences Current { get; private set; } = new UserPreferences();

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var prefs = JsonSerializer.Deserialize<UserPreferences>(json);
                    if (prefs != null)
                    {
                        Current = prefs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load preferences: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save preferences: {ex.Message}");
            }
        }
    }
}
