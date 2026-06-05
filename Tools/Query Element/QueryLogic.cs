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
                .GroupBy(g => g.Name)
                .Select(grp => new ElementItem
                {
                    Id = grp.First().Id,
                    Name = $"{grp.Key}  ({grp.Count()})"
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<GroupLocationItem> GetGroupLocationsByName(Document doc, string groupName)
        {
            var groups = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(g => g.OwnerViewId != ElementId.InvalidElementId && g.Name == groupName)
                .ToList();

            if (groups.Count == 0)
                return new List<GroupLocationItem>
                {
                    new GroupLocationItem
                    {
                        ViewId = ElementId.InvalidElementId,
                        GroupElementId = ElementId.InvalidElementId,
                        ViewName = "Not found",
                        GroupName = groupName
                    }
                };

            var results = new List<GroupLocationItem>();
            foreach (var g in groups)
            {
                var view = doc.GetElement(g.OwnerViewId) as View;
                string viewName = view != null ? view.Name : "Unknown View";

                // Try to find the sheet that contains this view
                string sheetInfo = "";
                if (view != null)
                {
                    var vp = new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .FirstOrDefault(v => v.ViewId == view.Id);
                    if (vp != null)
                    {
                        var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                        if (sheet != null)
                            sheetInfo = $"{sheet.SheetNumber} - {sheet.Name}";
                    }
                }

                results.Add(new GroupLocationItem
                {
                    ViewId = g.OwnerViewId,
                    GroupElementId = g.Id,
                    ViewName = viewName,
                    SheetInfo = sheetInfo,
                    GroupName = groupName
                });
            }

            return results.OrderBy(r => r.ViewName).ToList();
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

        /// <summary>
        /// Extract the raw group name from the display string "GroupName  (count)".
        /// </summary>
        public static string ParseGroupDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return displayName;
            int idx = displayName.LastIndexOf("  (");
            return idx > 0 ? displayName.Substring(0, idx) : displayName;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  3D MODEL GROUPS
        // ═══════════════════════════════════════════════════════════════════

        public static List<ElementItem> GetAllModelGroups3D(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(g => g.OwnerViewId == ElementId.InvalidElementId) // Model groups are NOT view-specific
                .GroupBy(g => g.Name)
                .Select(grp => new ElementItem
                {
                    Id = grp.First().Id,
                    Name = $"{grp.Key}  ({grp.Count()})"
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static List<ModelGroup3DLocationItem> GetModelGroup3DLocationsByName(Document doc, string groupName)
        {
            var groups = new FilteredElementCollector(doc)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(g => g.OwnerViewId == ElementId.InvalidElementId && g.Name == groupName)
                .ToList();

            if (groups.Count == 0)
                return new List<ModelGroup3DLocationItem>
                {
                    new ModelGroup3DLocationItem
                    {
                        GroupElementId = ElementId.InvalidElementId,
                        LevelName = "Not found",
                        LocationInfo = "",
                        GroupName = groupName
                    }
                };

            var results = new List<ModelGroup3DLocationItem>();
            foreach (var g in groups)
            {
                // Get level
                string levelName = "";
                try
                {
                    var levelParam = g.get_Parameter(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM)
                                  ?? g.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    if (levelParam != null && levelParam.AsElementId() != ElementId.InvalidElementId)
                    {
                        var level = doc.GetElement(levelParam.AsElementId()) as Level;
                        if (level != null) levelName = level.Name;
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(levelName))
                {
                    try
                    {
                        var levelId = g.LevelId;
                        if (levelId != null && levelId != ElementId.InvalidElementId)
                        {
                            var level = doc.GetElement(levelId) as Level;
                            if (level != null) levelName = level.Name;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(levelName)) levelName = "—";

                // Get location
                string locationInfo = "";
                try
                {
                    var loc = g.Location as LocationPoint;
                    if (loc != null)
                    {
                        var pt = loc.Point;
                        // Convert feet to mm
                        double x = System.Math.Round(pt.X * 304.8, 0);
                        double y = System.Math.Round(pt.Y * 304.8, 0);
                        double z = System.Math.Round(pt.Z * 304.8, 0);
                        locationInfo = $"{x}, {y}, {z}";
                    }
                }
                catch { }

                results.Add(new ModelGroup3DLocationItem
                {
                    GroupElementId = g.Id,
                    LevelName = levelName,
                    LocationInfo = locationInfo,
                    GroupName = groupName
                });
            }

            return results.OrderBy(r => r.LevelName).ToList();
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

    public class GroupLocationItem
    {
        public ElementId ViewId { get; set; }
        public ElementId GroupElementId { get; set; }
        public string ViewName { get; set; }
        public string SheetInfo { get; set; }
        public string GroupName { get; set; }
        public bool IsClickable => ViewId != null && ViewId != ElementId.InvalidElementId;
    }

    public class ModelGroup3DLocationItem
    {
        public ElementId GroupElementId { get; set; }
        public string LevelName { get; set; }
        public string LocationInfo { get; set; }
        public string GroupName { get; set; }
    }
}
