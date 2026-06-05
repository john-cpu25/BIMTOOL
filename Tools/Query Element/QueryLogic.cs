using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.QueryElement
{
    public class ElementItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public bool IsSelected { get; set; }
        public string VisibilityStatus { get; set; } = "";
        public Brush VisibilityColor { get; set; } = Brushes.Gray;

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

        // ═══════════════════════════════════════════════════════════════════
        //  CAD LINK / IMPORT
        // ═══════════════════════════════════════════════════════════════════

        public static List<ElementItem> GetViewTemplates(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => new ElementItem { Id = v.Id, Name = v.Name })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static void UpdateCadVisibilityFromTemplate(Document doc, ElementId templateId, List<ElementItem> cadItems)
        {
            if (templateId == null || templateId == ElementId.InvalidElementId)
            {
                // Clear visibility status
                foreach (var item in cadItems)
                {
                    item.VisibilityStatus = "";
                    item.VisibilityColor = Brushes.Gray;
                }
                return;
            }

            var template = doc.GetElement(templateId) as View;
            if (template == null) return;

            foreach (var item in cadItems)
            {
                try
                {
                    bool isHidden = template.GetCategoryHidden(item.Id);
                    item.VisibilityStatus = isHidden ? "✘" : "✔";
                    item.VisibilityColor = isHidden ? Brushes.Red : Brushes.Green;
                }
                catch
                {
                    item.VisibilityStatus = "—";
                    item.VisibilityColor = Brushes.Gray;
                }
            }
        }

        public static List<ElementItem> GetAllCadLinks(Document doc)
        {
            var cadFiles = new Dictionary<ElementId, string>();

            // 1. ImportInstance elements (placed instances)
            var imports = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .ToList();

            foreach (var imp in imports)
            {
                if (imp.Category != null && !cadFiles.ContainsKey(imp.Category.Id))
                {
                    cadFiles[imp.Category.Id] = imp.Category.Name;
                }
            }

            // 2. From any found ImportInstance, get parent category and iterate ALL siblings
            //    This finds (2), (3) variants that may not have active instances
            try
            {
                foreach (var imp in imports)
                {
                    if (imp.Category?.Parent != null)
                    {
                        foreach (Category sibling in imp.Category.Parent.SubCategories)
                        {
                            if (!cadFiles.ContainsKey(sibling.Id))
                            {
                                cadFiles[sibling.Id] = sibling.Name;
                            }
                        }
                        break; // Only need to do this once — all siblings come from same parent
                    }
                }
            }
            catch { }

            // 3. Scan OST_ImportObjectStyles subcategories directly
            try
            {
                Category importCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_ImportObjectStyles);
                if (importCat != null)
                {
                    foreach (Category subCat in importCat.SubCategories)
                    {
                        if (!cadFiles.ContainsKey(subCat.Id))
                        {
                            cadFiles[subCat.Id] = subCat.Name;
                        }
                    }
                }
            }
            catch { }

            // 4. Scan ALL elements in the document that belong to imported categories
            try
            {
                var allElements = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .ToElements();

                foreach (var elem in allElements)
                {
                    if (elem.Category != null && elem.Category.Parent != null)
                    {
                        var parentId = elem.Category.Parent.Id;
                        if (parentId == new ElementId(BuiltInCategory.OST_ImportObjectStyles))
                        {
                            if (!cadFiles.ContainsKey(elem.Category.Id))
                            {
                                cadFiles[elem.Category.Id] = elem.Category.Name;
                            }
                        }
                    }
                }
            }
            catch { }

            return cadFiles
                .Select(kvp => new ElementItem { Id = kvp.Key, Name = kvp.Value })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<CadLinkLocationItem> GetCadLinkLocations(Document doc, string cadFileName)
        {
            var imports = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(imp => imp.Category != null && imp.Category.Name == cadFileName)
                .ToList();

            if (imports.Count == 0)
                return new List<CadLinkLocationItem>
                {
                    new CadLinkLocationItem
                    {
                        Id = ElementId.InvalidElementId,
                        Name = "Not found in any view",
                        LinkType = "",
                        IsPinned = false
                    }
                };

            var results = new List<CadLinkLocationItem>();
            foreach (var imp in imports)
            {
                string linkType = imp.IsLinked ? "Link" : "Import";
                bool isPinned = imp.Pinned;

                // Determine which view owns this instance
                ElementId ownerViewId = imp.OwnerViewId;
                string viewName;
                ElementId navigateId;

                if (ownerViewId != null && ownerViewId != ElementId.InvalidElementId)
                {
                    var view = doc.GetElement(ownerViewId) as View;
                    viewName = view != null ? view.Name : "Unknown View";
                    navigateId = ownerViewId;
                }
                else
                {
                    // 3D / model-space import — not view-specific
                    viewName = "<Project-wide / 3D>";
                    navigateId = ElementId.InvalidElementId;
                }

                results.Add(new CadLinkLocationItem
                {
                    Id = navigateId,
                    InstanceId = imp.Id,
                    Name = viewName,
                    CadFileName = cadFileName,
                    LinkType = linkType,
                    IsPinned = isPinned
                });
            }

            return results
                .OrderBy(r => r.Name)
                .ToList();
        }
    }

    public class CadLinkLocationItem
    {
        public ElementId Id { get; set; }
        public ElementId InstanceId { get; set; }
        public string Name { get; set; }
        public string CadFileName { get; set; }
        public string LinkType { get; set; }
        public bool IsPinned { get; set; }
        public bool IsClickable => Id != null && Id != ElementId.InvalidElementId;
    }
}
