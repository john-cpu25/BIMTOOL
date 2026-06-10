using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingSchedule.Models;
using RincoNhan.Tools.LoadingSchedule.Services;

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
        public ObservableCollection<ViewWrapper> TemplateLegendViews { get; set; }
        public ObservableCollection<ViewWrapper> TargetLegendViews { get; set; }

        public DuplicateLegendViewModel DuplicateLegendViewModel { get; set; }
        public UpdateLegendViewModel UpdateLegendViewModel { get; set; }

        private ViewWrapper _selectedTemplateLegend;
        public ViewWrapper SelectedTemplateLegend
        {
            get => _selectedTemplateLegend;
            set
            {
                _selectedTemplateLegend = value;
                OnPropertyChanged();
                // Parse template when selection changes
                ParseSelectedTemplate();
            }
        }

        private ViewWrapper _selectedTargetLegend;
        public ViewWrapper SelectedTargetLegend
        {
            get => _selectedTargetLegend;
            set { _selectedTargetLegend = value; OnPropertyChanged(); }
        }

        private string _templateStatusMessage = "";
        public string TemplateStatusMessage
        {
            get => _templateStatusMessage;
            set { _templateStatusMessage = value; OnPropertyChanged(); }
        }

        public string SourceViewName => _activeView?.Name ?? "(unknown)";

        public LoadingScheduleViewModel(Document doc, View activeView)
        {
            _doc = doc;
            _activeView = activeView;
            Items = new ObservableCollection<LoadingScheduleItem>();
            TemplateLegendViews = new ObservableCollection<ViewWrapper>();
            TargetLegendViews = new ObservableCollection<ViewWrapper>();
            
            DuplicateLegendViewModel = new DuplicateLegendViewModel(doc);
            UpdateLegendViewModel = new UpdateLegendViewModel(doc);
        }

        public void InitializeData()
        {
            // LoadFilledRegionsFromView(); // Disabled because the first tab is hidden and it causes severe lag on load.
            LoadLegendViews();
            
            DuplicateLegendViewModel.LoadData();
            UpdateLegendViewModel.LoadData();
        }

        /// <summary>
        /// Loads FilledRegion types from the ACTIVE VIEW.
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
                    if (t != null)
                    {
                        var typeMarkParam = t.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        string typeMarkStr = typeMarkParam?.AsString() ?? "";
                        if (int.TryParse(typeMarkStr, out int num))
                            return num.ToString("D5"); // Pad with zeros for correct numeric string sort (e.g. 00001, 00012)
                        return typeMarkStr;
                    }
                    return "";
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
                    ColorDisplay = colorStr,
                    PatternName = patternName,
                    IsSelected = true
                });
            }

            TemplateStatusMessage = $"📋 Source view: {SourceViewName} ({Items.Count} hatch types found)";
        }

        /// <summary>
        /// Loads all Legend views for both template and target dropdowns.
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
            {
                TemplateLegendViews.Add(new ViewWrapper(lv));
                TargetLegendViews.Add(new ViewWrapper(lv));
            }

            if (TemplateLegendViews.Any())
                SelectedTemplateLegend = TemplateLegendViews.First();

            if (TargetLegendViews.Any())
                SelectedTargetLegend = TargetLegendViews.First();
        }

        /// <summary>
        /// Parses the selected template legend to show its structure in the status area.
        /// </summary>
        private void ParseSelectedTemplate()
        {
            if (_selectedTemplateLegend == null)
            {
                TemplateStatusMessage = $"📋 Source view: {SourceViewName} ({Items.Count} hatch types found)";
                return;
            }

            var parser = new LegendTemplateParser();
            bool ok = parser.Parse(_doc, _selectedTemplateLegend.View);

            var lines = new List<string>();
            lines.Add($"📋 Source view: {SourceViewName} ({Items.Count} hatch types found)");

            if (ok)
                lines.Add($"✓ Template: {parser.Summary}");
            else
                lines.Add($"✗ Template: {parser.Summary}");

            TemplateStatusMessage = string.Join("\n", lines);
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
}
