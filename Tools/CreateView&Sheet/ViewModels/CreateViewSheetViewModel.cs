using System;
using System.Collections.ObjectModel;
using System.Linq;
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

            // Load Views (Plan Views)
            LoadViewsAndSheets(doc);
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
                ProjectSheets.Add(new SheetItem { SheetNumber = s.SheetNumber, SheetName = s.Name, Id = s.Id });
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

        private void AddGroupedSheetRow(LevelItem level, string suffix, string sheetNumber, string groupId)
        {
            var row = new CreateSheetRow
            {
                SelectedLevel = level,
                Suffix = suffix,
                SheetNumber = sheetNumber,
                GroupId = groupId
            };

            row.OnLevelChangedCallback = (changedRow, newLevel) =>
            {
                if (_isSyncingLevel) return;
                _isSyncingLevel = true;
                foreach (var r in CreateSheetRows.Where(x => x.GroupId == changedRow.GroupId && x != changedRow))
                {
                    r.SelectedLevel = newLevel;
                }
                _isSyncingLevel = false;
            };

            CreateSheetRows.Add(row);
        }

        [RelayCommand]
        private void AddCreateSheetRow()
        {
            string groupId = Guid.NewGuid().ToString();
            var level = ProjectLevels.FirstOrDefault();

            AddGroupedSheetRow(level, "- OVER", "", groupId);
            AddGroupedSheetRow(level, "- UNDER", "", groupId);
            AddGroupedSheetRow(level, "- ARCH", "", groupId);
        }

        [RelayCommand]
        private void AddAllLevelsToCreateSheet()
        {
            CreateSheetRows.Clear();
            int sheetIndex = 1;
            foreach (var level in ProjectLevels)
            {
                string groupId = Guid.NewGuid().ToString();
                
                AddGroupedSheetRow(level, "- OVER", $"A{sheetIndex:D3}-OVER", groupId);
                AddGroupedSheetRow(level, "- UNDER", $"A{sheetIndex:D3}-UNDER", groupId);
                AddGroupedSheetRow(level, "- ARCH", $"A{sheetIndex:D3}-ARCH", groupId);

                sheetIndex++;
            }
            StatusMessage = $"Added {CreateSheetRows.Count} rows ({ProjectLevels.Count} levels × 3 sheets).";
        }

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
