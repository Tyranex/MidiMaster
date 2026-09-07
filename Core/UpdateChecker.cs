using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SS14_MIDI_IDE
{
    public static class UpdateChecker
    {
        // Replace this URL with the location where you host your update JSON file.
        // Example JSON structure:
        // {
        //   "latest_version": "1.2.0",
        //   "download_url": "https://yourdomain.com/downloads/MidiMaster_v1.2.0.exe"
        // }
        private const string UpdateUrl = "https://gist.githubusercontent.com/Tyranex/d9cb0a716e7c717fe8a9da9899ebe1e9/raw";
        
        public static async Task CheckForUpdatesAsync(bool manualCheck = true)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(UpdateUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var rawText = await response.Content.ReadAsStringAsync();
                        var latestTag = rawText.Trim();
                        
                        if (string.IsNullOrEmpty(latestTag)) return;
                        
                        // Remove leading 'v' if present
                        if (latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                        {
                            latestTag = latestTag.Substring(1);
                        }

                        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                        if (Version.TryParse(latestTag, out Version? latestVersion))
                        {
                            if (latestVersion > currentVersion)
                            {
                                SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show(
                                    $"A new version of MidiMaster is available!\n\nCurrent Version: {currentVersion}\nLatest Version: {latestVersion}\n\nPlease check the project page for the download.",
                                    "Update Available",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else if (manualCheck)
                            {
                                SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show(
                                    "You are running the latest version of MidiMaster.",
                                    "Up to Date",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        }
                        else if (manualCheck)
                        {
                            SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Failed to parse version number from the update server. Found: '{latestTag}'", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else if (manualCheck)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show("The update URL has not been configured yet (or the file is missing on your server).\n\nPlease replace 'UpdateUrl' in UpdateChecker.cs with your real JSON file URL.", "Update URL Not Configured", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Failed to check for updates: {response.StatusCode}", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (manualCheck)
                {
                    SS14_MIDI_IDE.UI.Windows.CustomMessageBox.Show($"Error checking for updates: {ex.Message}", "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
