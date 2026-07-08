using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.ComponentModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.AutoViewSheet.Models;

namespace RincoNhan.Tools.AutoViewSheet.ViewModels
{
    public partial class AutoViewSheetViewModel : ObservableObject
    {
        private readonly AutoViewSheetHandler _handler;
        
        // Settings Options
        public ObservableCollection<TitleBlockItem> TitleBlocks { get; set; } = new ObservableCollection<TitleBlockItem>();
        [ObservableProperty] private TitleBlockItem _selectedTitleBlock;

        public ObservableCollection<string> SheetSeriesOptions { get; set; } = new ObservableCollection<string>();
        [ObservableProperty] private string _selectedSheetSeries;

        public ObservableCollection<string> SheetNamingTypes { get; set; } = new ObservableCollection<string> { "GA PLAN", "LP PLAN", "NORMAL" };
        [ObservableProperty] private string _selectedSheetNamingType = "GA PLAN";

        partial void OnSelectedSheetSeriesChanged(string value)
        {
            UpdateAllRows();
        }

        partial void OnSelectedSheetNamingTypeChanged(string value)
        {
            if (AutoRows.Any() || PartRows.Any())
            {
                var response = MessageBox.Show("Do you want to regenerate all rows to apply the new Sheet Naming Type?", 
                    "Regenerate Rows", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (response == MessageBoxResult.Yes)
                {
                    AddAllLevels();
                    AddAllLevelsForPart();
                }
            }
        }

        public ObservableCollection<ViewportTitleItem> ViewportTitleTypes { get; set; } = new ObservableCollection<ViewportTitleItem>();
        [ObservableProperty] private ViewportTitleItem _selectedViewportTitle;

        [ObservableProperty] private bool _stackViews = true;

        // Data Row Options
        public List<LevelItem> ProjectLevels { get; set; } = new List<LevelItem>();
        public ObservableCollection<ViewTypeItem> ViewTypes { get; set; } = new ObservableCollection<ViewTypeItem>();
        public ObservableCollection<ViewTemplateItem> ViewTemplates { get; set; } = new ObservableCollection<ViewTemplateItem>();
        public ObservableCollection<ScopeBoxItem> ScopeBoxOptions { get; set; } = new ObservableCollection<ScopeBoxItem>();
        [ObservableProperty] private ScopeBoxItem _selectedGlobalScopeBox;

        public ObservableCollection<AutoViewSheetRow> AutoRows { get; set; } = new ObservableCollection<AutoViewSheetRow>();
        public ICollectionView AutoRowsView { get; private set; }

        public ObservableCollection<AutoViewSheetRow> PartRows { get; set; } = new ObservableCollection<AutoViewSheetRow>();
        public ICollectionView PartRowsView { get; private set; }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    AutoRowsView.Refresh();
                }
            }
        }

        private string _partSearchText;
        public string PartSearchText
        {
            get => _partSearchText;
            set
            {
                if (SetProperty(ref _partSearchText, value))
                {
                    PartRowsView.Refresh();
                }
            }
        }

        [ObservableProperty] private string _statusMessage = "Ready.";

        public AutoViewSheetViewModel(AutoViewSheetHandler handler, Document doc)
        {
            _handler = handler;
            _handler.ViewModel = this;

            AutoRowsView = CollectionViewSource.GetDefaultView(AutoRows);
            AutoRowsView.GroupDescriptions.Add(new PropertyGroupDescription("SheetNumberAndName"));
            AutoRowsView.Filter = FilterRows;
            AutoRowsView.SortDescriptions.Add(new SortDescription("SheetNumber", ListSortDirection.Ascending));

            PartRowsView = CollectionViewSource.GetDefaultView(PartRows);
            PartRowsView.Filter = FilterPartRows;
            PartRowsView.GroupDescriptions.Add(new PropertyGroupDescription("SelectedLevel.Name"));
            PartRowsView.GroupDescriptions.Add(new PropertyGroupDescription("GroupId"));
            PartRowsView.SortDescriptions.Add(new SortDescription("SelectedLevel.Elevation", ListSortDirection.Ascending));
            PartRowsView.SortDescriptions.Add(new SortDescription("SheetNumber", ListSortDirection.Ascending));

            LoadData(doc);
        }

        private bool FilterRows(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is AutoViewSheetRow row)
            {
                return (row.SheetName != null && row.SheetName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                       (row.SheetNumber != null && row.SheetNumber.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                       (row.SelectedLevel != null && row.SelectedLevel.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        private bool FilterPartRows(object obj)
        {
            if (string.IsNullOrWhiteSpace(PartSearchText)) return true;
            if (obj is AutoViewSheetRow row)
            {
                return (row.SheetName != null && row.SheetName.IndexOf(PartSearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                       (row.SheetNumber != null && row.SheetNumber.IndexOf(PartSearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                       (row.SelectedLevel != null && row.SelectedLevel.Name.IndexOf(PartSearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                       (row.ZoneName != null && row.ZoneName.IndexOf(PartSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            return false;
        }

        private bool _sortAscending = true;
        [RelayCommand]
        private void ToggleSort()
        {
            _sortAscending = !_sortAscending;
            AutoRowsView.SortDescriptions.Clear();
            AutoRowsView.SortDescriptions.Add(new SortDescription("SheetNumber", _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
        }

        private bool _partSortAscending = true;
        [RelayCommand]
        private void TogglePartSort()
        {
            _partSortAscending = !_partSortAscending;
            PartRowsView.SortDescriptions.Clear();
            PartRowsView.SortDescriptions.Add(new SortDescription("SelectedLevel.Elevation", _partSortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
            PartRowsView.SortDescriptions.Add(new SortDescription("SheetNumber", ListSortDirection.Ascending));
        }

        [RelayCommand]
        private void SelectAll(string action)
        {
            bool check = action == "True";
            foreach (var row in AutoRows)
            {
                row.IsGroupSelected = check;
            }
        }

        [RelayCommand]
        private void SelectAllPart(string action)
        {
            bool check = action == "True";
            // Group the part rows by their Level + GroupId since checking the first row automatically checks the rest via callbacks
            var grouped = PartRows.GroupBy(r => new { LevelId = r.SelectedLevel?.Id, r.GroupId });
            foreach (var group in grouped)
            {
                var firstRow = group.First();
                firstRow.IsGroupSelected = check;
            }
        }

        private void LoadData(Document doc)
        {
            // Levels
            ProjectLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new LevelItem { Name = l.Name, Id = l.Id, Elevation = l.Elevation })
                .ToList();

            // View Types
            var viewTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .Where(vt => vt.ViewFamily == ViewFamily.StructuralPlan)
                .OrderBy(vt => vt.Name)
                .ToList();
            foreach (var vt in viewTypes) ViewTypes.Add(new ViewTypeItem { Name = vt.Name, Id = vt.Id });

            // Templates
            ViewTemplates.Add(new ViewTemplateItem { Name = "<None>", Id = ElementId.InvalidElementId });
            var templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();
            foreach (var vt in templates) ViewTemplates.Add(new ViewTemplateItem { Name = vt.Name, Id = vt.Id });

            // Scope Boxes
            ScopeBoxOptions.Add(new ScopeBoxItem { Name = "<None>", Id = ElementId.InvalidElementId });
            var scopeBoxes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                .WhereElementIsNotElementType()
                .OrderBy(sb => sb.Name)
                .ToList();
            foreach (var sb in scopeBoxes) ScopeBoxOptions.Add(new ScopeBoxItem { Name = sb.Name, Id = sb.Id });
            SelectedGlobalScopeBox = ScopeBoxOptions.FirstOrDefault();

            // TitleBlocks
            var titleBlocks = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .OrderBy(tb => tb.Name)
                .ToList();
            foreach (var tb in titleBlocks) TitleBlocks.Add(new TitleBlockItem { Name = $"{tb.FamilyName} - {tb.Name}", Id = tb.Id });
            SelectedTitleBlock = TitleBlocks.FirstOrDefault();

            // Sheet Series
            LoadSheetSeries(doc);

            // Viewport Titles
            ViewportTitleTypes.Add(new ViewportTitleItem { Name = "<None>", Id = ElementId.InvalidElementId });
            var allViewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().ToList();
            if (allViewports.Any())
            {
                var validTypeIds = allViewports.First().GetValidTypes();
                foreach (var typeId in validTypeIds)
                {
                    var vpType = doc.GetElement(typeId) as ElementType;
                    if (vpType != null) ViewportTitleTypes.Add(new ViewportTitleItem { Name = vpType.Name, Id = vpType.Id });
                }
            }
            SelectedViewportTitle = ViewportTitleTypes.FirstOrDefault(vt => vt.Name.Equals("RINCO_Title_GA", StringComparison.OrdinalIgnoreCase)) ?? ViewportTitleTypes.FirstOrDefault();

            // Auto-populate all levels
            AddAllLevels();
            AddAllLevelsForPart();
        }

        private void LoadSheetSeries(Document doc)
        {
            SheetSeriesOptions.Clear();
            var seriesValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (!seriesValues.Any())
            {
                seriesValues.Add("S0000 SERIES - GENERAL");
                seriesValues.Add("S1000 SERIES - SITE RETENTION & FOUNDATION PLANS");
                seriesValues.Add("S2000 SERIES - GENERAL ARRANGEMENT PLANS");
                seriesValues.Add("S3000 SERIES - WALL ELEVATIONS");
                seriesValues.Add("S4000 SERIES - CONCRETE DETAILS");
                seriesValues.Add("S5000 SERIES - PEO & PT PLANS");
            }

            foreach (var s in seriesValues.OrderBy(x => x)) SheetSeriesOptions.Add(s);

            SelectedSheetSeries = SheetSeriesOptions.FirstOrDefault(s => s.Contains("GENERAL ARRANGEMENT")) ?? SheetSeriesOptions.FirstOrDefault();
        }

        private void AddGroupedViewRow(LevelItem level, string suffix, string viewTypeKey, string templateKey, string groupId, string baseSheetNum, int offset)
        {
            // Fuzzy match view type based on suffix
            var matchedViewType = ViewTypes.FirstOrDefault(vt => vt.Name.Equals(viewTypeKey, StringComparison.OrdinalIgnoreCase));
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
            if (matchedViewType == null) matchedViewType = ViewTypes.FirstOrDefault();
            
            // Fuzzy match template
            var matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(templateKey, StringComparison.OrdinalIgnoreCase));
            if (matchedTemplate == null)
            {
                if (suffix.Contains("OVER")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("OVER 1/100"));
                else if (suffix.Contains("UNDER")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("UNDER 1/100"));
                else if (suffix.Contains("ARCH")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("ARCH 1/100"));
            }
            if (matchedTemplate == null) matchedTemplate = ViewTemplates.FirstOrDefault();

            string nameSuffix = "GENERAL ARRANGEMENT PLAN";
            if (SelectedSheetNamingType == "LP PLAN") nameSuffix = "LOADING PLAN";
            else if (SelectedSheetNamingType == "NORMAL") nameSuffix = "PLAN";

            var row = new AutoViewSheetRow
            {
                SelectedLevel = level,
                Suffix = suffix,
                SelectedViewType = matchedViewType,
                SelectedViewTemplate = matchedTemplate,
                SelectedScopeBox = ScopeBoxOptions.FirstOrDefault(),
                SheetNumber = ComputeSheetNumber(baseSheetNum, offset),
                SheetName = $"{level.Name} {nameSuffix}",
                GroupId = groupId
            };

            row.GroupSelectionChangedCallback = (changedRow, isSelected) =>
            {
                foreach (var r in AutoRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.IsSelected = isSelected;
                }
            };

            row.LevelChangedCallback = (changedRow, oldLevel, newLevel) =>
            {
                foreach (var r in AutoRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.SelectedLevel = newLevel;
                }
            };

            row.SheetNumberChangedCallback = (changedRow, newNum) =>
            {
                foreach (var r in AutoRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.SheetNumber = newNum;
                }
            };

            row.SheetNameChangedCallback = (changedRow, newName) =>
            {
                foreach (var r in AutoRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.SheetName = newName;
                }
            };

            row.ScopeBoxChangedCallback = (changedRow, newBox) =>
            {
                foreach (var r in AutoRows.Where(x => x != changedRow))
                {
                    r.SelectedScopeBox = newBox;
                }
            };

            AutoRows.Add(row);
        }

        private string ComputeSheetNumber(string baseNum, int offset)
        {
            if (string.IsNullOrEmpty(baseNum)) return "";
            if (offset == 0) return baseNum;

            var match = Regex.Match(baseNum, @"^([A-Za-z]*)(\d+)(.*)$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                int num = int.Parse(match.Groups[2].Value);
                string suffix = match.Groups[3].Value;
                return $"{prefix}{num + offset}{suffix}";
            }
            return $"{baseNum}.{offset}";
        }

        private void UpdateAllRows()
        {
            if (AutoRows == null || !AutoRows.Any()) return;
            
            var levelGroups = AutoRows.GroupBy(r => r.GroupId).ToList();
            HashSet<string> usedBaseNums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string nameSuffix = "GENERAL ARRANGEMENT PLAN";
            string archTemplateName = "RINCO - GA - ARCH 1/100";
            string overTemplateName = "RINCO - GA - OVER 1/100";
            string underTemplateName = "RINCO - GA - UNDER 1/100";
            string suffixAddition = "";

            if (SelectedSheetNamingType == "LP PLAN") 
            {
                nameSuffix = "LOADING PLAN";
                archTemplateName = "RINCO - LP - ARCH 1/100";
                overTemplateName = "RINCO - LP - OVER 1/100";
                suffixAddition = " - LOADING PLAN";
            }
            else if (SelectedSheetNamingType == "NORMAL") nameSuffix = "PLAN";

            foreach (var group in levelGroups)
            {
                var firstRow = group.First();
                var level = firstRow.SelectedLevel;
                if (level == null) continue;
                
                string baseSheetNum = GenerateBaseSheetNumber(level, ProjectLevels);
                
                string uniqueBaseNum = baseSheetNum;
                int suffixCount = 1;
                while (usedBaseNums.Contains(uniqueBaseNum))
                {
                    uniqueBaseNum = $"{baseSheetNum}.{suffixCount}";
                    suffixCount++;
                }
                usedBaseNums.Add(uniqueBaseNum);

                firstRow.SheetNumber = uniqueBaseNum; // updates the whole group via callback
                firstRow.SheetName = $"{level.Name} {nameSuffix}"; // updates the whole group via callback

                var underRow = group.FirstOrDefault(r => r.Suffix != null && r.Suffix.IndexOf("UNDER", StringComparison.OrdinalIgnoreCase) >= 0);
                
                if (SelectedSheetNamingType == "LP PLAN")
                {
                    if (underRow != null)
                    {
                        AutoRows.Remove(underRow);
                    }
                }
                else
                {
                    if (underRow == null)
                    {
                        underRow = new AutoViewSheetRow
                        {
                            SelectedLevel = level,
                            Suffix = "- UNDER",
                            SelectedViewType = ViewTypes.FirstOrDefault(vt => vt.Name.EndsWith("UNDER", StringComparison.OrdinalIgnoreCase) || vt.Name.IndexOf("UNDER", StringComparison.OrdinalIgnoreCase) >= 0) ?? ViewTypes.FirstOrDefault(),
                            SelectedViewTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(underTemplateName, StringComparison.OrdinalIgnoreCase)) ?? ViewTemplates.FirstOrDefault(),
                            SelectedScopeBox = ScopeBoxOptions.FirstOrDefault(),
                            SheetNumber = uniqueBaseNum,
                            SheetName = $"{level.Name} {nameSuffix}",
                            GroupId = firstRow.GroupId,
                            IsSelected = firstRow.IsGroupSelected
                        };
                        
                        underRow.GroupSelectionChangedCallback = firstRow.GroupSelectionChangedCallback;
                        underRow.LevelChangedCallback = firstRow.LevelChangedCallback;
                        underRow.SheetNumberChangedCallback = firstRow.SheetNumberChangedCallback;
                        underRow.SheetNameChangedCallback = firstRow.SheetNameChangedCallback;
                        underRow.ScopeBoxChangedCallback = firstRow.ScopeBoxChangedCallback;

                        AutoRows.Add(underRow);
                    }
                }

                foreach (var row in AutoRows.Where(r => r.GroupId == firstRow.GroupId))
                {
                    string cleanSuffix = row.Suffix?.Replace(" - LOADING PLAN", "");
                    
                    if (cleanSuffix != null && cleanSuffix.IndexOf("ARCH", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        row.Suffix = cleanSuffix + suffixAddition;
                        var tmpl = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(archTemplateName, StringComparison.OrdinalIgnoreCase));
                        if (tmpl != null) row.SelectedViewTemplate = tmpl;
                    }
                    else if (cleanSuffix != null && cleanSuffix.IndexOf("OVER", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        row.Suffix = cleanSuffix + suffixAddition;
                        var tmpl = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(overTemplateName, StringComparison.OrdinalIgnoreCase));
                        if (tmpl != null) row.SelectedViewTemplate = tmpl;
                    }
                    else if (cleanSuffix != null && cleanSuffix.IndexOf("UNDER", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        row.Suffix = cleanSuffix;
                        var tmpl = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(underTemplateName, StringComparison.OrdinalIgnoreCase));
                        if (tmpl != null) row.SelectedViewTemplate = tmpl;
                    }
                }
            }
        }

        private string GenerateBaseSheetNumber(LevelItem level, List<LevelItem> allLevels)
        {
            string name = level.Name.ToUpper().Trim();
            string prefix = "S4"; // fallback
            
            if (!string.IsNullOrEmpty(SelectedSheetSeries))
            {
                var seriesMatch = Regex.Match(SelectedSheetSeries, @"^([A-Za-z0-9]{2})");
                if (seriesMatch.Success) prefix = seriesMatch.Groups[1].Value;
            }

            // BASEMENT levels → prefix + 099 (multiple basements count down)
            if (name.Contains("BASEMENT"))
            {
                var basements = allLevels.Where(l => l.Name.ToUpper().Contains("BASEMENT")).OrderBy(l => l.Elevation).ToList();
                int basementCount = basements.Count;
                int index = basements.IndexOf(level);
                return $"{prefix}{99 - (basementCount - 1 - index):D3}";
            }

            // GROUND level → prefix + 100
            if (name.Contains("GROUND")) return $"{prefix}100";

            // LEVEL X → prefix + (100 + X)
            var match = Regex.Match(name, @"LEVEL\s*(\d+)");
            if (match.Success)
            {
                int levelNum = int.Parse(match.Groups[1].Value);
                return $"{prefix}{100 + levelNum}";
            }

            // Other levels
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
                .Where(l => !l.Name.ToUpper().Contains("BASEMENT") && !l.Name.ToUpper().Contains("GROUND") && !Regex.IsMatch(l.Name.ToUpper(), @"LEVEL\s*\d+"))
                .OrderBy(l => l.Elevation).ToList();
            int otherIndex = otherLevels.IndexOf(level);

            return $"{prefix}{100 + maxLevelNum + 1 + otherIndex}";
        }

        [RelayCommand]
        private void AddRow()
        {
            if (!ProjectLevels.Any()) return;
            var level = ProjectLevels.First();
            string groupId = Guid.NewGuid().ToString();
            string baseSheet = GenerateBaseSheetNumber(level, ProjectLevels);

            string archTemplateName = "RINCO - GA - ARCH 1/100";
            string overTemplateName = "RINCO - GA - OVER 1/100";
            string underTemplateName = "RINCO - GA - UNDER 1/100";
            string suffixAddition = "";

            if (SelectedSheetNamingType == "LP PLAN") 
            {
                archTemplateName = "RINCO - LP - ARCH 1/100";
                overTemplateName = "RINCO - LP - OVER 1/100";
                suffixAddition = " - LOADING PLAN";
            }

            AddGroupedViewRow(level, "- ARCH" + suffixAddition, "ARCH OVERLAY_PLANS", archTemplateName, groupId, baseSheet, 0);
            
            if (SelectedSheetNamingType != "LP PLAN")
            {
                AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", underTemplateName, groupId, baseSheet, 0);
            }
            
            AddGroupedViewRow(level, "- OVER" + suffixAddition, "OVER_PLANS", overTemplateName, groupId, baseSheet, 0);
        }

        [RelayCommand]
        private void AddAllLevels()
        {
            AutoRows.Clear();
            var levels = ProjectLevels;
            HashSet<string> usedBaseNums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string archTemplateName = "RINCO - GA - ARCH 1/100";
            string overTemplateName = "RINCO - GA - OVER 1/100";
            string underTemplateName = "RINCO - GA - UNDER 1/100";
            string suffixAddition = "";

            if (SelectedSheetNamingType == "LP PLAN") 
            {
                archTemplateName = "RINCO - LP - ARCH 1/100";
                overTemplateName = "RINCO - LP - OVER 1/100";
                suffixAddition = " - LOADING PLAN";
            }

            foreach (var level in levels)
            {
                string baseSheetNum = GenerateBaseSheetNumber(level, levels);
                
                string uniqueBaseNum = baseSheetNum;
                int suffixCount = 1;
                while (usedBaseNums.Contains(uniqueBaseNum))
                {
                    uniqueBaseNum = $"{baseSheetNum}.{suffixCount}";
                    suffixCount++;
                }
                usedBaseNums.Add(uniqueBaseNum);

                string groupId = level.Id.ToString();

                AddGroupedViewRow(level, "- ARCH" + suffixAddition, "ARCH OVERLAY_PLANS", archTemplateName, groupId, uniqueBaseNum, 0);
                
                if (SelectedSheetNamingType != "LP PLAN")
                {
                    AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", underTemplateName, groupId, uniqueBaseNum, 0);
                }
                
                AddGroupedViewRow(level, "- OVER" + suffixAddition, "OVER_PLANS", overTemplateName, groupId, uniqueBaseNum, 0);
            }
        }

        private void AddAllLevelsForPart()
        {
            PartRows.Clear();
            var levels = ProjectLevels;
            HashSet<string> usedBaseNums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string archTemplateName = "RINCO - GA - ARCH 1/100";
            string overTemplateName = "RINCO - GA - OVER 1/100";
            string underTemplateName = "RINCO - GA - UNDER 1/100";
            string suffixAddition = "";
            string nameSuffix = "GENERAL ARRANGEMENT PLAN";

            if (SelectedSheetNamingType == "LP PLAN") 
            {
                archTemplateName = "RINCO - LP - ARCH 1/100";
                overTemplateName = "RINCO - LP - OVER 1/100";
                suffixAddition = " - LOADING PLAN";
                nameSuffix = "LOADING PLAN";
            }
            else if (SelectedSheetNamingType == "NORMAL")
            {
                nameSuffix = "PLAN";
            }

            foreach (var level in levels)
            {
                string baseSheetNum = GenerateBaseSheetNumber(level, levels);
                
                string uniqueBaseNum = baseSheetNum;
                int suffixCount = 1;
                while (usedBaseNums.Contains(uniqueBaseNum))
                {
                    uniqueBaseNum = $"{baseSheetNum}.{suffixCount}";
                    suffixCount++;
                }
                usedBaseNums.Add(uniqueBaseNum);

                string groupId = Guid.NewGuid().ToString();
                string defaultZoneName = "ZONE 1";

                AddGroupedPartRow(level, "- ARCH" + suffixAddition, "ARCH OVERLAY_PLANS", archTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
                
                if (SelectedSheetNamingType != "LP PLAN")
                {
                    AddGroupedPartRow(level, "- UNDER", "UNDER_PLANS", underTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
                }
                
                AddGroupedPartRow(level, "- OVER" + suffixAddition, "OVER_PLANS", overTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
            }
        }

        [RelayCommand]
        private void AddPartRowLevel()
        {
            var usedLevelIds = PartRows.Where(x => x.SelectedLevel != null).Select(x => x.SelectedLevel.Id).ToHashSet();
            var level = ProjectLevels.FirstOrDefault(l => !usedLevelIds.Contains(l.Id)) ?? ProjectLevels.FirstOrDefault();
            
            if (level != null)
            {
                string archTemplateName = "RINCO - GA - ARCH 1/100";
                string overTemplateName = "RINCO - GA - OVER 1/100";
                string underTemplateName = "RINCO - GA - UNDER 1/100";
                string suffixAddition = "";
                string nameSuffix = "GENERAL ARRANGEMENT PLAN";

                if (SelectedSheetNamingType == "LP PLAN") 
                {
                    archTemplateName = "RINCO - LP - ARCH 1/100";
                    overTemplateName = "RINCO - LP - OVER 1/100";
                    suffixAddition = " - LOADING PLAN";
                    nameSuffix = "LOADING PLAN";
                }
                else if (SelectedSheetNamingType == "NORMAL")
                {
                    nameSuffix = "PLAN";
                }

                string baseSheetNum = GenerateBaseSheetNumber(level, ProjectLevels);
                
                string uniqueBaseNum = baseSheetNum;
                int suffixCount = 1;
                while (PartRows.Any(r => r.SheetNumber == uniqueBaseNum))
                {
                    uniqueBaseNum = $"{baseSheetNum}.{suffixCount}";
                    suffixCount++;
                }

                string groupId = Guid.NewGuid().ToString();
                string defaultZoneName = "ZONE 1";

                AddGroupedPartRow(level, "- ARCH" + suffixAddition, "ARCH OVERLAY_PLANS", archTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
                
                if (SelectedSheetNamingType != "LP PLAN")
                {
                    AddGroupedPartRow(level, "- UNDER", "UNDER_PLANS", underTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
                }
                
                AddGroupedPartRow(level, "- OVER" + suffixAddition, "OVER_PLANS", overTemplateName, groupId, uniqueBaseNum, defaultZoneName, nameSuffix);
            }
        }

        private void RemoveRow(AutoViewSheetRow row)
        {
            if (row != null && AutoRows.Contains(row))
            {
                AutoRows.Remove(row);
            }
        }

        [RelayCommand]
        private void RunAutoProcess()
        {
            StatusMessage = "Processing...";
            _handler.Raise();
        }

        [RelayCommand]
        private void AddZoneForLevel(AutoViewSheetRow rowGroup)
        {
            if (rowGroup == null) return;
            var level = rowGroup.SelectedLevel;
            if (level == null) return;

            string groupId = Guid.NewGuid().ToString();

            int maxZone = 0;
            string lastSheetNumber = null;
            foreach (var r in PartRows.Where(x => x.SelectedLevel?.Id == level.Id))
            {
                var match = Regex.Match(r.ZoneName ?? "", @"ZONE (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int z))
                {
                    if (z > maxZone)
                    {
                        maxZone = z;
                        lastSheetNumber = r.SheetNumber;
                    }
                }
            }
            string defaultZoneName = $"ZONE {maxZone + 1}";

            string baseSheetNum = lastSheetNumber;
            if (string.IsNullOrEmpty(baseSheetNum))
            {
                baseSheetNum = GenerateBaseSheetNumber(level, ProjectLevels);
            }
            else
            {
                // Increment trailing number
                var numMatch = Regex.Match(baseSheetNum, @"(\d+)$");
                if (numMatch.Success)
                {
                    string numStr = numMatch.Groups[1].Value;
                    if (long.TryParse(numStr, out long num))
                    {
                        string newNumStr = (num + 1).ToString().PadLeft(numStr.Length, '0');
                        baseSheetNum = baseSheetNum.Substring(0, numMatch.Index) + newNumStr;
                    }
                    else baseSheetNum += ".1";
                }
                else baseSheetNum += ".1";
            }

            string uniqueBaseNum = baseSheetNum;
            int suffixCount = 1;
            while (PartRows.Any(r => r.SheetNumber == uniqueBaseNum))
            {
                uniqueBaseNum = $"{baseSheetNum}.{suffixCount}";
                suffixCount++;
            }
            baseSheetNum = uniqueBaseNum;
            
            string archTemplateName = "RINCO - GA - ARCH 1/100";
            string overTemplateName = "RINCO - GA - OVER 1/100";
            string underTemplateName = "RINCO - GA - UNDER 1/100";
            string suffixAddition = "";
            string nameSuffix = "GENERAL ARRANGEMENT PLAN";

            if (SelectedSheetNamingType == "LP PLAN") 
            {
                archTemplateName = "RINCO - LP - ARCH 1/100";
                overTemplateName = "RINCO - LP - OVER 1/100";
                suffixAddition = " - LOADING PLAN";
                nameSuffix = "LOADING PLAN";
            }
            else if (SelectedSheetNamingType == "NORMAL")
            {
                nameSuffix = "PLAN";
            }

            AddGroupedPartRow(level, "- ARCH" + suffixAddition, "ARCH OVERLAY_PLANS", archTemplateName, groupId, baseSheetNum, defaultZoneName, nameSuffix);
            
            if (SelectedSheetNamingType != "LP PLAN")
            {
                AddGroupedPartRow(level, "- UNDER", "UNDER_PLANS", underTemplateName, groupId, baseSheetNum, defaultZoneName, nameSuffix);
            }
            
            AddGroupedPartRow(level, "- OVER" + suffixAddition, "OVER_PLANS", overTemplateName, groupId, baseSheetNum, defaultZoneName, nameSuffix);
        }

        private void AddGroupedPartRow(LevelItem level, string suffix, string viewTypeKey, string templateKey, string groupId, string sheetNum, string zoneName, string sheetNameStr)
        {
            // Fuzzy match view type based on suffix
            var matchedViewType = ViewTypes.FirstOrDefault(vt => vt.Name.Equals(viewTypeKey, StringComparison.OrdinalIgnoreCase));
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
            if (matchedViewType == null) matchedViewType = ViewTypes.FirstOrDefault();
            
            // Fuzzy match template
            var matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Equals(templateKey, StringComparison.OrdinalIgnoreCase));
            if (matchedTemplate == null)
            {
                if (suffix.Contains("OVER")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("OVER 1/100"));
                else if (suffix.Contains("UNDER")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("UNDER 1/100"));
                else if (suffix.Contains("ARCH")) matchedTemplate = ViewTemplates.FirstOrDefault(vt => vt.Name.Contains("ARCH 1/100"));
            }
            if (matchedTemplate == null) matchedTemplate = ViewTemplates.FirstOrDefault();
            
            var row = new AutoViewSheetRow
            {
                SelectedLevel = level,
                Suffix = suffix,
                SelectedViewType = matchedViewType,
                SelectedViewTemplate = matchedTemplate,
                SelectedScopeBox = ScopeBoxOptions.FirstOrDefault(),
                SheetNumber = sheetNum,
                SheetName = sheetNameStr,
                GroupId = groupId,
                IsGroupSelected = true,
                ZoneName = zoneName
            };

            row.GroupSelectionChangedCallback = (r, isSelected) =>
            {
                foreach (var groupRow in PartRows.Where(gr => gr.GroupId == r.GroupId && gr != r))
                    groupRow.IsSelected = isSelected;
            };

            row.LevelChangedCallback = (r, oldLvl, newLvl) =>
            {
                if (oldLvl == null || newLvl == null) return;
                var rowsToChange = PartRows.Where(gr => gr.SelectedLevel?.Id == oldLvl.Id && gr != r).ToList();
                foreach (var groupRow in rowsToChange)
                {
                    groupRow.SelectedLevel = newLvl;
                }
            };

            row.SheetNumberChangedCallback = (r, val) =>
            {
                foreach (var groupRow in PartRows.Where(gr => gr.GroupId == r.GroupId && gr != r))
                    groupRow.SheetNumber = val;
            };

            row.SheetNameChangedCallback = (r, val) =>
            {
                foreach (var groupRow in PartRows.Where(gr => gr.GroupId == r.GroupId && gr != r))
                    groupRow.SheetName = val;
            };

            row.ZoneNameChangedCallback = (r, val) =>
            {
                foreach (var groupRow in PartRows.Where(gr => gr.GroupId == r.GroupId && gr != r))
                    groupRow.ZoneName = val;
            };

            row.ScopeBoxChangedCallback = (r, val) =>
            {
                foreach (var groupRow in PartRows.Where(gr => gr.GroupId == r.GroupId && gr != r))
                    groupRow.SelectedScopeBox = val;
            };

            row.LevelSelectionChangedCallback = (r, val) =>
            {
                if (r.SelectedLevel == null) return;
                var levelId = r.SelectedLevel.Id;
                foreach (var groupRow in PartRows.Where(gr => gr.SelectedLevel?.Id == levelId))
                {
                    if (groupRow != r) groupRow.IsLevelSelected = val;
                }
            };

            PartRows.Add(row);
        }

        [RelayCommand]
        private void RemovePartRowForLevel(AutoViewSheetRow rowGroup)
        {
            if (rowGroup == null || rowGroup.SelectedLevel == null) return;
            var levelId = rowGroup.SelectedLevel.Id;
            var rowsToRemove = PartRows.Where(r => r.SelectedLevel?.Id == levelId && (r.IsGroupSelected || r.IsSelected)).ToList();
            foreach (var r in rowsToRemove)
            {
                PartRows.Remove(r);
            }
        }

        [RelayCommand]
        private void RunAutoProcessPart()
        {
            StatusMessage = "Processing Parts...";
            _handler.IsPartMode = true;
            _handler.Raise();
        }
    }
}
