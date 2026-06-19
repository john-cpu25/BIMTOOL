using System;
using System.IO;
using System.Text.Json;

namespace RincoNhan.Tools.Align_Dim.Models
{
    public class AlignDimSettings
    {
        public double DistanceMm { get; set; } = 1000.0;

        private static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "RincoNhan", "BIMTOOL");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "AlignDimSettings.json");
        }

        public static AlignDimSettings Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AlignDimSettings>(json);
                    if (settings != null)
                        return settings;
                }
            }
            catch
            {
                // Ignore load errors and return default
            }
            return new AlignDimSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
