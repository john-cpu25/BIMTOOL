using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingSchedule.Models;

namespace RincoNhan.Tools.LoadingSchedule.ViewModels
{
    /// <summary>
    /// ViewModel for the Loading Schedule legend configuration UI.
    /// Loads FilledRegionTypes from the ACTIVE VIEW and prepares items for legend generation.
    /// </summary>
    public class LoadingScheduleViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly View _activeView;

        public ObservableCollection<LoadingScheduleItem> Items { get; set; }
        public ObservableCollection<ViewWrapper> LegendViews { get; set; }
        public ObservableCollection<TextTypeWrapper> TextTypes { get; set; }

        private ViewWrapper _selectedLegendView;
        public ViewWrapper SelectedLegendView
        {
            get => _selectedLegendView;
            set { _selectedLegendView = value; OnPropertyChanged(); }
        }

        private TextTypeWrapper _selectedTextType;
        public TextTypeWrapper SelectedTextType
        {
            get => _selectedTextType;
            set { _selectedTextType = value; OnPropertyChanged(); }
        }

        public string FamilyStatusMessage { get; private set; }
        public string SourceViewName => _activeView?.Name ?? "(unknown)";

        public LoadingScheduleViewModel(Document doc, View activeView)
        {
            _doc = doc;
            _activeView = activeView;
            Items = new ObservableCollection<LoadingScheduleItem>();
            LegendViews = new ObservableCollection<ViewWrapper>();
            TextTypes = new ObservableCollection<TextTypeWrapper>();

            LoadFilledRegionsFromView();
            LoadLegendViews();
            LoadTextNoteTypes();
            CheckFamilyStatus();
        }

        /// <summary>
        /// Loads FilledRegion types from the ACTIVE VIEW (not all types in the doc).
        /// Groups by FilledRegionType and creates one LoadingScheduleItem per unique type.
        /// </summary>
        private void LoadFilledRegionsFromView()
        {
            // Get all filled regions from the current view
            var filledRegions = new FilteredElementCollector(_doc, _activeView.Id)
                .OfClass(typeof(FilledRegion))
                .Cast<FilledRegion>()
                .ToList();

            // Group by FilledRegionType Id
            var grouped = filledRegions
                .GroupBy(fr => fr.GetTypeId())
                .OrderBy(g =>
                {
                    var t = _doc.GetElement(g.Key) as FilledRegionType;
                    return t?.Name ?? "";
                })
                .ToList();

            int number = 1;
            foreach (var group in grouped)
            {
                var typeId = group.Key;
                var regionType = _doc.GetElement(typeId) as FilledRegionType;
                if (regionType == null) continue;

                string typeName = regionType.Name;

                // Color info for UI display
                string colorStr = "";
                var color = regionType.ForegroundPatternColor;
                if (color != null && color.IsValid)
                    colorStr = $"{color.Red}, {color.Green}, {color.Blue}";

                // Pattern name for UI display
                string patternName = "";
                var patternId = regionType.ForegroundPatternId;
                if (patternId != ElementId.InvalidElementId)
                {
                    var patternElem = _doc.GetElement(patternId) as FillPatternElement;
                    if (patternElem != null) patternName = patternElem.Name;
                }

                Items.Add(new LoadingScheduleItem
                {
                    Number = number++,
                    FilledRegionTypeId = typeId,
                    TypeName = typeName,
                    LoadType = typeName, // Default to type name; tag will override
                    ColorDisplay = colorStr,
                    PatternName = patternName,
                    IsSelected = true
                });
            }
        }

        /// <summary>
        /// Loads all Legend views for the target dropdown.
        /// </summary>
        private void LoadLegendViews()
        {
            var legendViews = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var lv in legendViews)
                LegendViews.Add(new ViewWrapper(lv));

            if (LegendViews.Any())
                SelectedLegendView = LegendViews.First();
        }

        /// <summary>
        /// Loads available TextNoteTypes for the header text style dropdown.
        /// </summary>
        private void LoadTextNoteTypes()
        {
            var textTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var tt in textTypes)
                TextTypes.Add(new TextTypeWrapper(tt));

            if (TextTypes.Any())
            {
                // Prefer RINCO or Arial type
                var preferred = TextTypes.FirstOrDefault(t =>
                    t.Name.Contains("RINCO") || t.Name.Contains("Arial"));
                SelectedTextType = preferred ?? TextTypes.First();
            }
        }

        /// <summary>
        /// Checks if the required RINCO tag families are loaded.
        /// </summary>
        private void CheckFamilyStatus()
        {
            var allSymbols = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            bool hasLoadingTag = allSymbols.Any(fs =>
                fs.FamilyName?.Equals("RINCO_LH_Loading Tag", StringComparison.OrdinalIgnoreCase) == true);

            var infoTagTypes = allSymbols
                .Where(fs => fs.FamilyName?.Equals("RINCO_LH_Info Loading Tag", StringComparison.OrdinalIgnoreCase) == true)
                .Select(fs => fs.Name)
                .ToList();

            var messages = new List<string>();
            messages.Add(hasLoadingTag
                ? "✓ RINCO_LH_Loading Tag"
                : "✗ RINCO_LH_Loading Tag (not found)");

            if (infoTagTypes.Any())
                messages.Add($"✓ RINCO_LH_Info Loading Tag ({string.Join(", ", infoTagTypes)})");
            else
                messages.Add("✗ RINCO_LH_Info Loading Tag (not found)");

            // Check for title text type
            bool hasTitleType = new FilteredElementCollector(_doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .Any(t => t.Name.Contains("RINCO_3.0_Arial N/T_ARW"));

            messages.Add(hasTitleType
                ? "✓ RINCO_3.0_Arial N/T_ARW (TextNoteType)"
                : "✗ RINCO_3.0_Arial N/T_ARW (TextNoteType not found)");

            messages.Add($"📋 Source view: {SourceViewName} ({Items.Count} hatch types found)");

            FamilyStatusMessage = string.Join("\n", messages);
            OnPropertyChanged(nameof(FamilyStatusMessage));
        }

        /// <summary>
        /// Gets the selected items with renumbered sequence.
        /// </summary>
        public List<LoadingScheduleItem> GetSelectedItems()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            for (int i = 0; i < selected.Count; i++)
                selected[i].Number = i + 1;
            return selected;
        }

        public void MoveUp(int index)
        {
            if (index <= 0 || index >= Items.Count) return;
            Items.Move(index, index - 1);
            RenumberItems();
        }

        public void MoveDown(int index)
        {
            if (index < 0 || index >= Items.Count - 1) return;
            Items.Move(index, index + 1);
            RenumberItems();
        }

        private void RenumberItems()
        {
            for (int i = 0; i < Items.Count; i++)
                Items[i].Number = i + 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ViewWrapper
    {
        public View View { get; }
        public string Name => View.Name;
        public ElementId Id => View.Id;
        public ViewWrapper(View view) { View = view; }
        public override string ToString() => Name;
    }

    public class TextTypeWrapper
    {
        public TextNoteType Type { get; }
        public string Name => Type.Name;
        public ElementId Id => Type.Id;
        public TextTypeWrapper(TextNoteType type) { Type = type; }
        public override string ToString() => Name;
    }
}
