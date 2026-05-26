using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.Filter
{
    public class RevitDataCollector
    {
        private Document _doc;
        private View _activeView;

        public RevitDataCollector(Document doc, View activeView)
        {
            _doc = doc;
            _activeView = activeView;
        }

        public int GetTotalElementCount(bool inActiveViewOnly)
        {
            if (inActiveViewOnly && _activeView == null) return 0;

            using (FilteredElementCollector collector = inActiveViewOnly
                ? new FilteredElementCollector(_doc, _activeView.Id)
                : new FilteredElementCollector(_doc))
            {
                return collector.WhereElementIsNotElementType().GetElementCount();
            }
        }

        public Dictionary<string, int> GetCategoryElementCounts(bool inActiveViewOnly)
        {
            var counts = new Dictionary<string, int>();
            if (inActiveViewOnly && _activeView == null) return counts;

            using (FilteredElementCollector collector = inActiveViewOnly
                ? new FilteredElementCollector(_doc, _activeView.Id)
                : new FilteredElementCollector(_doc))
            {
                // Iterate directly over the collector to avoid materializing all elements at once
                foreach (Element elem in collector.WhereElementIsNotElementType())
                {
                    if (elem.Category != null)
                    {
                        string name = elem.Category.Name;
                        if (!counts.ContainsKey(name))
                            counts[name] = 0;
                        counts[name]++;
                    }
                }
            }
            return counts;
        }

        public IEnumerable<Category> GetCategories(bool inActiveViewOnly)
        {
            var categories = new Dictionary<ElementId, Category>();
            if (inActiveViewOnly && _activeView == null) return categories.Values;

            using (FilteredElementCollector collector = inActiveViewOnly 
                ? new FilteredElementCollector(_doc, _activeView.Id) 
                : new FilteredElementCollector(_doc))
            {
                foreach (Element elem in collector.WhereElementIsNotElementType())
                {
                    if (elem.Category != null && !categories.ContainsKey(elem.Category.Id))
                    {
                        categories[elem.Category.Id] = elem.Category;
                    }
                }
            }

            return categories.Values.OrderBy(c => c.Name);
        }

        public IEnumerable<string> GetFamiliesForCategory(Category category, bool inActiveViewOnly)
        {
            var families = new HashSet<string>();
            if (category == null) return families;
            if (inActiveViewOnly && _activeView == null) return families;

            using (FilteredElementCollector collector = inActiveViewOnly
                ? new FilteredElementCollector(_doc, _activeView.Id)
                : new FilteredElementCollector(_doc))
            {
                foreach (Element elem in collector.OfCategoryId(category.Id).WhereElementIsNotElementType())
                {
                    var typeId = elem.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        var elemType = _doc.GetElement(typeId) as ElementType;
                        if (elemType != null)
                        {
                            string famName = elemType.FamilyName ?? "";
                            string typeName = elemType.Name ?? "";
                            string combined = !string.IsNullOrEmpty(famName) && !string.IsNullOrEmpty(typeName)
                                ? $"{famName} - {typeName}"
                                : famName + typeName;

                            if (!string.IsNullOrWhiteSpace(combined))
                            {
                                families.Add(combined);
                            }
                        }
                    }
                }
            }
            return families.OrderBy(f => f);
        }

        public IEnumerable<Parameter> GetParametersForCategory(Category category, bool inActiveViewOnly)
        {
            if (category == null) return new List<Parameter>();
            if (inActiveViewOnly && _activeView == null) return new List<Parameter>();

            using (FilteredElementCollector collector = inActiveViewOnly 
                ? new FilteredElementCollector(_doc, _activeView.Id) 
                : new FilteredElementCollector(_doc))
            {
                // Just get the first element to extract parameter list
                var element = collector.OfCategoryId(category.Id).WhereElementIsNotElementType().FirstOrDefault();

                if (element != null)
                {
                    var parameters = new List<Parameter>();
                    foreach (Parameter param in element.Parameters)
                    {
                        if (param.Definition != null)
                            parameters.Add(param);
                    }
                    return parameters.OrderBy(p => p.Definition.Name);
                }
            }
            return new List<Parameter>();
        }

        public Dictionary<string, List<Element>> GetParameterValues(Category category, Parameter parameter, bool inActiveViewOnly)
        {
            var valueDict = new Dictionary<string, List<Element>>();
            if (category == null || parameter == null) return valueDict;
            if (inActiveViewOnly && _activeView == null) return valueDict;

            string paramName = parameter.Definition.Name;

            using (FilteredElementCollector collector = inActiveViewOnly 
                ? new FilteredElementCollector(_doc, _activeView.Id) 
                : new FilteredElementCollector(_doc))
            {
                foreach(Element elem in collector.OfCategoryId(category.Id).WhereElementIsNotElementType())
                {
                    var elParam = elem.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Definition.Name == paramName);
                    if (elParam != null && elParam.HasValue)
                    {
                        string val = elParam.AsValueString();
                        if (string.IsNullOrEmpty(val)) val = elParam.AsString();
                        if (string.IsNullOrEmpty(val)) val = elParam.AsInteger().ToString(); 
                        if (string.IsNullOrEmpty(val)) val = "<Empty/No Value>";

                        if (!valueDict.ContainsKey(val))
                        {
                            valueDict[val] = new List<Element>();
                        }
                        valueDict[val].Add(elem);
                    }
                }
            }
            return valueDict;
        }
    }
}
