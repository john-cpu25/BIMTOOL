using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.CreateViewSheet.Models;

namespace RincoNhan.Tools.CreateViewSheet.ViewModels
{
    public partial class CreateViewSheetViewModel : ObservableObject
    {
        private CreateViewSheetHandler _handler;

        // Shared Data
        public ObservableCollection<LevelItem> ProjectLevels { get; set; } = new ObservableCollection<LevelItem>();
        public ObservableCollection<ViewTypeItem> ViewTypes { get; set; } = new ObservableCollection<ViewTypeItem>();
        public ObservableCollection<ViewTemplateItem> ViewTemplates { get; set; } = new ObservableCollection<ViewTemplateItem>();
        public ObservableCollection<TitleBlockItem> TitleBlocks { get; set; } = new ObservableCollection<TitleBlockItem>();
        public ObservableCollection<ViewItem> ProjectViews { get; set; } = new ObservableCollection<ViewItem>();
        public ObservableCollection<SheetItem> ProjectSheets { get; set; } = new ObservableCollection<SheetItem>();

        // Tab 1: Create View
        public ObservableCollection<CreateViewRow> CreateViewRows { get; set; } = new ObservableCollection<CreateViewRow>();
        public DuplicateMode[] DuplicateModes { get; } = (DuplicateMode[])Enum.GetValues(typeof(DuplicateMode));

        // Tab 2: Create Sheet
        public ObservableCollection<CreateSheetRow> CreateSheetRows { get; set; } = new ObservableCollection<CreateSheetRow>();
        [ObservableProperty] private TitleBlockItem _selectedTitleBlock;
        public ObservableCollection<string> SheetSeriesOptions { get; set; } = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedSheetSeries;

        // Tab 3: Add & Align View
        [ObservableProperty] private bool _stackViews;
        [ObservableProperty] private ViewItem _templateViewForAlign;

        [ObservableProperty] private string _statusMessage = "Ready.";

        public CreateViewSheetViewModel(CreateViewSheetHandler handler, Document doc)
        {
            _handler = handler;
            _handler.ViewModel = this;

            LoadData(doc);
        }

        public void SetStatus(string msg)
        {
            StatusMessage = msg;
        }

        private void LoadData(Document doc)
        {
            // Load Levels
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            foreach (var l in levels)
            {
                ProjectLevels.Add(new LevelItem { Name = l.Name, Id = l.Id, Elevation = l.Elevation });
            }

            // Load View Types (FloorPlan, CeilingPlan, StructuralPlan)
            var viewTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .Where(vt => vt.ViewFamily == ViewFamily.FloorPlan
                          || vt.ViewFamily == ViewFamily.CeilingPlan
                          || vt.ViewFamily == ViewFamily.StructuralPlan)
                .OrderBy(vt => vt.Name)
                .ToList();

            foreach (var vt in viewTypes)
            {
                ViewTypes.Add(new ViewTypeItem { Name = vt.Name, Id = vt.Id });
            }

            // Load View Templates
            var viewTemplates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            ViewTemplates.Add(new ViewTemplateItem { Name = "<None>", Id = ElementId.InvalidElementId });
            foreach (var vt in viewTemplates)
            {
                ViewTemplates.Add(new ViewTemplateItem { Name = vt.Name, Id = vt.Id });
            }

            // Load TitleBlocks
            var titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .OrderBy(tb => tb.Name)
                .ToList();

            foreach (var tb in titleBlocks)
            {
                TitleBlocks.Add(new TitleBlockItem { Name = $"{tb.FamilyName} - {tb.Name}", Id = tb.Id });
            }
            if (TitleBlocks.Any()) SelectedTitleBlock = TitleBlocks.First();

            // Load Sheet Series from existing sheets' titleblock parameter
            LoadSheetSeries(doc);

            // Load Views (Plan Views)
            LoadViewsAndSheets(doc);
        }

        private void LoadSheetSeries(Document doc)
        {
            SheetSeriesOptions.Clear();

            var seriesValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Single query for ALL titleblock instances in the project (fast)
            var allTitleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var tbInstance in allTitleBlocks)
            {
                var param = tbInstance.LookupParameter("RINCO_TB_SHEET SERIES");
                if (param != null && param.HasValue && !string.IsNullOrWhiteSpace(param.AsString()))
                {
                    seriesValues.Add(param.AsString());
                }
            }

            // Add defaults if none found
            if (!seriesValues.Any())
            {
                seriesValues.Add("S0000 SERIES - GENERAL");
                seriesValues.Add("S1000 SERIES - SITE RETENTION & FOUNDATION PLANS");
                seriesValues.Add("S2000 SERIES - GENERAL ARRANGEMENT PLANS");
                seriesValues.Add("S3000 SERIES - WALL ELEVATIONS");
                seriesValues.Add("S4000 SERIES - CONCRETE DETAILS");
                seriesValues.Add("S5000 SERIES - PEO & PT PLANS");
            }

