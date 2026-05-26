using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Data;
using RincoNhan.Tools.ImportEXtoLegend.Models;
using System.Collections.Generic;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    public partial class ImportEXtoLegendViewModel : ObservableObject
    {
        private ImportEXtoLegendHandler _handler;
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
            UpdatePreview();
        }

        [ObservableProperty]
        private bool _isSplitEnabled;

        [ObservableProperty]
        private ObservableCollection<TitleblockItem> _titleblocks = new ObservableCollection<TitleblockItem>();

        [ObservableProperty]
        private TitleblockItem _selectedTitleblock;

        [ObservableProperty]
        private ObservableCollection<LegendItem> _legends = new ObservableCollection<LegendItem>();

        [ObservableProperty]
        private LegendItem _selectedLegend;

        [ObservableProperty]
        private ObservableCollection<TextNoteTypeItem> _textTypes = new ObservableCollection<TextNoteTypeItem>();

        [ObservableProperty]
        private TextNoteTypeItem _selectedTextType;

        [ObservableProperty]
        private ObservableCollection<double> _rowSpacings = new ObservableCollection<double> { 1.0, 1.2, 1.5, 2.0, 2.5, 3.0 };

        [ObservableProperty]
        private double _selectedRowSpacing = 1.2;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private DataTable _previewTable;

        public ExcelTableData WorkingData => _workingData;
        private ExcelTableData _workingData;

        public ImportEXtoLegendViewModel(ImportEXtoLegendHandler handler, Document doc)
        {
            _handler = handler;
            _doc = doc;
            _externalEvent = ExternalEvent.Create(_handler);
            
            _handler.NotifyStatus = msg => StatusMessage = msg;

            LoadTitleblocks(doc);
            LoadLegends(doc);
            LoadTextTypes(doc);
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(ExcelFilePath) || string.IsNullOrEmpty(SelectedWorksheet)) return;

            try
            {
                _workingData = ExcelReader.ReadTable(ExcelFilePath, SelectedWorksheet);
                
                var dt = new DataTable();
                // Create columns A, B, C...
                for (int i = 1; i <= _workingData.MaxCol; i++)
                {
                    string colName = GetColumnName(i);
                    dt.Columns.Add(colName);
                }

                // Fill rows
                for (int r = 1; r <= _workingData.MaxRow; r++)
                {
                    var row = dt.NewRow();
                    for (int c = 1; c <= _workingData.MaxCol; c++)
                    {
                        var cell = _workingData.Cells.FirstOrDefault(x => x.Row == r && x.Column == c);
                        row[c - 1] = cell?.Value ?? "";
                    }
                    dt.Rows.Add(row);
                }

                PreviewTable = dt;
            }
            catch (System.Exception ex)
            {
                StatusMessage = "Error loading preview: " + ex.Message;
            }
        }

        private string GetColumnName(int columnNumber)
        {
            int dividend = columnNumber;
            string columnName = string.Empty;
            int modulo;

            while (dividend > 0)
            {
                modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo).ToString() + columnName;
                dividend = (int)((dividend - modulo) / 26);
            }

            return columnName;
        }

        [RelayCommand]
        private void SetAlignment(string alignment)
        {
            // Note: In a real implementation with DataGrid selection, we'd need to pass the selected cells.
            // For now, we assume this command applies to the current "Active" cell if we can track it, 
            // or we'll update the logic to accept selection.
            // Since this is a specialized tool, I'll implement a way to set it for the selected items in the UI.
        }

        public void UpdateCellAlignment(int row, int col, HorizontalAlignmentType alignment)
        {
            if (_workingData == null) return;
            var cell = _workingData.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
            if (cell != null)
            {
                cell.HorizontalAlignment = alignment;
            }
            else
            {
                _workingData.Cells.Add(new ExcelCellData { Row = row, Column = col, Value = "", HorizontalAlignment = alignment });
            }
        }

        public void UpdateCellVerticalAlignment(int row, int col, VerticalAlignmentType alignment)
        {
            if (_workingData == null) return;
            var cell = _workingData.Cells.FirstOrDefault(c => c.Row == row && c.Column == col);
            if (cell != null)
            {
                cell.VerticalAlignment = alignment;
            }
            else
            {
                _workingData.Cells.Add(new ExcelCellData { Row = row, Column = col, Value = "", VerticalAlignment = alignment });
            }
        }

        public void UpdateColumnWidth(int colIndex, double newWidthPx)
        {
            if (_workingData == null) return;
            // 7.14 is our conversion factor defined in ExcelReader
            _workingData.ColumnWidths[colIndex + 1] = newWidthPx; 
        }

        private void LoadTextTypes(Document doc)
        {
            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .Select(t => new TextNoteTypeItem { Name = t.Name, Id = t.Id })
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var t in types) TextTypes.Add(t);
            if (TextTypes.Any()) SelectedTextType = TextTypes.First();
        }

        private void LoadLegends(Document doc)
        {
            var legends = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend)
                .Select(v => new LegendItem { Name = v.Name, Id = v.Id })
                .OrderBy(l => l.Name)
                .ToList();

            foreach (var l in legends) Legends.Add(l);
            if (Legends.Any()) SelectedLegend = Legends.First();
        }

        private void LoadTitleblocks(Document doc)
        {
            var tbs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .Select(t => new TitleblockItem { Name = $"{t.FamilyName} - {t.Name}", Id = t.Id })
                .ToList();

            foreach (var tb in tbs) Titleblocks.Add(tb);
            if (Titleblocks.Any()) SelectedTitleblock = Titleblocks.First();
        }

        [RelayCommand]
        private void Browse()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                Title = "Select Excel File"
            };

            if (dialog.ShowDialog() == true)
            {
                ExcelFilePath = dialog.FileName;
                var sheets = ExcelReader.GetSheetNames(ExcelFilePath);
                Worksheets.Clear();
                foreach (var s in sheets) Worksheets.Add(s);
                if (Worksheets.Any()) SelectedWorksheet = Worksheets.First();
            }
        }

        [RelayCommand]
        private void Import()
        {
            if (string.IsNullOrEmpty(ExcelFilePath) || string.IsNullOrEmpty(SelectedWorksheet))
            {
                StatusMessage = "Please select an Excel file and worksheet first.";
                return;
            }
            if (SelectedLegend == null)
            {
                StatusMessage = "Please select a target Legend view.";
                return;
            }

            if (SelectedTextType == null)
            {
                StatusMessage = "Please select a target Text Type.";
                return;
            }

            _handler.IsReload = false;
            _handler.ExcelFilePath = ExcelFilePath;
            _handler.WorksheetName = SelectedWorksheet;
            _handler.TargetLegendId = SelectedLegend.Id;
            _handler.TargetTextTypeId = SelectedTextType.Id;
            _handler.RowSpacingMultiplier = SelectedRowSpacing;
            _handler.IsSplitEnabled = IsSplitEnabled;
            _handler.ChosenTitleblockId = SelectedTitleblock?.Id;
            
            // Pass the pre-loaded data (with interactive edits)
            _handler.TableData = _workingData;

            _externalEvent.Raise();
        }

        [RelayCommand]
        private void Reload()
        {
            _handler.IsReload = true;
            _externalEvent.Raise();
        }
    }

    public class TitleblockItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class LegendItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class TextNoteTypeItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }
}
