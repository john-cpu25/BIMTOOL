using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.ExportExcel.Models;

namespace RincoNhan.Tools.ExportExcel.ViewModels
{
    public partial class ExportExcelViewModel : ObservableObject
    {
        private readonly Document _doc;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private bool _selectAll = true;

        [ObservableProperty]
        private bool _exportAsIndividualFiles = false;

        [ObservableProperty]
        private string _selectedFormat = "XLSX";

        public ObservableCollection<ScheduleModel> Schedules { get; }

        public ExportExcelViewModel(Document doc)
        {
            _doc = doc;
            Schedules = new ObservableCollection<ScheduleModel>();
            LoadSchedules();
        }

        private void LoadSchedules()
        {
            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsInternalKeynoteSchedule && !s.IsTitleblockRevisionSchedule)
                .OrderBy(s => s.Name)
                .Select(s => new ScheduleModel(s))
                .ToList();

            foreach (var s in schedules)
            {
                s.PropertyChanged += (sender, e) => 
                {
                    if (e.PropertyName == nameof(ScheduleModel.IsSelected))
                    {
                        CheckSelectAllState();
                    }
                };
                Schedules.Add(s);
            }

            StatusMessage = $"Found {Schedules.Count} schedules.";
        }

        private bool _isUpdatingSelectAll;

        private void CheckSelectAllState()
        {
            if (_isUpdatingSelectAll) return;

            var allSelected = Schedules.All(s => s.IsSelected);
            var noneSelected = Schedules.All(s => !s.IsSelected);
            
            if (allSelected || noneSelected)
            {
                _isUpdatingSelectAll = true;
                SelectAll = allSelected;
                _isUpdatingSelectAll = false;
            }
        }

        partial void OnSelectAllChanged(bool value)
        {
            if (_isUpdatingSelectAll) return;
            _isUpdatingSelectAll = true;

            foreach (var s in Schedules)
            {
                s.IsSelected = value;
            }

            _isUpdatingSelectAll = false;
        }