            foreach (var s in seriesValues.OrderBy(x => x))
            {
                SheetSeriesOptions.Add(s);
            }

            // Default to GA Plans
            SelectedSheetSeries = SheetSeriesOptions.FirstOrDefault(s => s.Contains("GENERAL ARRANGEMENT"))
                                  ?? SheetSeriesOptions.FirstOrDefault();
        }

        private void LoadViewsAndSheets(Document doc)
        {
            ProjectViews.Clear();
            ProjectSheets.Clear();

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate
                    && (v.ViewType == ViewType.FloorPlan
                     || v.ViewType == ViewType.CeilingPlan
                     || v.ViewType == ViewType.EngineeringPlan))
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var v in views)
            {
                ProjectViews.Add(new ViewItem { Name = v.Name, Id = v.Id });
            }

            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            foreach (var s in sheets)
            {
                var seriesParam = s.LookupParameter("RINCO_TB_SHEET SERIES");
                string series = seriesParam != null ? (seriesParam.AsString() ?? "") : "";
                ProjectSheets.Add(new SheetItem { SheetNumber = s.SheetNumber, SheetName = s.Name, SheetSeries = series, Id = s.Id });
            }
        }

        /// <summary>
        /// Called from handler after Revit transactions to refresh data.
        /// </summary>
        public void RefreshAllData(Document doc)
        {
            LoadViewsAndSheets(doc);
        }

        // ================= Tab 1: Create View =================

        private bool _isSyncingLevel = false;

        private void AddGroupedViewRow(LevelItem level, string suffix, string viewTypeKey, string templateKey, string groupId)
        {
            var matchedViewType = ViewTypes.FirstOrDefault(vt =>
                vt.Name.Equals(viewTypeKey, StringComparison.OrdinalIgnoreCase))
                ?? ViewTypes.FirstOrDefault();

            var matchedTemplate = ViewTemplates.FirstOrDefault(vt =>
                vt.Name.Equals(templateKey, StringComparison.OrdinalIgnoreCase))
                ?? ViewTemplates.FirstOrDefault();

            var row = new CreateViewRow
            {
                SelectedLevel = level,
                Suffix = suffix,
                SelectedViewType = matchedViewType,
                SelectedViewTemplate = matchedTemplate,
                DuplicateMode = DuplicateMode.Duplicate,
                GroupId = groupId
            };

            row.OnLevelChangedCallback = (changedRow, newLevel) =>
            {
                if (_isSyncingLevel) return;
                _isSyncingLevel = true;
                foreach (var r in CreateViewRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.SelectedLevel = newLevel;
                }
                _isSyncingLevel = false;
            };

            CreateViewRows.Add(row);
        }

        [RelayCommand]
        private void AddCreateViewRow()
        {
            string groupId = Guid.NewGuid().ToString();
            var level = ProjectLevels.FirstOrDefault();

            AddGroupedViewRow(level, "- OVER", "OVER_PLANS", "RINCO - GA - OVER 1/100", groupId);
            AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", "RINCO - GA - UNDER 1/100", groupId);
            AddGroupedViewRow(level, "- ARCH", "ARCH OVERLAY_PLANS", "RINCO - GA - ARCH 1/100", groupId);
        }

        [RelayCommand]
        private void AddAllLevelsToCreateView()
        {
            CreateViewRows.Clear();

            foreach (var level in ProjectLevels)
            {
                string groupId = Guid.NewGuid().ToString();
                AddGroupedViewRow(level, "- OVER", "OVER_PLANS", "RINCO - GA - OVER 1/100", groupId);
                AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", "RINCO - GA - UNDER 1/100", groupId);
                AddGroupedViewRow(level, "- ARCH", "ARCH OVERLAY_PLANS", "RINCO - GA - ARCH 1/100", groupId);
            }

            StatusMessage = $"Added {CreateViewRows.Count} rows ({ProjectLevels.Count} levels × 3 types).";
        }

        [RelayCommand]
        private void RemoveCreateViewRow(CreateViewRow row)
        {
            if (row != null && CreateViewRows.Contains(row))
            {
                CreateViewRows.Remove(row);
            }
        }

        [RelayCommand]
        private void GenerateViews()
        {
            var rowsToProcess = CreateViewRows.Where(r => r.IsSelected && r.SelectedLevel != null).ToList();
            if (!rowsToProcess.Any())
            {
                StatusMessage = "No valid rows selected to create views.";
                return;
            }

            _handler.RequestAction = "CREATE_VIEWS";
            _handler.Raise();
        }

        // ================= Tab 2: Create Sheet =================

        [RelayCommand]
        private void AddCreateSheetRow()
        {
            var level = ProjectLevels.FirstOrDefault();
            var row = new CreateSheetRow
            {
                SelectedLevel = level,
                Suffix = "GENERAL ARRANGEMENT PLAN",
                SheetNumber = "",
            };
            CreateSheetRows.Add(row);
        }

        [RelayCommand]
        private void AddAllLevelsToCreateSheet()
        {
            CreateSheetRows.Clear();
            var allLevels = ProjectLevels.ToList();

            foreach (var level in ProjectLevels)
            {
                var row = new CreateSheetRow
                {
                    SelectedLevel = level,
                    Suffix = "GENERAL ARRANGEMENT PLAN",
                    SheetNumber = GenerateSheetNumber(level, allLevels),
                };
                CreateSheetRows.Add(row);
            }
            StatusMessage = $"Added {CreateSheetRows.Count} rows ({ProjectLevels.Count} levels).";
        }

        private string GenerateSheetNumber(LevelItem level, List<LevelItem> allLevels)
        {
            string name = level.Name.ToUpper().Trim();

            // BASEMENT levels → S4099 (multiple basements count down: S4098, S4099)
            if (name.Contains("BASEMENT"))
            {
                var basements = allLevels
                    .Where(l => l.Name.ToUpper().Contains("BASEMENT"))
                    .OrderBy(l => l.Elevation)
                    .ToList();
                int basementCount = basements.Count;
                int index = basements.IndexOf(level);
                return $"S{4099 - (basementCount - 1 - index)}";
            }

            // GROUND level → S4100
            if (name.Contains("GROUND"))
            {
                return "S4100";
            }

            // LEVEL X → S4100 + X (e.g. LEVEL 1 → S4101, LEVEL 2 → S4102)
            var match = Regex.Match(name, @"LEVEL\s*(\d+)");
            if (match.Success)
            {
                int levelNum = int.Parse(match.Groups[1].Value);
                return $"S{4100 + levelNum}";
            }

            // Other levels (ROOF, etc.) → continue after the highest LEVEL number
            int maxLevelNum = 0;
            foreach (var l in allLevels)
            {
                var m = Regex.Match(l.Name.ToUpper(), @"LEVEL\s*(\d+)");
                if (m.Success)
                {
                    int num = int.Parse(m.Groups[1].Value);
                    if (num > maxLevelNum) maxLevelNum = num;
                }
            }

            var otherLevels = allLevels
                .Where(l => !l.Name.ToUpper().Contains("BASEMENT")
                         && !l.Name.ToUpper().Contains("GROUND")
                         && !Regex.IsMatch(l.Name.ToUpper(), @"LEVEL\s*\d+"))
                .OrderBy(l => l.Elevation)
                .ToList();
            int otherIndex = otherLevels.IndexOf(level);

            return $"S{4100 + maxLevelNum + 1 + otherIndex}";
        }

        [RelayCommand]
        private void RemoveCreateSheetRow(CreateSheetRow row)
        {
            if (row != null && CreateSheetRows.Contains(row))
            {
                CreateSheetRows.Remove(row);
            }
        }

        [RelayCommand]
        private void GenerateSheets()
        {
            var rowsToProcess = CreateSheetRows.Where(r => r.IsSelected && r.SelectedLevel != null).ToList();
            if (!rowsToProcess.Any() || SelectedTitleBlock == null)
            {
                StatusMessage = "No valid rows selected or Titleblock missing.";
                return;
            }

            _handler.RequestAction = "CREATE_SHEETS";
            _handler.Raise();
        }

        // ================= Tab 3: Add & Align View =================

        [RelayCommand]
        private void AddViewsToSheets()
        {
            var selectedViews = ProjectViews.Where(v => v.IsSelected).ToList();
            var selectedSheets = ProjectSheets.Where(s => s.IsSelected).ToList();

            if (!selectedViews.Any() || !selectedSheets.Any())
            {
                StatusMessage = "Select at least one view and one sheet.";
                return;
            }

            _handler.RequestAction = "ADD_VIEWS_TO_SHEETS";
            _handler.Raise();
        }

        [RelayCommand]
        private void AlignViews()
        {
            if (TemplateViewForAlign == null)
            {
                StatusMessage = "Please select a Template View to align to.";
                return;
            }

            var selectedSheets = ProjectSheets.Where(s => s.IsSelected).ToList();
            if (!selectedSheets.Any())
            {
                StatusMessage = "Select at least one target sheet.";
                return;
            }

            _handler.RequestAction = "ALIGN_VIEWS";
            _handler.Raise();
        }
    }
}
