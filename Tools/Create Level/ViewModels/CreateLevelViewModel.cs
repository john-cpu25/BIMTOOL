using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using ClosedXML.Excel;
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
            LoadLevelData();
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

        public CreateLevelViewModel(CreateLevelHandler handler, Document doc)
        {
            _handler = handler;
            _doc = doc;
            _externalEvent = ExternalEvent.Create(_handler);

            _handler.NotifyStatus = msg => StatusMessage = msg;

            LoadExistingLevels();
        }

        private void LoadExistingLevels()
        {
            ExistingLevels.Clear();
            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var l in levels)
            {
                ExistingLevels.Add(new ExistingLevelInfo
                {
                    Name = l.Name,
                    Elevation = Math.Round(l.Elevation * 304.8, 1) // feet to mm
                });
            }
        }

        [RelayCommand]
        private void Browse()
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

        private void LoadSheets()
        {
            Worksheets.Clear();
            try
            {
                using (var fs = new FileStream(ExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var wb = new XLWorkbook(fs))
                    {
                        foreach (var ws in wb.Worksheets)
                        {
                            Worksheets.Add(ws.Name);
                        }
                    }
                }
                if (Worksheets.Any())
                {
                    SelectedWorksheet = Worksheets.First();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error reading Excel: " + ex.Message;
            }
        }

        private void LoadLevelData()
        {
            LevelDataList.Clear();
            if (string.IsNullOrEmpty(ExcelFilePath) || string.IsNullOrEmpty(SelectedWorksheet)) return;

            try
            {
                using (var fs = new FileStream(ExcelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var wb = new XLWorkbook(fs))
                    {
                        var ws = wb.Worksheet(SelectedWorksheet);
                        if (ws == null) return;

                        var range = ws.RangeUsed();
                        if (range == null) return;

                        int firstRow = range.FirstRow().RowNumber();
                        int lastRow = range.LastRow().RowNumber();

                        // Read data: Column A = Name, Column B = Floor height (mm)
                        var rawData = new List<(string name, double? height)>();

                        for (int r = firstRow; r <= lastRow; r++)
                        {
                            string name = ws.Cell(r, 1).GetFormattedString()?.Trim();
                            if (string.IsNullOrEmpty(name)) continue;

                            double? height = null;
                            var heightCell = ws.Cell(r, 2);
                            if (!heightCell.IsEmpty())
                            {
                                if (heightCell.TryGetValue(out double h))
                                {
                                    height = h;
                                }
                                else
                                {
                                    // Try parse from formatted string
                                    string hStr = heightCell.GetFormattedString()?.Trim();
                                    if (double.TryParse(hStr, out double parsed))
                                    {
                                        height = parsed;
                                    }
                                }
                            }

                            rawData.Add((name, height));
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

                            // Add this level's floor height for the next level
                            if (item.height.HasValue)
                            {
                                cumulativeElevation += item.height.Value;
                            }
                        }

                        UpdateSummary();
                        StatusMessage = $"Loaded {LevelDataList.Count} levels from Excel.";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading data: " + ex.Message;
            }
        }

        private void UpdateSummary()
        {
            int count = LevelDataList.Count(l => l.IsSelected);
            double minElev = LevelDataList.Where(l => l.IsSelected).Min(l => l.Elevation);
            double maxElev = LevelDataList.Where(l => l.IsSelected).Max(l => l.Elevation);
            SummaryText = $"{count} levels | Elevation: {minElev:F0} → {maxElev:F0} mm";
        }

        [RelayCommand]
        private void CreateLevels()
        {
            var selected = LevelDataList.Where(l => l.IsSelected).ToList();
            if (selected.Count == 0)
            {
                StatusMessage = "No levels selected.";
                return;
            }

            _handler.LevelsToCreate = selected;
            _handler.DeleteExistingLevels = DeleteExistingLevels;
            _externalEvent.Raise();

            StatusMessage = "Creating levels...";
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var l in LevelDataList) l.IsSelected = true;
            // Force refresh
            var temp = new ObservableCollection<LevelData>(LevelDataList);
            LevelDataList = temp;
            UpdateSummary();
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var l in LevelDataList) l.IsSelected = false;
            var temp = new ObservableCollection<LevelData>(LevelDataList);
            LevelDataList = temp;
            UpdateSummary();
        }
    }

    public class ExistingLevelInfo
    {
        public string Name { get; set; }
        public double Elevation { get; set; }
    }
}
