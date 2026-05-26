using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Filter.ViewModels;

namespace RincoNhan.Tools.Filter
{
    public class FilterExternalEventHandler : IExternalEventHandler
    {
        public string RequestAction { get; set; }
        public MainViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            if (ViewModel == null || app.ActiveUIDocument == null) return;

            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (doc == null || view == null) return;

            try
            {
                var matchedIds = GetMatchedElementIds(doc, view);

                using (Transaction trans = new Transaction(doc, "Rinco Filter - " + RequestAction))
                {
                    trans.Start();

                    switch (RequestAction)
                    {
                        case "APPLY":
                            if (matchedIds != null)
                                uidoc.Selection.SetElementIds(matchedIds);
                            ViewModel.SetStatus($"Selected {matchedIds?.Count ?? 0} elements.", true);
                            break;

                        case "EH":
                            var hideableIds = matchedIds.Where(id => {
                                var e = doc.GetElement(id);
                                return e != null && e.CanBeHidden(view);
                            }).ToList();
                            if (hideableIds.Any()) view.HideElements(hideableIds);
                            ViewModel.SetStatus($"Hidden {hideableIds.Count} elements in view.", true);
                            break;

                        case "HH":
                            // user requested Hide Category
                            var categoryIds = matchedIds
                                .Select(id => doc.GetElement(id)?.Category)
                                .Where(c => c != null)
                                .Select(c => c.Id)
                                .Distinct()
                                .Where(catId => view.CanCategoryBeHidden(catId))
                                .ToList();
                            foreach (var catId in categoryIds)
                            {
                                view.SetCategoryHidden(catId, true);
                            }
                            ViewModel.SetStatus(string.Format("Hidden {0} categories in view.", categoryIds.Count), true);
                            break;

                        case "HI":
                            // user requested Reset HI
                            if (view.IsTemporaryHideIsolateActive())
                            {
                                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                            }
                            ViewModel.SetStatus("View reset successfully.", true);
                            break;

                        case "OVERRIDE":
                            ApplyOverrides(view, matchedIds);
                            ViewModel.SetStatus($"Applied overrides to {matchedIds.Count} elements.", true);
                            break;
                    }

                    trans.Commit();
                }
            }
            catch (Exception ex)
            {
                ViewModel.SetStatus("Error in ExternalEvent: " + ex.Message, false);
            }
        }

        public string GetName() => "RincoFilterActionHandler";

        private List<ElementId> GetMatchedElementIds(Document doc, View view)
        {
            var matchedIds = new List<ElementId>();

            using (FilteredElementCollector collector = ViewModel.IsInProjectView
                ? new FilteredElementCollector(doc)
                : new FilteredElementCollector(doc, view.Id))
            {
                var elements = collector.WhereElementIsNotElementType();
                
                // 1. Sidebar Category Filter
                var selectedSidebarCategories = ViewModel.CategoryCounts
                    .Where(c => c.IsSelected)
                    .Select(c => c.Name)
                    .ToHashSet();

                // 2. Builder Rules Filter
                var activeGroups = ViewModel.Groups.Where(g => g.Rules.Any()).ToList();
                bool hasValidRules = activeGroups.Any(g => g.Rules.Any(r => r.SelectedCategory != null));

                foreach (Element elem in elements)
                {
                    // Category filter from sidebar
                    if (selectedSidebarCategories.Any())
                    {
                        if (elem.Category == null || !selectedSidebarCategories.Contains(elem.Category.Name)) continue;
                    }

                    // Rule filter
                    if (hasValidRules)
                    {
                        bool totalMatch = false;
                        foreach (var group in activeGroups)
                        {
                            bool groupMatches = group.IsAndLogic;
                            foreach (var rule in group.Rules)
                            {
                                bool ruleMatches = CheckRuleMatch(elem, rule, doc);
                                
                                if (group.IsAndLogic) { if (!ruleMatches) { groupMatches = false; break; } }
                                else { if (ruleMatches) { groupMatches = true; break; } }
                            }
                            if (groupMatches) { totalMatch = true; break; }
                        }
                        if (totalMatch) matchedIds.Add(elem.Id);
                    }
                    else if (selectedSidebarCategories.Any())
                    {
                        // Only sidebar filter active
                        matchedIds.Add(elem.Id);
                    }
                }
            }
            return matchedIds;
        }

