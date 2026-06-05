using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RincoNhan.Tools.SmartLinkCad
{
    /// <summary>
    /// Represents a saved preset of hidden layers.
    /// </summary>
    public class LayerPreset
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// List of hidden layers stored as "CadFileName::LayerName"
        /// </summary>
        public List<string> HiddenLayers { get; set; } = new List<string>();

        public string GetKey(string cadFileName, string layerName)
        {
            return cadFileName + "::" + layerName;
        }
    }

    /// <summary>
    /// Manages saving/loading layer visibility presets to disk.
    /// Presets are stored per-project in %APPDATA%\RincoNhan\SmartLinkCad\
    /// </summary>
    public class LayerPresetManager
    {
        private readonly string _presetFilePath;
        private List<LayerPreset> _presets = new List<LayerPreset>();

        // Separator between preset sections
        private const string PRESET_HEADER = "##PRESET##";
        private const string PRESET_DATE = "##DATE##";
        private const string PRESET_LAYER = "##LAYER##";

        public LayerPresetManager(string projectPath)
        {
            // Create unique folder per project
            string projectHash = GetProjectHash(projectPath);
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RincoNhan", "SmartLinkCad");

            if (!Directory.Exists(appDataDir))
                Directory.CreateDirectory(appDataDir);

            _presetFilePath = Path.Combine(appDataDir, projectHash + "_presets.dat");
            LoadFromFile();
        }

        private string GetProjectHash(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath))
                return "default";

            // Use project filename as identifier
            string fileName = Path.GetFileNameWithoutExtension(projectPath);
            // Remove invalid chars
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            return fileName;
        }

        public List<LayerPreset> GetPresets()
        {
            return _presets.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public void SavePreset(string name, List<CADLayerInfo> allLayers)
        {
            // Remove existing preset with same name
            _presets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            var preset = new LayerPreset
            {
                Name = name,
                CreatedAt = DateTime.Now,
                HiddenLayers = new List<string>()
            };

            foreach (var layer in allLayers)
            {
                if (!layer.IsVisible)
                {
                    preset.HiddenLayers.Add(preset.GetKey(layer.CadFileName, layer.LayerName));
                }
            }

            _presets.Add(preset);
            SaveToFile();
        }

        public void ApplyPreset(LayerPreset preset, List<CADLayerInfo> allLayers)
        {
            if (preset == null) return;

            // First, set all layers visible
            foreach (var layer in allLayers)
            {
                layer.IsVisible = true;
            }

            // Then hide layers from preset
            var hiddenSet = new HashSet<string>(preset.HiddenLayers, StringComparer.OrdinalIgnoreCase);
            var tempPreset = new LayerPreset();

            foreach (var layer in allLayers)
            {
                string key = tempPreset.GetKey(layer.CadFileName, layer.LayerName);
                if (hiddenSet.Contains(key))
                {
                    layer.IsVisible = false;
                }
            }
        }

        public void DeletePreset(string name)
        {
            _presets.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            SaveToFile();
        }

        private void SaveToFile()
        {
            try
            {
                using (var writer = new StreamWriter(_presetFilePath, false, System.Text.Encoding.UTF8))
                {
                    foreach (var preset in _presets)
                    {
                        writer.WriteLine(PRESET_HEADER + preset.Name);
                        writer.WriteLine(PRESET_DATE + preset.CreatedAt.ToString("o"));

                        foreach (var layer in preset.HiddenLayers)
                        {
                            writer.WriteLine(PRESET_LAYER + layer);
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadFromFile()
        {
            _presets.Clear();

            if (!File.Exists(_presetFilePath)) return;

            try
            {
                var lines = File.ReadAllLines(_presetFilePath, System.Text.Encoding.UTF8);
                LayerPreset current = null;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith(PRESET_HEADER))
                    {
                        current = new LayerPreset
                        {
                            Name = line.Substring(PRESET_HEADER.Length),
                            CreatedAt = DateTime.Now
                        };
                        _presets.Add(current);
                    }
                    else if (line.StartsWith(PRESET_DATE) && current != null)
                    {
                        DateTime dt;
                        if (DateTime.TryParse(line.Substring(PRESET_DATE.Length), out dt))
                            current.CreatedAt = dt;
                    }
                    else if (line.StartsWith(PRESET_LAYER) && current != null)
                    {
                        current.HiddenLayers.Add(line.Substring(PRESET_LAYER.Length));
                    }
                }
            }
            catch { }
        }
    }
}
