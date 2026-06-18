using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
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

        public ObservableCollection<ViewportTitleItem> ViewportTitleTypes { get; set; } = new ObservableCollection<ViewportTitleItem>();
        [ObservableProperty] private ViewportTitleItem _selectedViewportTitle;

        public ObservableCollection<ViewItem> AlignTemplates { get; set; } = new ObservableCollection<ViewItem>();
        [ObservableProperty] private ViewItem _selectedAlignTemplate;

        [ObservableProperty] private bool _stackViews;

        // Data Row Options
        public List<LevelItem> ProjectLevels { get; set; } = new List<LevelItem>();
        public ObservableCollection<ViewTypeItem> ViewTypes { get; set; } = new ObservableCollection<ViewTypeItem>();
        public ObservableCollection<ViewTemplateItem> ViewTemplates { get; set; } = new ObservableCollection<ViewTemplateItem>();
        public ObservableCollection<ScopeBoxItem> ScopeBoxOptions { get; set; } = new ObservableCollection<ScopeBoxItem>();

        // Main Grid Rows
        public ObservableCollection<AutoViewSheetRow> AutoRows { get; set; } = new ObservableCollection<AutoViewSheetRow>();

        [ObservableProperty] private string _statusMessage = "Ready.";

        public AutoViewSheetViewModel(AutoViewSheetHandler handler, Document doc)
        {
            _handler = handler;
            _handler.ViewModel = this;

            LoadData(doc);
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
                .Where(vt => vt.ViewFamily == ViewFamily.FloorPlan
                          || vt.ViewFamily == ViewFamily.CeilingPlan
                          || vt.ViewFamily == ViewFamily.StructuralPlan)
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
            SelectedViewportTitle = ViewportTitleTypes.FirstOrDefault();

            // Align Templates (Views on sheets)
            AlignTemplates.Add(new ViewItem { Name = "<None>", Id = ElementId.InvalidElementId });
            var viewsOnSheets = allViewports.Select(vp => vp.ViewId).Distinct().ToList();
            var validViews = new FilteredElementCollector(doc, viewsOnSheets)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType != ViewType.Schedule && v.ViewType != ViewType.Legend)
                .OrderBy(v => v.Name)
                .ToList();
            foreach (var v in validViews) AlignTemplates.Add(new ViewItem { Name = v.Name, Id = v.Id });
            SelectedAlignTemplate = AlignTemplates.FirstOrDefault();

            // Auto-populate all levels
            AddAllLevels();
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

            var row = new AutoViewSheetRow
            {
                SelectedLevel = level,
                Suffix = suffix,
                SelectedViewType = matchedViewType,
                SelectedViewTemplate = matchedTemplate,
                SelectedScopeBox = ScopeBoxOptions.FirstOrDefault(),
                SheetNumber = ComputeSheetNumber(baseSheetNum, offset),
                SheetName = $"{level.Name} - GENERAL ARRANGEMENT",
                GroupId = groupId
            };

            row.LevelChangedCallback = (changedRow, newLevel) =>
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
            if (baseNum.StartsWith("S"))
            {
                if (int.TryParse(baseNum.Substring(1), out int num))
                {
                    return $"S{num + offset}";
                }
            }
            return $"{baseNum}.{offset}";
        }

        private string GenerateBaseSheetNumber(LevelItem level, List<LevelItem> allLevels)
        {
            string name = level.Name.ToUpper().Trim();

            // BASEMENT levels → S4099 (multiple basements count down)
            if (name.Contains("BASEMENT"))
            {
                var basements = allLevels.Where(l => l.Name.ToUpper().Contains("BASEMENT")).OrderBy(l => l.Elevation).ToList();
                int basementCount = basements.Count;
                int index = basements.IndexOf(level);
                return $"S{4099 - (basementCount - 1 - index)}";
            }

            // GROUND level → S4100
            if (name.Contains("GROUND")) return "S4100";

            // LEVEL X → S4100 + X
            var match = Regex.Match(name, @"LEVEL\s*(\d+)");
            if (match.Success)
            {
                int levelNum = int.Parse(match.Groups[1].Value);
                return $"S{4100 + levelNum}";
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

            return $"S{4100 + maxLevelNum + 1 + otherIndex}";
        }

        [RelayCommand]
        private void AddRow()
        {
            if (!ProjectLevels.Any()) return;
            var level = ProjectLevels.First();
            string groupId = Guid.NewGuid().ToString();
            string baseSheet = GenerateBaseSheetNumber(level, ProjectLevels);

            AddGroupedViewRow(level, "- ARCH", "ARCH OVERLAY_PLANS", "RINCO - GA - ARCH 1/100", groupId, baseSheet, 0);
            AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", "RINCO - GA - UNDER 1/100", groupId, baseSheet, 0);
            AddGroupedViewRow(level, "- OVER", "OVER_PLANS", "RINCO - GA - OVER 1/100", groupId, baseSheet, 0);
        }

        [RelayCommand]
        private void AddAllLevels()
        {
            AutoRows.Clear();
            foreach (var level in ProjectLevels)
            {
                string groupId = Guid.NewGuid().ToString();
                string baseSheet = GenerateBaseSheetNumber(level, ProjectLevels);

                AddGroupedViewRow(level, "- ARCH", "ARCH OVERLAY_PLANS", "RINCO - GA - ARCH 1/100", groupId, baseSheet, 0);
                AddGroupedViewRow(level, "- UNDER", "UNDER_PLANS", "RINCO - GA - UNDER 1/100", groupId, baseSheet, 0);
                AddGroupedViewRow(level, "- OVER", "OVER_PLANS", "RINCO - GA - OVER 1/100", groupId, baseSheet, 0);
            }
        }

        [RelayCommand]
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
    }
}