        [RelayCommand]
        private void Export()
        {
            var selectedSchedules = Schedules.Where(s => s.IsSelected).ToList();
            if (!selectedSchedules.Any())
            {
                StatusMessage = "No schedules selected to export.";
                return;
            }

            if (ExportAsIndividualFiles)
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select a folder to save the schedules";
                    dialog.ShowNewFolderButton = true;
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            StatusMessage = "Exporting...";
                            if (SelectedFormat == "CSV")
                            {
                                ExportSchedulesAsIndividualCsvs(selectedSchedules, dialog.SelectedPath);
                            }
                            else
                            {
                                ExportSchedulesAsIndividualFiles(selectedSchedules, dialog.SelectedPath);
                            }
                            StatusMessage = "Exported successfully!";
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = "Error: " + ex.Message;
                        }
                    }
                }
            }
            else
            {
                using (var dialog = new SaveFileDialog())
                {
                    bool isCsv = SelectedFormat == "CSV";
                    dialog.Filter = isCsv ? "CSV Files|*.csv" : "Excel Files|*.xlsx";
                    dialog.Title = isCsv ? "Save Schedules to CSV" : "Save Schedules to Excel";
                    dialog.FileName = isCsv ? "ProjectSchedules.csv" : "ProjectSchedules.xlsx";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            StatusMessage = "Exporting...";
                            if (isCsv)
                            {
                                ExportSchedulesToCsv(selectedSchedules, dialog.FileName);
                            }
                            else
                            {
                                ExportSchedulesToExcel(selectedSchedules, dialog.FileName);
                            }
                            StatusMessage = "Exported successfully!";
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = "Error: " + ex.Message;
                        }
                    }
                }
            }
        }

        private void ExportSchedulesToCsv(List<ScheduleModel> selectedSchedules, string filePath)
        {
            using (var writer = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                bool first = true;
                foreach (var scheduleModel in selectedSchedules)
                {
                    if (!first)
                    {
                        writer.WriteLine(); // empty line between schedules
                    }
                    first = false;

                    writer.WriteLine(EscapeCsv(scheduleModel.Schedule.Name));

                    var tableData = scheduleModel.Schedule.GetTableData();
                    WriteCsvSection(writer, scheduleModel.Schedule, tableData, SectionType.Header);
                    WriteCsvSection(writer, scheduleModel.Schedule, tableData, SectionType.Body);
                }
            }
        }

        private void ExportSchedulesAsIndividualCsvs(List<ScheduleModel> selectedSchedules, string folderPath)
        {
            foreach (var scheduleModel in selectedSchedules)
            {
                string fileName = SanitizeFileName(scheduleModel.Schedule.Name) + ".csv";
                string filePath = System.IO.Path.Combine(folderPath, fileName);
                
                int suffix = 1;
                string uniqueFilePath = filePath;
                while (System.IO.File.Exists(uniqueFilePath))
                {
                    string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    uniqueFilePath = System.IO.Path.Combine(folderPath, $"{nameWithoutExt} ({suffix}).csv");
                    suffix++;
                }

                using (var writer = new System.IO.StreamWriter(uniqueFilePath, false, System.Text.Encoding.UTF8))
                {
                    var tableData = scheduleModel.Schedule.GetTableData();
                    WriteCsvSection(writer, scheduleModel.Schedule, tableData, SectionType.Header);
                    WriteCsvSection(writer, scheduleModel.Schedule, tableData, SectionType.Body);
                }
            }
        }

        private void WriteCsvSection(System.IO.StreamWriter writer, ViewSchedule schedule, TableData tableData, SectionType sectionType)
        {
            var sectionData = tableData.GetSectionData(sectionType);
            if (sectionData != null)
            {
                for (int r = 0; r < sectionData.NumberOfRows; r++)
                {
                    List<string> rowCells = new List<string>();
                    for (int c = 0; c < sectionData.NumberOfColumns; c++)
                    {
                        rowCells.Add(EscapeCsv(schedule.GetCellText(sectionType, r, c)));
                    }
                    writer.WriteLine(string.Join(",", rowCells));
                }
            }
        }

        private string EscapeCsv(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\"", "\"\"");
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return $"\"{text}\"";
            }
            return text;
        }

        private void ExportSchedulesToExcel(List<ScheduleModel> selectedSchedules, string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                List<string> existingSheetNames = new List<string>();

                foreach (var scheduleModel in selectedSchedules)
                {
                    var schedule = scheduleModel.Schedule;
                    string safeName = GetUniqueSheetName(schedule.Name, existingSheetNames);
                    var ws = workbook.Worksheets.Add(safeName);
                    PopulateAndFormatWorksheet(schedule, ws);
                }

                workbook.SaveAs(filePath);
            }
        }

        private void ExportSchedulesAsIndividualFiles(List<ScheduleModel> selectedSchedules, string folderPath)
        {
            foreach (var scheduleModel in selectedSchedules)
            {
                using (var workbook = new XLWorkbook())
                {
                    var schedule = scheduleModel.Schedule;
                    string safeName = GetUniqueSheetName(schedule.Name, new List<string>());
                    var ws = workbook.Worksheets.Add(safeName);
                    PopulateAndFormatWorksheet(schedule, ws);
                    
                    string fileName = SanitizeFileName(schedule.Name) + ".xlsx";
                    string filePath = System.IO.Path.Combine(folderPath, fileName);
                    
                    int suffix = 1;
                    string uniqueFilePath = filePath;
                    while (System.IO.File.Exists(uniqueFilePath))
                    {
                        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                        uniqueFilePath = System.IO.Path.Combine(folderPath, $"{nameWithoutExt} ({suffix}).xlsx");
                        suffix++;
                    }

                    workbook.SaveAs(uniqueFilePath);
                }
            }
        }

        private void PopulateAndFormatWorksheet(ViewSchedule schedule, IXLWorksheet ws)
        {
            var tableData = schedule.GetTableData();
            int currentRow = 1;
            int maxCol = 1;
            int headerRowCount = 0;

            // Export Header
            var headerData = tableData.GetSectionData(SectionType.Header);
            if (headerData != null)
            {
                headerRowCount = headerData.NumberOfRows;
                for (int r = 0; r < headerData.NumberOfRows; r++)
                {
                    for (int c = 0; c < headerData.NumberOfColumns; c++)
                    {
                        ws.Cell(currentRow, c + 1).Value = schedule.GetCellText(SectionType.Header, r, c);
                        if (c + 1 > maxCol) maxCol = c + 1;
                    }
                    currentRow++;
                }
            }

            // Export Body
            var bodyData = tableData.GetSectionData(SectionType.Body);
            if (bodyData != null)
            {
                for (int r = 0; r < bodyData.NumberOfRows; r++)
                {
                    for (int c = 0; c < bodyData.NumberOfColumns; c++)
                    {
                        ws.Cell(currentRow, c + 1).Value = schedule.GetCellText(SectionType.Body, r, c);
                        if (c + 1 > maxCol) maxCol = c + 1;
                    }
                    currentRow++;
                }
            }

            ws.Columns().AdjustToContents();

            // Formatting
            var usedRange = ws.RangeUsed();
            if (usedRange != null)
            {
                usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                usedRange.Style.Border.OutsideBorderColor = XLColor.Black;
                usedRange.Style.Border.InsideBorderColor = XLColor.Black;

                if (headerRowCount > 0)
                {
                    var headerRange = ws.Range(1, 1, headerRowCount, maxCol);
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Font.Bold = true;
                }
            }
        }

        private string SanitizeFileName(string name)
        {
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
                name = name.Replace(c.ToString(), "");
            return name;
        }

        private string GetUniqueSheetName(string name, List<string> existingNames)
        {
            string invalidChars = "[]*/\\?";
            foreach (char c in invalidChars)
                name = name.Replace(c.ToString(), "");
            
            if (name.Length > 31)
                name = name.Substring(0, 31);
                
            string uniqueName = name;
            int suffix = 1;
            while (existingNames.Contains(uniqueName, StringComparer.OrdinalIgnoreCase))
            {
                string suffixStr = $"({suffix})";
                if (name.Length + suffixStr.Length > 31)
                    uniqueName = name.Substring(0, 31 - suffixStr.Length) + suffixStr;
                else
                    uniqueName = name + suffixStr;
                suffix++;
            }
            existingNames.Add(uniqueName);
            return uniqueName;
        }
    }
}
