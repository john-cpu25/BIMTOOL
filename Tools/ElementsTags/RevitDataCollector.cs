using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.ElementsTags
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

        public List<Category> GetTaggableCategories()
        {
            // Get all instances in the active view
            var elements = new FilteredElementCollector(_doc, _activeView.Id)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                .ToList();

            var categories = elements
                .Select(e => e.Category)
                .GroupBy(c => c.Id.GetIdValue())
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            return categories;
        }

        public List<FamilySymbol> GetTagTypes(Category category)
        {
            if (category == null) return new List<FamilySymbol>();

            string categoryName = category.Name;
            // Remove 's' at the end for plural-to-singular matching (e.g. Walls -> Wall Tags)
            string singularName = categoryName.EndsWith("s") ? categoryName.Substring(0, categoryName.Length - 1) : categoryName;
            
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null && fs.Category.IsTagCategory)
                .Where(fs => fs.Category.Name.Contains(singularName) || fs.Name.Contains(singularName))
                .OrderBy(fs => fs.Name)
                .ToList();

            return collector;
        }

        public List<Element> GetUntaggedElements(Category category)
        {
            if (category == null) return new List<Element>();

            // Get all model elements of this category in view
            var elements = new FilteredElementCollector(_doc, _activeView.Id)
                .OfCategoryId(category.Id)
                .WhereElementIsNotElementType()
                .ToElements();

            // Get all tags in view
            var tags = new FilteredElementCollector(_doc, _activeView.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            // Track IDs of tagged elements
            var taggedElementIds = new HashSet<ElementId>();
            foreach (var tag in tags)
            {
#if REVIT2022_OR_GREATER
                foreach (var id in tag.GetTaggedLocalElementIds())
#else
                foreach (var id in new List<ElementId> { tag.TaggedLocalElementId })
#endif
                {
                    taggedElementIds.Add(id);
                }
            }

            return elements.Where(e => !taggedElementIds.Contains(e.Id)).ToList();
        }
    }
}