        private bool CheckRuleMatch(Element elem, FilterRuleViewModel rule, Document doc)
        {
            if (rule.SelectedCategory == null) return true;
            if (elem.Category == null || elem.Category.Name != rule.SelectedCategory.Name) return false;

            if (!string.IsNullOrEmpty(rule.SelectedFamily))
            {
                string combinedFamName = GetCombinedFamilyName(elem, doc);
                if (combinedFamName != rule.SelectedFamily) return false;
            }

            if (rule.SelectedParameter != null)
            {
                var param = Enumerable.FirstOrDefault(Enumerable.Cast<Parameter>(elem.Parameters), 
                    p => p.Definition.Name == rule.SelectedParameter.Definition.Name);
                
                if (param == null || !param.HasValue) return false;

                string val = GetParamValueString(param);
                string targetVal = rule.Value ?? "";

                switch (rule.SelectedOperator)
                {
                    case "= equals": return string.Equals(val, targetVal, StringComparison.OrdinalIgnoreCase);
                    case "does not equal": return !string.Equals(val, targetVal, StringComparison.OrdinalIgnoreCase);
                    case "contains": return val.IndexOf(targetVal, StringComparison.OrdinalIgnoreCase) >= 0;
                    case "> greater than":
                        if (double.TryParse(val, out double vd1) && double.TryParse(targetVal, out double td1)) return vd1 > td1;
                        break;
                    case "< less than":
                        if (double.TryParse(val, out double vd2) && double.TryParse(targetVal, out double td2)) return vd2 < td2;
                        break;
                }
                return false;
            }
            return true;
        }

        private string GetCombinedFamilyName(Element elem, Document doc)
        {
            var typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var elemType = doc.GetElement(typeId) as ElementType;
                if (elemType != null)
                {
                    string fName = elemType.FamilyName ?? "";
                    string tName = elemType.Name ?? "";
                    return !string.IsNullOrEmpty(fName) && !string.IsNullOrEmpty(tName) ? $"{fName} - {tName}" : fName + tName;
                }
            }
            return "";
        }

        private string GetParamValueString(Parameter param)
        {
            string val = param.AsValueString();
            if (string.IsNullOrEmpty(val)) val = param.AsString();
            if (string.IsNullOrEmpty(val)) val = param.AsInteger().ToString();
            if (string.IsNullOrEmpty(val)) val = "<Empty/No Value>";
            return val;
        }

        private void ApplyOverrides(View view, List<ElementId> ids)
        {
            Random rnd = new Random();
            var patternCollector = new FilteredElementCollector(view.Document).OfClass(typeof(FillPatternElement));
            var solidPattern = patternCollector.Cast<FillPatternElement>().FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

            foreach (var id in ids)
            {
                Element elem = view.Document.GetElement(id);
                if (elem == null || elem.Category == null) continue;

                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                Color randomColor = new Color((byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256));

                // 3D elements (Model)
                if (elem.Category.CategoryType == CategoryType.Model)
                {
                    ogs.SetSurfaceForegroundPatternColor(randomColor);
                    ogs.SetSurfaceBackgroundPatternColor(randomColor);
                    ogs.SetCutForegroundPatternColor(randomColor);
                    ogs.SetCutBackgroundPatternColor(randomColor);
                    
                    if (solidPattern != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                        ogs.SetSurfaceBackgroundPatternId(solidPattern.Id);
                        ogs.SetCutForegroundPatternId(solidPattern.Id);
                    }
                }
                // 2D elements (Annotation/Detail)
                else
                {
                    ogs.SetProjectionLineColor(randomColor);
                    // Also try pattern if it's a Filled Region or similar
                    ogs.SetSurfaceForegroundPatternColor(randomColor);
                    if (solidPattern != null)
                    {
                        ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                    }
                }

                view.SetElementOverrides(id, ogs);
            }
        }
    }
}
