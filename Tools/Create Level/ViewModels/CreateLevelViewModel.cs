using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RincoNhan.Tools.CreateLevel.ViewModels
{
    public partial class CreateLevelViewModel : ObservableObject
    {
        private CreateLevelHandler _handler;
        private ExternalEvent _externalEvent;
        private Document _doc;
        private int _levelCounter = 1;

        [ObservableProperty]
        private ObservableCollection<LevelData> _levelDataList = new ObservableCollection<LevelData>();

        [ObservableProperty]
        private LevelData _selectedLevelData;

        [ObservableProperty]
        private bool _deleteExistingLevels;

        [ObservableProperty]
        private string _statusMessage = "Ready. Add levels to begin.";

        [ObservableProperty]
        private string _summaryText;

        [ObservableProperty]
        private ObservableCollection<ExistingLevelInfo> _existingLevels = new ObservableCollection<ExistingLevelInfo>();

        [ObservableProperty]
        private ObservableCollection<TemplateLevelItem> _templateLevels = new ObservableCollection<TemplateLevelItem>();

        [ObservableProperty]
        private TemplateLevelItem _selectedTemplateLevel;

        // Input fields for adding new level
        [ObservableProperty]
        private string _newLevelName = "LEVEL 1";

        [ObservableProperty]
        private string _newFloorHeight = "3300";

        public CreateLevelViewModel(CreateLevelHandler handler, Document doc)
        {
            _handler = handler;
            _doc = doc;
            _externalEvent = ExternalEvent.Create(_handler);

            _handler.NotifyStatus = msg =>
            {
                try { StatusMessage = msg; } catch { }
            };

            try
            {
                LoadExistingLevels();
            }
            catch (Exception ex)
            {
                StatusMessage = "Warning: Could not load existing levels. " + ex.Message;
            }
        }

        private void LoadExistingLevels()
        {
            ExistingLevels.Clear();
            TemplateLevels.Clear();

            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var l in levels)
            {
                double elevMm = Math.Round(l.Elevation * 304.8, 1);
                ExistingLevels.Add(new ExistingLevelInfo
                {
                    Name = l.Name,
                    Elevation = elevMm
                });

                TemplateLevels.Add(new TemplateLevelItem
                {
                    Name = $"{l.Name} ({elevMm:F0} mm)",
                    Id = l.Id
                });
            }

            if (TemplateLevels.Any())
            {
                SelectedTemplateLevel = TemplateLevels.First();
            }
        }

        [RelayCommand]
        private void AddRow()
        {
            try
            {
                string name = string.IsNullOrWhiteSpace(NewLevelName)
                    ? $"LEVEL {_levelCounter++}"
                    : NewLevelName.Trim().ToUpper();

                double floorHeight = 3300;
                if (double.TryParse(NewFloorHeight, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                {
                    floorHeight = parsed;
                }

                var newLevel = new LevelData
                {
                    Name = name,
                    FloorHeight = floorHeight,
                    IsSelected = true
                };
                newLevel.FloorHeightChangedCallback = RecalculateAllElevations;

                LevelDataList.Add(newLevel);
                RecalculateAllElevations();
                UpdateSummary();

                // Auto-suggest next level name (e.g. "Level 1" → "Level 2")
                NewLevelName = GetNextLevelName(name);
                StatusMessage = $"Added \"{name}\". Next → \"{NewLevelName}\"";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error adding level: " + ex.Message;
            }
        }

        /// <summary>
        /// Parses the current level name and suggests the next one by incrementing
        /// the trailing number. Supports patterns like:
        ///   "Level 1" → "Level 2"
        ///   "TẦNG 3"  → "TẦNG 4"
        ///   "B2"      → "B3"
        ///   "GROUND"  → "Level 2" (no number → fallback)
        /// </summary>
        private string GetNextLevelName(string currentName)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return $"LEVEL {LevelDataList.Count + 1}";

            // Match trailing number with optional space/separator before it
            var match = Regex.Match(currentName, @"^(.*?)(\s*)(\d+)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;      // "Level", "TẦNG", "B", etc.
                string separator = match.Groups[2].Value;    // space or empty
                int number = int.Parse(match.Groups[3].Value);
                return $"{prefix}{separator}{number + 1}";
            }

            // No trailing number found → fallback
            return $"LEVEL {LevelDataList.Count + 1}";
        }

        [RelayCommand]
        private void RemoveRow()
        {
            try
            {
                if (SelectedLevelData == null)
                {
                    StatusMessage = "Select a level to remove.";
                    return;
                }

                string removedName = SelectedLevelData.Name;
                LevelDataList.Remove(SelectedLevelData);
                RecalculateAllElevations();
                UpdateSummary();
                StatusMessage = $"Removed \"{removedName}\". Total: {LevelDataList.Count} levels.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error removing level: " + ex.Message;
            }
        }

        [RelayCommand]
        private void MoveUp()
        {
            try
            {
                if (SelectedLevelData == null) return;
                int index = LevelDataList.IndexOf(SelectedLevelData);
                if (index <= 0) return;

                LevelDataList.Move(index, index - 1);
                RecalculateAllElevations();
                UpdateSummary();
            }
            catch { }
        }

        [RelayCommand]
        private void MoveDown()
        {
            try
            {
                if (SelectedLevelData == null) return;
                int index = LevelDataList.IndexOf(SelectedLevelData);
                if (index < 0 || index >= LevelDataList.Count - 1) return;

                LevelDataList.Move(index, index + 1);
                RecalculateAllElevations();
                UpdateSummary();
            }
            catch { }
        }

        private void RecalculateAllElevations()
        {
            LevelDataHelper.RecalculateElevations(LevelDataList);
        }

        private void UpdateSummary()
        {
            try
            {
                var selected = LevelDataList.Where(l => l.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    SummaryText = "No levels selected";
                    return;
                }

                double minElev = selected.Min(l => l.Elevation);
                double maxElev = selected.Max(l => l.Elevation);
                SummaryText = $"{selected.Count} levels | Elevation: {minElev:F0} → {maxElev:F0} mm";
            }
            catch
            {
                SummaryText = "";
            }
        }

        [RelayCommand]
        private void CreateLevels()
        {
            try
            {
                var selected = LevelDataList.Where(l => l.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    StatusMessage = "No levels selected.";
                    return;
                }

                _handler.LevelsToCreate = selected;
                _handler.DeleteExistingLevels = DeleteExistingLevels;
                _handler.TemplateLevelId = SelectedTemplateLevel?.Id;
                _externalEvent.Raise();

                StatusMessage = "Creating levels...";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            try
            {
                foreach (var l in LevelDataList) l.IsSelected = true;
                var temp = new ObservableCollection<LevelData>(LevelDataList);
                LevelDataList = temp;
                ReattachCallbacks();
                UpdateSummary();
            }
            catch { }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            try
            {
                foreach (var l in LevelDataList) l.IsSelected = false;
                var temp = new ObservableCollection<LevelData>(LevelDataList);
                LevelDataList = temp;
                ReattachCallbacks();
                UpdateSummary();
            }
            catch { }
        }

        [RelayCommand]
        private void ClearAll()
        {
            try
            {
                LevelDataList.Clear();
                _levelCounter = 1;
                UpdateSummary();
                StatusMessage = "All levels cleared.";
            }
            catch { }
        }

        /// <summary>
        /// Re-attach the OnFloorHeightChanged callback after replacing the collection.
        /// </summary>
        private void ReattachCallbacks()
        {
            foreach (var l in LevelDataList)
            {
                l.FloorHeightChangedCallback = RecalculateAllElevations;
            }
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            string msg = ex.GetType().Name + ": " + ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg += "\n→ " + inner.GetType().Name + ": " + inner.Message;
                inner = inner.InnerException;
            }
            return msg;
        }
    }

    public class ExistingLevelInfo
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
    }

    public class TemplateLevelItem
    {
        public string Name { get; set; }
        public Autodesk.Revit.DB.ElementId Id { get; set; }
    }
}
