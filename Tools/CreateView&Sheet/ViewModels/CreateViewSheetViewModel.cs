using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;
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
        [ObservableProperty] private string _alignMode = "View + Title"; // "View", "Title", "View + Title"

        // View Search/Filter
        private ICollectionView _filteredViews;
        public ICollectionView FilteredViews => _filteredViews;

        private string _viewSearchText = "";
        public string ViewSearchText
        {
            get => _viewSearchText;
            set
            {
                if (SetProperty(ref _viewSearchText, value))
                {
                    _filteredViews?.Refresh();
                }
            }
        }

        // Sheet Search/Filter
        private ICollectionView _filteredSheets;
        public ICollectionView FilteredSheets => _filteredSheets;

        private string _sheetSearchText = "";
        public string SheetSearchText
        {
            get => _sheetSearchText;
            set
            {
                if (SetProperty(ref _sheetSearchText, value))
                {
                    _filteredSheets?.Refresh();
                }
            }
        }
        // Tab Multi: Add Views Multi
        public ObservableCollection<MultiAddRow> MultiAddRows { get; set; } = new ObservableCollection<MultiAddRow>();
        private ICollectionView _filteredMultiRows;
        public ICollectionView FilteredMultiRows => _filteredMultiRows;

        private string _multiSheetSearchText = "";
        public string MultiSheetSearchText
        {
            get => _multiSheetSearchText;
            set
            {
                if (SetProperty(ref _multiSheetSearchText, value))
                {
                    _filteredMultiRows?.Refresh();
                }
            }
        }

        // ViewItem list with <None> option for ComboBox
        public ObservableCollection<ViewItem> ViewOptionsForMulti { get; set; } = new ObservableCollection<ViewItem>();

        // Tab 6: Open View Title
        public ObservableCollection<ViewportTitleRow> ViewportTitleRows { get; set; } = new ObservableCollection<ViewportTitleRow>();
        public ObservableCollection<ViewportTitleItem> ViewportTitleTypes { get; set; } = new ObservableCollection<ViewportTitleItem>();
        [ObservableProperty] private ViewportTitleItem _selectedViewportTitle;

        private ICollectionView _filteredTitleRows;
        public ICollectionView FilteredTitleRows => _filteredTitleRows;

        private string _titleSearchText = "";
        public string TitleSearchText
        {
            get => _titleSearchText;
            set
            {
                if (SetProperty(ref _titleSearchText, value))
                {
                    _filteredTitleRows?.Refresh();
                }
            }
        }

        // Tab 7: Scope Box
        public ObservableCollection<ScopeBoxViewRow> ScopeBoxViewRows { get; set; } = new ObservableCollection<ScopeBoxViewRow>();
        public ObservableCollection<ScopeBoxItem> ScopeBoxOptions { get; set; } = new ObservableCollection<ScopeBoxItem>();
        [ObservableProperty] private ScopeBoxItem _selectedScopeBox;

        private ICollectionView _filteredScopeBoxRows;
        public ICollectionView FilteredScopeBoxRows => _filteredScopeBoxRows;

        private string _scopeBoxSearchText = "";
        public string ScopeBoxSearchText
        {
            get => _scopeBoxSearchText;
            set
            {
                if (SetProperty(ref _scopeBoxSearchText, value))
                {
                    _filteredScopeBoxRows?.Refresh();
                }
            }
        }

        public ObservableCollection<string> ScopeBoxViewTypes { get; set; } = new ObservableCollection<string>();
        private string _selectedScopeBoxViewType;
        public string SelectedScopeBoxViewType
        {
            get => _selectedScopeBoxViewType;
            set
            {
                if (SetProperty(ref _selectedScopeBoxViewType, value))
                {
                    _filteredScopeBoxRows?.Refresh();
                }
            }
        }

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

            // Setup filtered view for search
            _filteredViews = CollectionViewSource.GetDefaultView(ProjectViews);
            _filteredViews.Filter = ViewFilter;
            OnPropertyChanged(nameof(FilteredViews));

            // Setup filtered sheets for search (with grouping)
            _filteredSheets = CollectionViewSource.GetDefaultView(ProjectSheets);
            _filteredSheets.Filter = SheetFilter;
            _filteredSheets.GroupDescriptions.Add(new PropertyGroupDescription("SheetSeries"));
            OnPropertyChanged(nameof(FilteredSheets));

            // Setup Multi tab data
            LoadMultiRows();

            // Setup Title tab data
            LoadViewportTitleData(doc);

            // Setup Scope Box tab data
            LoadScopeBoxData(doc);
        }

        private void LoadSheetSeries(Document doc)
        {
            SheetSeriesOptions.Clear();

            var seriesValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Single query for ALL sheets in the project
            var allSheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            foreach (var sheet in allSheets)
            {
                var param = sheet.LookupParameter("RINCO_TB_SHEET SERIES");
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
            LoadMultiRows();
            LoadViewportTitleData(doc);
            LoadScopeBoxData(doc);
        }

        // ================= Tab 1: Create View =================

        private bool _isSyncingLevel = false;

        private void AddGroupedViewRow(LevelItem level, string suffix, string viewTypeKey, string templateKey, string groupId)
        {
            var matchedViewType = ViewTypes.FirstOrDefault(vt =>
                vt.Name.Equals(viewTypeKey, StringComparison.OrdinalIgnoreCase));

            // Fuzzy matching based on Suffix if exact match fails
            if (matchedViewType == null)
            {
                if (suffix.IndexOf("OVER", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedViewType = ViewTypes.FirstOrDefault(vt => 
                        vt.Name.EndsWith("OVER", StringComparison.OrdinalIgnoreCase) || 
                        (vt.Name.IndexOf("OVER", StringComparison.OrdinalIgnoreCase) >= 0 && vt.Name.IndexOf("OVERLAY", StringComparison.OrdinalIgnoreCase) < 0));
                }
                else if (suffix.IndexOf("UNDER", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedViewType = ViewTypes.FirstOrDefault(vt => 
                        vt.Name.EndsWith("UNDER", StringComparison.OrdinalIgnoreCase) || 
                        vt.Name.IndexOf("UNDER", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                else if (suffix.IndexOf("ARCH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedViewType = ViewTypes.FirstOrDefault(vt => 
                        vt.Name.IndexOf("ARCH", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            // Fallback to first
            if (matchedViewType == null)
            {
                matchedViewType = ViewTypes.FirstOrDefault();
            }

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

        private bool ViewFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(ViewSearchText)) return true;
            if (obj is ViewItem view)
            {
                return view.Name.IndexOf(ViewSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        [RelayCommand]
        private void SelectAllViews()
        {
            foreach (var v in ProjectViews)
            {
                if (ViewFilter(v)) v.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllViews()
        {
            foreach (var v in ProjectViews)
            {
                v.IsSelected = false;
            }
        }

        private bool SheetFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(SheetSearchText)) return true;
            if (obj is SheetItem sheet)
            {
                return sheet.SheetNumber.IndexOf(SheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || sheet.SheetName.IndexOf(SheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || (sheet.SheetSeries != null && sheet.SheetSeries.IndexOf(SheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        [RelayCommand]
        private void SelectAllSheets()
        {
            foreach (var s in ProjectSheets)
            {
                if (SheetFilter(s)) s.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllSheets()
        {
            foreach (var s in ProjectSheets)
            {
                s.IsSelected = false;
            }
        }

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

        // ================= Tab Multi: Add Views Multi =================

        private bool MultiRowFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(MultiSheetSearchText)) return true;
            if (obj is MultiAddRow row)
            {
                return row.Sheet.SheetNumber.IndexOf(MultiSheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || row.Sheet.SheetName.IndexOf(MultiSheetSearchText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        public void LoadMultiRows()
        {
            MultiAddRows.Clear();
            foreach (var sheet in ProjectSheets)
            {
                MultiAddRows.Add(new MultiAddRow { Sheet = sheet });
            }

            // Setup filter + grouping
            _filteredMultiRows = CollectionViewSource.GetDefaultView(MultiAddRows);
            _filteredMultiRows.Filter = MultiRowFilter;
            _filteredMultiRows.GroupDescriptions.Clear();
            _filteredMultiRows.GroupDescriptions.Add(new PropertyGroupDescription("Sheet.SheetSeries"));
            OnPropertyChanged(nameof(FilteredMultiRows));

            // Build view options with None
            ViewOptionsForMulti.Clear();
            ViewOptionsForMulti.Add(null); // <None> option
            foreach (var v in ProjectViews)
            {
                ViewOptionsForMulti.Add(v);
            }
        }

        [RelayCommand]
        private void AutoMatchViews()
        {
            int matchedCount = 0;

            foreach (var row in MultiAddRows)
            {
                string level = ExtractLevelFromSheetName(row.Sheet.SheetName);
                if (string.IsNullOrEmpty(level))
                {
                    row.ArchView = null;
                    row.UnderView = null;
                    row.OverView = null;
                    continue;
                }

                row.ArchView = FindMatchingView(level, "ARCH");
                row.UnderView = FindMatchingView(level, "UNDER");
                row.OverView = FindMatchingView(level, "OVER");

                if (row.ArchView != null || row.UnderView != null || row.OverView != null)
                    matchedCount++;
            }

            int totalViews = MultiAddRows.Sum(r => (r.ArchView != null ? 1 : 0) + (r.UnderView != null ? 1 : 0) + (r.OverView != null ? 1 : 0));
            StatusMessage = $"Auto-matched: {matchedCount}/{MultiAddRows.Count} sheets, {totalViews} views found.";
        }

        private string ExtractLevelFromSheetName(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return null;

            string name = sheetName.Trim();

            // Remove common suffixes to extract level
            string[] suffixes = { "GENERAL ARRANGEMENT PLAN", "GENERAL ARRANGEMENT", "GA PLAN", "GA", "PLAN" };
            foreach (var suffix in suffixes)
            {
                int idx = name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    return name.Substring(0, idx).Trim();
                }
            }

            return name;
        }

        private ViewItem FindMatchingView(string level, string keyword)
        {
            string levelUpper = level.ToUpper().Trim();

            // Find views that contain the level AND keyword, excluding "Copy" and "LOADING"
            var candidates = ProjectViews.Where(v =>
            {
                string nameUpper = v.Name.ToUpper();
                return nameUpper.Contains(levelUpper)
                    && nameUpper.Contains(keyword.ToUpper())
                    && !nameUpper.Contains("COPY")
                    && !nameUpper.Contains("LOADING");
            }).ToList();

            // Prefer exact match pattern: "{Level} - GA - {Keyword}"
            var exact = candidates.FirstOrDefault(v =>
                v.Name.ToUpper().Contains($"{levelUpper} - GA - {keyword.ToUpper()}"));
            if (exact != null) return exact;

            // Fallback: "{Level} - {Keyword}"
            var fallback = candidates.FirstOrDefault(v =>
                v.Name.ToUpper().Contains($"{levelUpper} - {keyword.ToUpper()}"));
            if (fallback != null) return fallback;

            return candidates.FirstOrDefault();
        }

        [RelayCommand]
        private void SelectAllMultiRows()
        {
            foreach (var r in MultiAddRows)
            {
                if (MultiRowFilter(r)) r.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllMultiRows()
        {
            foreach (var r in MultiAddRows)
            {
                r.IsSelected = false;
            }
        }

        [RelayCommand]
        private void AddMultiViewsToSheets()
        {
            var rowsToProcess = MultiAddRows.Where(r => r.IsSelected
                && (r.ArchView != null || r.UnderView != null || r.OverView != null)).ToList();

            if (!rowsToProcess.Any())
            {
                StatusMessage = "No matched rows selected.";
                return;
            }

            _handler.RequestAction = "ADD_MULTI_VIEWS";
            _handler.Raise();
        }

        // ================= Tab 6: Open View Title =================

        private bool TitleRowFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(TitleSearchText)) return true;
            if (obj is ViewportTitleRow row)
            {
                return row.ViewName.IndexOf(TitleSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.SheetNumber != null && row.SheetNumber.IndexOf(TitleSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (row.CurrentTitle != null && row.CurrentTitle.IndexOf(TitleSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        public void LoadViewportTitleData(Document doc)
        {
            ViewportTitleRows.Clear();
            ViewportTitleTypes.Clear();

            // Build a map: ViewId -> Viewport
            var allViewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            // Load viewport types from an existing viewport's valid types
            if (allViewports.Any())
            {
                var validTypeIds = allViewports.First().GetValidTypes();
                foreach (var typeId in validTypeIds)
                {
                    var vpType = doc.GetElement(typeId) as ElementType;
                    if (vpType != null)
                    {
                        ViewportTitleTypes.Add(new ViewportTitleItem { Name = vpType.Name, Id = vpType.Id });
                    }
                }
            }

            var viewIdToViewport = new Dictionary<ElementId, Viewport>();
            foreach (var vp in allViewports)
            {
                viewIdToViewport[vp.ViewId] = vp;
            }

            // Load all non-template views
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.DrawingSheet)
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                bool isOnSheet = viewIdToViewport.ContainsKey(view.Id);
                string currentTitle = "—";
                string sheetNumber = "";
                ElementId viewportId = ElementId.InvalidElementId;

                if (isOnSheet)
                {
                    var vp = viewIdToViewport[view.Id];
                    viewportId = vp.Id;
                    var vpType = doc.GetElement(vp.GetTypeId()) as ElementType;
                    currentTitle = vpType?.Name ?? "Unknown";

                    // Find sheet number
                    var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                    sheetNumber = sheet?.SheetNumber ?? "";
                }

                ViewportTitleRows.Add(new ViewportTitleRow
                {
                    IsSelected = false,
                    ViewName = view.Name,
                    ViewType = view.ViewType.ToString(),
                    CurrentTitle = currentTitle,
                    SheetNumber = sheetNumber,
                    IsOnSheet = isOnSheet,
                    ViewportId = viewportId,
                    ViewId = view.Id
                });
            }

            // Setup filter + grouping
            _filteredTitleRows = CollectionViewSource.GetDefaultView(ViewportTitleRows);
            _filteredTitleRows.Filter = TitleRowFilter;
            _filteredTitleRows.GroupDescriptions.Clear();
            _filteredTitleRows.GroupDescriptions.Add(new PropertyGroupDescription("ViewType"));
            OnPropertyChanged(nameof(FilteredTitleRows));
        }

        [RelayCommand]
        private void SelectAllTitleRows()
        {
            foreach (var r in ViewportTitleRows)
            {
                if (TitleRowFilter(r) && r.IsOnSheet) r.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllTitleRows()
        {
            foreach (var r in ViewportTitleRows)
            {
                r.IsSelected = false;
            }
        }

        [RelayCommand]
        private void ApplyViewportTitle()
        {
            if (SelectedViewportTitle == null)
            {
                StatusMessage = "Please select a Title Type.";
                return;
            }

            var selected = ViewportTitleRows.Where(r => r.IsSelected && r.IsOnSheet && r.ViewportId != ElementId.InvalidElementId).ToList();
            if (!selected.Any())
            {
                StatusMessage = "No valid rows selected.";
                return;
            }

            _handler.RequestAction = "APPLY_VIEWPORT_TITLE";
            _handler.Raise();
        }

        // ================= Tab 7: Scope Box =================

        private bool ScopeBoxRowFilter(object obj)
        {
            if (obj is ScopeBoxViewRow row)
            {
                if (SelectedScopeBoxViewType != "All" && !string.IsNullOrEmpty(SelectedScopeBoxViewType) && row.ViewType != SelectedScopeBoxViewType)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(ScopeBoxSearchText))
                {
                    return row.ViewName.IndexOf(ScopeBoxSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                        || row.ViewType.IndexOf(ScopeBoxSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                        || (row.CurrentScopeBox != null && row.CurrentScopeBox.IndexOf(ScopeBoxSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                
                return true;
            }
            return false;
        }

        public void LoadScopeBoxData(Document doc)
        {
            ScopeBoxViewRows.Clear();
            ScopeBoxOptions.Clear();

            // Load Scope Boxes
            var scopeBoxes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                .WhereElementIsNotElementType()
                .ToList();

            ScopeBoxOptions.Add(new ScopeBoxItem { Name = "<None>", Id = ElementId.InvalidElementId });
            foreach (var sb in scopeBoxes.OrderBy(x => x.Name))
            {
                ScopeBoxOptions.Add(new ScopeBoxItem { Name = sb.Name, Id = sb.Id });
            }
            SelectedScopeBox = ScopeBoxOptions.FirstOrDefault();

            // Load plan views
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate
                    && (v.ViewType == ViewType.FloorPlan
                     || v.ViewType == ViewType.CeilingPlan
                     || v.ViewType == ViewType.EngineeringPlan
                     || v.ViewType == ViewType.AreaPlan
                     || v.ViewType == ViewType.Section
                     || v.ViewType == ViewType.Elevation))
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                string currentScopeBox = "<None>";
                var sbParam = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                if (sbParam != null && sbParam.AsElementId() != ElementId.InvalidElementId)
                {
                    var sbElem = doc.GetElement(sbParam.AsElementId());
                    if (sbElem != null)
                        currentScopeBox = sbElem.Name;
                }

                ScopeBoxViewRows.Add(new ScopeBoxViewRow
                {
                    ViewId = view.Id,
                    ViewName = view.Name,
                    ViewType = view.ViewType.ToString(),
                    CurrentScopeBox = currentScopeBox
                });
            }

            // Populate distinct ViewTypes
            ScopeBoxViewTypes.Clear();
            ScopeBoxViewTypes.Add("All");
            var distinctTypes = views.Select(v => v.ViewType.ToString()).Distinct().OrderBy(x => x).ToList();
            foreach (var type in distinctTypes)
            {
                ScopeBoxViewTypes.Add(type);
            }
            SelectedScopeBoxViewType = "All";

            _filteredScopeBoxRows = CollectionViewSource.GetDefaultView(ScopeBoxViewRows);
            _filteredScopeBoxRows.Filter = ScopeBoxRowFilter;
            OnPropertyChanged(nameof(FilteredScopeBoxRows));
        }

        [RelayCommand]
        private void SelectAllScopeBoxRows()
        {
            foreach (var r in ScopeBoxViewRows)
            {
                if (ScopeBoxRowFilter(r)) r.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAllScopeBoxRows()
        {
            foreach (var r in ScopeBoxViewRows)
            {
                r.IsSelected = false;
            }
        }

        [RelayCommand]
        private void ApplyScopeBox()
        {
            if (SelectedScopeBox == null)
            {
                StatusMessage = "Please select a Scope Box.";
                return;
            }

            var selected = ScopeBoxViewRows.Where(r => r.IsSelected).ToList();
            if (!selected.Any())
            {
                StatusMessage = "No views selected.";
                return;
            }

            _handler.RequestAction = "APPLY_SCOPE_BOX";
            _handler.Raise();
        }
    }
}
