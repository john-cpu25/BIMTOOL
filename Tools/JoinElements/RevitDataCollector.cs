using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.JoinElements
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

        public IEnumerable<Category> GetModelCategories(bool inActiveViewOnly)
        {
            var categories = new Dictionary<ElementId, Category>();
            if (inActiveViewOnly && _activeView == null) return categories.Values;

            using (FilteredElementCollector collector = inActiveViewOnly 
                ? new FilteredElementCollector(_doc, _activeView.Id) 
                : new FilteredElementCollector(_doc))
            {
                foreach (Element elem in collector.WhereElementIsNotElementType())
                {
                    if (elem.Category != null && elem.Category.CategoryType == CategoryType.Model && !categories.ContainsKey(elem.Category.Id))
                    {
                        categories[elem.Category.Id] = elem.Category;
                    }
                }
            }

            return categories.Values.OrderBy(c => c.Name);
        }
    }
}
