using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
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

        [ObservableProperty]
        private string _excelFilePath;

        [ObservableProperty]
        private ObservableCollection<string> _worksheets = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedWorksheet;

        partial void OnSelectedWorksheetChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                LoadLevelData();
            }
        }

        [ObservableProperty]
        private ObservableCollection<LevelData> _levelDataList = new ObservableCollection<LevelData>();

        [ObservableProperty]
        private bool _deleteExistingLevels;

        [ObservableProperty]
        private string _statusMessage = "Ready. Select an Excel file to begin.";

        [ObservableProperty]
        private string _summaryText;

        [ObservableProperty]
        private ObservableCollection<ExistingLevelInfo> _existingLevels = new ObservableCollection<ExistingLevelInfo>();

        [ObservableProperty]
        private ObservableCollection<TemplateLevelItem> _templateLevels = new ObservableCollection<TemplateLevelItem>();

        [ObservableProperty]
        private TemplateLevelItem _selectedTemplateLevel;

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
        private void Browse()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    Title = "Select Excel File with Level Data"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExcelFilePath = dialog.FileName;
                    LoadSheets();
                }
            }
            catch (Exception ex)
            {
                string msg = GetFullExceptionMessage(ex);
                StatusMessage = msg;
                System.Windows.MessageBox.Show(msg, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadSheets()
        {
            Worksheets.Clear();
            LevelDataList.Clear();
            SummaryText = "";

            try
            {
                // Use SimpleExcelReader (built-in .NET, no ClosedXML)
                var sheetNames = SimpleExcelReader.GetSheetNames(ExcelFilePath);

                foreach (var name in sheetNames)
                {
                    Worksheets.Add(name);
                }

                if (Worksheets.Any())
                {
                    SelectedWorksheet = Worksheets.First();
                }
                else
                {
                    StatusMessage = "No worksheets found in the Excel file.";
                }
            }
            catch (Exception ex)
            {
                string msg = GetFullExceptionMessage(ex);
                StatusMessage = msg;
                System.Windows.MessageBox.Show(msg, "Error Loading Excel", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadLevelData()
        {
            LevelDataList.Clear();
            SummaryText = "";

            if (string.IsNullOrEmpty(ExcelFilePath) || string.IsNullOrEmpty(SelectedWorksheet))
            {
                return;
            }

            try
            {
                // Use SimpleExcelReader (built-in .NET, no ClosedXML)
                var rawData = SimpleExcelReader.ReadLevelData(ExcelFilePath, SelectedWorksheet);

                if (rawData.Count == 0)
                {
                    StatusMessage = "No level data found in the worksheet.";
                    return;
                }

                // Calculate cumulative elevation
                // GROUND (first level) = elevation 0
                // Each subsequent level = previous elevation + previous floor height
                double cumulativeElevation = 0;

                for (int i = 0; i < rawData.Count; i++)
                {
                    var item = rawData[i];
                    var levelData = new LevelData
                    {
                        Name = item.name,
                        FloorHeight = item.height ?? 0,
                        Elevation = cumulativeElevation,
                        IsSelected = true
                    };

                    LevelDataList.Add(levelData);

                    if (item.height.HasValue)
                    {
                        cumulativeElevation += item.height.Value;
                    }
                }

                UpdateSummary();
                StatusMessage = $"Loaded {LevelDataList.Count} levels from Excel.";
            }
            catch (Exception ex)
            {
                string msg = GetFullExceptionMessage(ex);
                StatusMessage = msg;
            }
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
                UpdateSummary();
            }
            catch { }
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
