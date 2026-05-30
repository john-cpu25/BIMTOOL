using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.QueryElement
{
    public class ElementItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }

        public override string ToString() => Name;
    }

    public class LocationItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public bool IsClickable => Id != null && Id != ElementId.InvalidElementId;
    }

    public static class QueryLogic
    {
        public static List<ElementItem> GetAllViews(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType != ViewType.Legend && v.ViewType != ViewType.Schedule && v.ViewType != ViewType.ProjectBrowser)
                .Select(v => new ElementItem { Id = v.Id, Name = v.Name })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<ElementItem> GetAllLegends(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewType == ViewType.Legend)
                .Select(v => new ElementItem { Id = v.Id, Name = v.Name })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<ElementItem> GetAllGroups(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(g => g.OwnerViewId != ElementId.InvalidElementId) // Detail groups belong to a view
                .Select(g => new ElementItem { Id = g.Id, Name = g.Name })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<LocationItem> GetViewLocation(Document doc, ElementId viewId)
        {
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Where(v => v.ViewId == viewId)
                .ToList();

            if (viewports.Count == 0) return new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Not placed on any sheet" } };

            var sheet = doc.GetElement(viewports.First().SheetId) as ViewSheet;
            if (sheet != null)
                return new List<LocationItem> { new LocationItem { Id = sheet.Id, Name = $"{sheet.SheetNumber} - {sheet.Name}" } };
            
            return new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Unknown Sheet" } };
        }

        public static List<LocationItem> GetLegendLocations(Document doc, ElementId legendId)
        {
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Where(v => v.ViewId == legendId)
                .ToList();

            if (viewports.Count == 0) return new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Not placed on any sheet" } };

            return viewports.Select(vp =>
            {
                var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                return sheet != null ? new LocationItem { Id = sheet.Id, Name = $"{sheet.SheetNumber} - {sheet.Name}" } : new LocationItem { Id = ElementId.InvalidElementId, Name = "Unknown Sheet" };
            }).ToList();
        }

        public static List<LocationItem> GetGroupLocation(Document doc, ElementId groupId)
        {
            var group = doc.GetElement(groupId) as Group;
            if (group == null) return new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Group not found" } };

            if (group.OwnerViewId != ElementId.InvalidElementId)
            {
                var view = doc.GetElement(group.OwnerViewId) as View;
                return view != null ? new List<LocationItem> { new LocationItem { Id = view.Id, Name = $"View: {view.Name}" } } : new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Unknown View" } };
            }

            return new List<LocationItem> { new LocationItem { Id = ElementId.InvalidElementId, Name = "Model Group" } };
        }
    }
}
