using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ElementsTags
{
    public class ElementsTagsExternalEventHandler : IExternalEventHandler
    {
        public string Action { get; set; }
        public List<ViewModels.CategoryItemViewModel> SelectedCategories { get; set; }
        public bool AddLeader { get; set; }
        public bool OnlyUntagged { get; set; }
        public bool AutoRehostSection { get; set; }
        public Action<string> NotifyStatus { get; set; }
        public Action<List<ViewModels.ErrorItemViewModel>> ReportErrors { get; set; }
        public List<ViewModels.ErrorItemViewModel> SelectedTagsToUpdate { get; set; }
        public ElementId ElementIdToShow { get; set; }

        // Session-level set: tracks tag IDs that had leaders auto-added by the tool (no leader originally)
        private readonly HashSet<ElementId> _autoAddedLeaderTagIds = new HashSet<ElementId>();

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            View view = doc.ActiveView;

            using (Transaction trans = new Transaction(doc, "Elements Tags Operation"))
            {
                trans.Start();

                try
                {
                    if (Action == "TagAll")
                    {
                        TagAll(doc, view);
                    }
                    else if (Action == "CheckTag2D")
                    {
                        CheckOrFixTags(doc, view, checkOnly: true);
                    }
                    else if (Action == "ClashTag")
                    {
                        CheckClashTags(doc, view);
                    }
                    else if (Action == "CheckWallTagSection")
                    {
                        CheckWallTagSection(doc, view);
                    }
                    else if (Action == "UpdateWallTagSection")
                    {
                        UpdateWallTagSection(doc, view);
                    }
                    else if (Action == "ResetAll")
                    {
                        ResetOverrides(doc, view, "All");
                    }
                    else if (Action == "ShowElement" && ElementIdToShow != null)
                    {
                        app.ActiveUIDocument.Selection.SetElementIds(new List<ElementId> { ElementIdToShow });
                        app.ActiveUIDocument.ShowElements(ElementIdToShow);
                    }

                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    NotifyStatus?.Invoke("Error: " + ex.Message);
                }
            }
        }

        private void TagAll(Document doc, View view)
        {
            if (SelectedCategories == null || !SelectedCategories.Any())
            {
                NotifyStatus?.Invoke("Please select at least one category to tag.");
                return;
            }

            int totalCount = 0;
            var collector = new RevitDataCollector(doc, view);

            // Calculate Cut Plane elevation for view filtering
            double? cutPlaneElevation = null;
            if (view is ViewPlan viewPlan)
            {
                try
                {
                    PlanViewRange range = viewPlan.GetViewRange();
                    ElementId levelId = range.GetLevelId(PlanViewPlane.CutPlane);
                    double offset = range.GetOffset(PlanViewPlane.CutPlane);
                    Level level = doc.GetElement(levelId) as Level;
                    if (level != null)
                    {
                        cutPlaneElevation = level.Elevation + offset;
                    }
                }
                catch { }
            }

            foreach (var item in SelectedCategories)
            {
                var elementsToTag = collector.GetUntaggedElements(item.Category);
                
                int count = 0;
                foreach (var elem in elementsToTag)
                {
                    try
                    {
                        // USER REQUIREMENT: Filter Walls and Columns by Cut Plane
                        if (cutPlaneElevation.HasValue && IsVerticalCategory(elem.Category))
                        {
                            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
                            if (bbox != null)
                            {
                                // Check if Cut Plane is between Min.Z and Max.Z
                                if (cutPlaneElevation < bbox.Min.Z || cutPlaneElevation > bbox.Max.Z)
                                {
                                    continue; // Skip if not cut
                                }
                            }
                        }

                        XYZ point = GetElementCenter(elem, view);
                        if (point == null) continue;

                        Reference hostRef = new Reference(elem);
                        IndependentTag tag = IndependentTag.Create(doc, item.SelectedTagType.Id, view.Id, hostRef, AddLeader, TagOrientation.Horizontal, point);
                        
                        if (tag != null) count++;
                    }
                    catch { }
                }
                totalCount += count;
            }

            NotifyStatus?.Invoke($"Tagged {totalCount} elements across {SelectedCategories.Count} categories.");
        }

        private bool IsVerticalCategory(Category category)
        {
            if (category == null) return false;
            int bic = (int)category.Id.GetIdValue();
            return bic == (int)BuiltInCategory.OST_Walls || 
                   bic == (int)BuiltInCategory.OST_Columns || 
                   bic == (int)BuiltInCategory.OST_StructuralColumns;
        }

        private void CheckOrFixTags(Document doc, View view, bool checkOnly)
        {
            // Get all tags in view
            var tagsArr = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            int warningCount = 0;
            var errorList = new List<ViewModels.ErrorItemViewModel>();

            var selectedCatIds = new HashSet<int>();
            if (SelectedCategories != null)
            {
                foreach (var cat in SelectedCategories)
                {
                    selectedCatIds.Add((int)cat.Category.Id.GetIdValue());
                }
            }

            foreach (var tag in tagsArr)
            {


                // Resolve Host Element (handle Link instances)
                Element currentHost = null;
                Transform linkTransform = Transform.Identity;
                
#if REVIT2022_OR_GREATER
                var refs = tag.GetTaggedReferences();
                if (refs.Any())
                {
                    Reference r = refs.First();
                    Element localElem = doc.GetElement(r.ElementId);
                    if (localElem is RevitLinkInstance linkInst)
                    {
                        Document linkDoc = linkInst.GetLinkDocument();
                        if (linkDoc != null) 
                        {
                            currentHost = linkDoc.GetElement(r.LinkedElementId);
                            linkTransform = linkInst.GetTotalTransform();
                        }
                    }
                    else
                    {
                        currentHost = localElem;
                    }
                }
#else
                if (tag.TaggedLocalElementId != ElementId.InvalidElementId)
                {
                    Element localElem = doc.GetElement(tag.TaggedLocalElementId);
                    if (localElem is RevitLinkInstance linkInst && tag.TaggedElementId.LinkedElementId != ElementId.InvalidElementId)
                    {
                        Document linkDoc = linkInst.GetLinkDocument();
                        if (linkDoc != null) 
                        {
                            currentHost = linkDoc.GetElement(tag.TaggedElementId.LinkedElementId);
                            linkTransform = linkInst.GetTotalTransform();
                        }
                    }
                    else
                    {
                        currentHost = localElem;
                    }
                }
#endif
                else
                {
                    // SMART RE-HOST: If orphaned, try to find the nearest host
                    currentHost = FindNearestHost(doc, view, tag, selectedCatIds);
                    if (currentHost != null)
                    {
                        // Save properties from old tag
                        ElementId symbolId = tag.GetTypeId();
                        TagOrientation orientation = tag.TagOrientation;
                        XYZ headPos = tag.TagHeadPosition;
                        bool hasLeader = tag.HasLeader;
                        
                        // Create NEW tag with identified host
                        IndependentTag newTag = IndependentTag.Create(doc, symbolId, view.Id, new Reference(currentHost), hasLeader, orientation, headPos);
                        
                        // Delete old orphaned tag
                        doc.Delete(tag.Id);
                        
                        // Switch current processing to the new tag so overrides/checks continue
                        var oldTag = tag;
                        // Since tag is the loop variable, we can't reassignment it in a FOREACH.
                        // We will just report success and skip the rest of this check for this specific tag
                        // because the new tag will be caught next time or doesn't need immediate re-overriding.
                        warningCount++;
                        continue; 
                    }
                }

                if (currentHost == null || currentHost.Category == null) continue;

                // Only check tags of selected categories
                if (!selectedCatIds.Contains((int)currentHost.Category.Id.GetIdValue())) continue;

                bool isVerticalCat = false;
                if (currentHost.Category != null)
                {
                    int bic = (int)currentHost.Category.Id.GetIdValue();
                    if (bic == (int)BuiltInCategory.OST_Walls ||
                        bic == (int)BuiltInCategory.OST_Columns ||
                        bic == (int)BuiltInCategory.OST_StructuralColumns ||
                        bic == (int)BuiltInCategory.OST_StructuralFoundation)
                    {
                        isVerticalCat = true;
                    }
                }

                // Capture original leader state BEFORE any modification
                bool originallyHadLeader = tag.HasLeader;

                // --- AUTO-ADD ATTACHED LEADER if tag has no leader (vertical cat) ---
                // Step 1: Set Attached → Revit auto-snaps leader end onto the element surface
                // Step 2: Convert to Free End → "freezes" that snapped point so GetLeaderEnd() can read it
                if (!originallyHadLeader && isVerticalCat)
                {
                    tag.HasLeader = true;
                    tag.LeaderEndCondition = LeaderEndCondition.Attached;
                    tag.LeaderEndCondition = LeaderEndCondition.Free; // Freeze the snapped point
                    _autoAddedLeaderTagIds.Add(tag.Id);
                }

                // --- GET LEADER END POINT ---
                // For Free End Leader (including auto-converted): GetLeaderEnd() reliably returns the touch point.
                // For Attached Leader (original): GetLeaderEnd() may throw; fallback to deprecated LeaderEnd.
                XYZ leaderEndPoint = null;
                if (tag.HasLeader)
                {
                    try
                    {
#if REVIT2022_OR_GREATER
                        var refsCol = tag.GetTaggedReferences();
                        if (refsCol.Any()) leaderEndPoint = tag.GetLeaderEnd(refsCol.First());
#else
                        leaderEndPoint = tag.LeaderEnd;
#endif
                    }
                    catch
                    {
                    }
                }

                // --- VALIDATE ---
                bool isValid = true;

                if (tag.HasLeader)
                {
                    // Both Attached and Free leader: leader end must touch the element.
                    // For Attached: Revit guarantees it touches → if GetLeaderEnd succeeded, verify; if not, trust Revit.
                    if (leaderEndPoint != null)
                    {
                        XYZ evalPoint = linkTransform.IsIdentity ? leaderEndPoint : linkTransform.Inverse.OfPoint(leaderEndPoint);

                        if (currentHost is Floor floorL)
                        {
                            isValid = IsPointInFloorBoundary(floorL, evalPoint, view);
                        }
                        else
                        {
                            double tol = 0.5; // Leader end must be close to the element
                            BoundingBoxXYZ bbox = currentHost.get_BoundingBox(null);
                            if (bbox != null)
                            {
                                var min = bbox.Min - new XYZ(tol, tol, 0.1);
                                var max = bbox.Max + new XYZ(tol, tol, 0.1);
                                if (evalPoint.X < min.X || evalPoint.X > max.X ||
                                    evalPoint.Y < min.Y || evalPoint.Y > max.Y)
                                {
                                    isValid = false;
                                }
                            }
                        }
                    }
                    else if (originallyHadLeader && tag.LeaderEndCondition == LeaderEndCondition.Attached)
                    {
                        // GetLeaderEnd failed for Attached Leader → Revit guarantees it touches, accept as valid.
                        isValid = true;
                    }
                    else
                    {
                        // GetLeaderEnd failed for Free End Leader → cannot verify, mark as invalid.
                        isValid = false;
                    }
                }
                else
                {
                    // No leader (non-vertical cat): check tag head position against element
                    XYZ headPos = tag.TagHeadPosition;
                    XYZ evalHead = linkTransform.IsIdentity ? headPos : linkTransform.Inverse.OfPoint(headPos);

                    if (currentHost is Floor floorN)
                    {
                        isValid = IsPointInFloorBoundary(floorN, evalHead, view);
                    }
                    else
                    {
                        BoundingBoxXYZ bbox = currentHost.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            var min = bbox.Min - new XYZ(0.1, 0.1, 0.1);
                            var max = bbox.Max + new XYZ(0.1, 0.1, 0.1);
                            if (evalHead.X < min.X || evalHead.X > max.X ||
                                evalHead.Y < min.Y || evalHead.Y > max.Y)
                            {
                                isValid = false;
                            }
                        }
                    }
                }

                if (!isValid)
                {
                    Color highlightColor = new Color(255, 0, 0);
                    var matchingCat = SelectedCategories?.FirstOrDefault(c => c.Category.Id.GetIdValue() == currentHost.Category.Id.GetIdValue());
                    if (matchingCat != null) highlightColor = matchingCat.OverrideColor;

                    OverrideGraphicSettings settings = new OverrideGraphicSettings();
                    settings.SetProjectionLineColor(highlightColor);

                    view.SetElementOverrides(tag.Id, settings);
                    warningCount++;


                    errorList.Add(new ViewModels.ErrorItemViewModel 
                    {
                        ElementId = tag.Id,
                        IdValue = currentHost.Id.GetIdValue().ToString(),
                        Category = currentHost.Category?.Name ?? "Unknown",
                        ErrorType = "Misplaced Tag"
                    });
                }
                else
                {
                    view.SetElementOverrides(tag.Id, new OverrideGraphicSettings());
                }
            }

            ReportErrors?.Invoke(errorList);
            NotifyStatus?.Invoke($"Found {warningCount} misplaced tags (Red).");
        }

        private void ResetOverrides(Document doc, View view, string target)
        {
            var selectedCatIds = SelectedCategories?.Select(c => c.Category.Id).ToList() ?? new List<ElementId>();

            if (target == "Tags" || target == "All")
            {
                var tags = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .ToList();

                foreach (var tag in tags)
                {
                    if (selectedCatIds.Any())
                    {
                        // Resolve Host (support Links)
                        Element host = null;
#if REVIT2022_OR_GREATER
                        var rCol = tag.GetTaggedReferences();
#else
                        var rCol = new List<Reference> { tag.TaggedLocalElementId != null ? new Reference(doc.GetElement(tag.TaggedLocalElementId)) : null }.Where(r => r != null);
#endif
                        if (rCol.Any())
                        {
                            Reference r = rCol.First();
                            Element localElem = doc.GetElement(r.ElementId);
                            if (localElem is RevitLinkInstance linkInst)
                            {
                                Document linkDoc = linkInst.GetLinkDocument();
                                if (linkDoc != null) host = linkDoc.GetElement(r.LinkedElementId);
                            }
                            else host = localElem;
                        }

                        if (host == null || host.Category == null || !selectedCatIds.Contains(host.Category.Id))
                        {
                            continue;
                        }

                        // Only remove leaders that were AUTO-ADDED by the tool (tracked during check)
                        if (_autoAddedLeaderTagIds.Contains(tag.Id))
                        {
                            tag.HasLeader = false;
                        }
                    }
                    view.SetElementOverrides(tag.Id, new OverrideGraphicSettings());
                }
                _autoAddedLeaderTagIds.Clear();
            }

            if (target == "Floors" || target == "All")
            {
                if (selectedCatIds.Any())
                {
                    foreach (var catId in selectedCatIds)
                    {
                        var elements = new FilteredElementCollector(doc, view.Id)
                            .OfCategoryId(catId)
                            .WhereElementIsNotElementType()
                            .ToList();

                        foreach (var elem in elements)
                        {
                            view.SetElementOverrides(elem.Id, new OverrideGraphicSettings());
                        }
                    }
                }
            }

            NotifyStatus?.Invoke($"All {target.ToLower()} overrides have been reset.");
        }

        private void CheckClashTags(Document doc, View view)
        {
            var selectedCatIds = SelectedCategories?.Select(c => (int)c.Category.Id.GetIdValue()).ToHashSet() ?? new HashSet<int>();
            
            var allTags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .Where(t => {
#if REVIT2022_OR_GREATER
                    var hostId = t.GetTaggedLocalElementIds().FirstOrDefault();
#else
                    var hostId = t.TaggedLocalElementId;
#endif
                    if (hostId == null) return false;
                    var host = doc.GetElement(hostId);
                    return host != null && host.Category != null && selectedCatIds.Contains((int)host.Category.Id.GetIdValue());
                })
                .ToList();

            var other2D = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(TextNote))
                .Cast<TextNote>()
                .ToList();

            var errorList = new List<ViewModels.ErrorItemViewModel>();
            int clashCount = 0;
            Color orange = new Color(255, 165, 0);
            OverrideGraphicSettings orangeSettings = new OverrideGraphicSettings();
            orangeSettings.SetProjectionLineColor(orange);

            for (int i = 0; i < allTags.Count; i++)
            {
                var tag1 = allTags[i];
                BoundingBoxXYZ bbox1 = tag1.get_BoundingBox(view);
                if (bbox1 == null) continue;

                bool clashing = false;

                // Check vs other tags
                for (int j = 0; j < allTags.Count; j++)
                {
                    if (i == j) continue;
                    var tag2 = allTags[j];
                    BoundingBoxXYZ bbox2 = tag2.get_BoundingBox(view);
                    if (bbox2 != null && Intersects(bbox1, bbox2))
                    {
                        clashing = true;
                        break;
                    }
                }

                // Check vs TextNotes
                if (!clashing)
                {
                    foreach (var tn in other2D)
                    {
                        BoundingBoxXYZ bboxTn = tn.get_BoundingBox(view);
                        if (bboxTn != null && Intersects(bbox1, bboxTn))
                        {
                            clashing = true;
                            break;
                        }
                    }
                }

                if (clashing)
                {
                    view.SetElementOverrides(tag1.Id, orangeSettings);
                    clashCount++;
                    errorList.Add(new ViewModels.ErrorItemViewModel 
                    {
                        ElementId = tag1.Id,
                        IdValue = tag1.Id.GetIdValue().ToString(),
                        Category = "Tag",
                        ErrorType = "Clash"
                    });
                }
                else
                {
                    view.SetElementOverrides(tag1.Id, new OverrideGraphicSettings());
                }
            }

            ReportErrors?.Invoke(errorList);
            NotifyStatus?.Invoke($"Found {clashCount} clashing tags (Orange).");
        }

        private bool Intersects(BoundingBoxXYZ b1, BoundingBoxXYZ b2)
        {
            return b1.Min.X < b2.Max.X && b1.Max.X > b2.Min.X &&
                   b1.Min.Y < b2.Max.Y && b1.Max.Y > b2.Min.Y;
        }

        private void CheckUntaggedFloors(Document doc, View view)
        {
            if (SelectedCategories == null || !SelectedCategories.Any())
            {
                NotifyStatus?.Invoke("Please select at least one category to check.");
                return;
            }

            var tags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            var taggedElementIds = new HashSet<ElementId>();
            foreach (var tag in tags)
            {
#if REVIT2022_OR_GREATER
                var hostIds = tag.GetTaggedLocalElementIds();
#else
                var hostIds = new List<ElementId> { tag.TaggedLocalElementId };
#endif
                foreach (var hostId in hostIds)
                {
                    taggedElementIds.Add(hostId);
                }
            }

            ElementId solidFillPatternId = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill)?.Id;

            int warningCount = 0;
            var errorList = new List<ViewModels.ErrorItemViewModel>();
            
            foreach (var item in SelectedCategories)
            {
                Color catColor = item.OverrideColor;
                OverrideGraphicSettings settings = new OverrideGraphicSettings();
                settings.SetProjectionLineColor(catColor);
                settings.SetCutLineColor(catColor);

                if (solidFillPatternId != null)
                {
                    settings.SetSurfaceForegroundPatternId(solidFillPatternId);
                    settings.SetSurfaceForegroundPatternColor(catColor);
                    settings.SetCutForegroundPatternId(solidFillPatternId);
                    settings.SetCutForegroundPatternColor(catColor);
                }

                var elements = new FilteredElementCollector(doc, view.Id)
                    .OfCategoryId(item.Category.Id)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var elem in elements)
                {
                    if (!taggedElementIds.Contains(elem.Id))
                    {
                        view.SetElementOverrides(elem.Id, settings);
                        warningCount++;

                        errorList.Add(new ViewModels.ErrorItemViewModel 
                        {
                            ElementId = elem.Id,
                            IdValue = elem.Id.GetIdValue().ToString(),
                            Category = item.Category.Name,
                            ErrorType = "Untagged"
                        });
                    }
                    else
                    {
                        view.SetElementOverrides(elem.Id, new OverrideGraphicSettings());
                    }
                }
            }

            ReportErrors?.Invoke(errorList);
            NotifyStatus?.Invoke($"Found {warningCount} untagged 3D elements (Cyan).");
        }

        private void CheckWallTagSection(Document doc, View view)
        {
            var tagsArr = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            int warningCount = 0;
            int rehostedCount = 0;
            var errorList = new List<ViewModels.ErrorItemViewModel>();

            var allWalls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var tag in tagsArr)
            {
                // Only process Wall Tags
                if (tag.Category == null || tag.Category.Id.GetIdValue() != (long)BuiltInCategory.OST_WallTags) 
                {
                    continue;
                }

                Element currentHost = null;
                Transform linkTransform = Transform.Identity;
                
#if REVIT2022_OR_GREATER
                var refs = tag.GetTaggedReferences();
                if (refs.Any())
                {
                    Reference r = refs.First();
                    Element localElem = doc.GetElement(r.ElementId);
                    if (localElem is RevitLinkInstance linkInst)
                    {
                        Document linkDoc = linkInst.GetLinkDocument();
                        if (linkDoc != null) 
                        {
                            currentHost = linkDoc.GetElement(r.LinkedElementId);
                            linkTransform = linkInst.GetTotalTransform();
                        }
                    }
                    else
                    {
                        currentHost = localElem;
                    }
                }
#else
                if (tag.TaggedLocalElementId != ElementId.InvalidElementId)
                {
                    Element localElem = doc.GetElement(tag.TaggedLocalElementId);
                    if (localElem is RevitLinkInstance linkInst && tag.TaggedElementId.LinkedElementId != ElementId.InvalidElementId)
                    {
                        Document linkDoc = linkInst.GetLinkDocument();
                        if (linkDoc != null) 
                        {
                            currentHost = linkDoc.GetElement(tag.TaggedElementId.LinkedElementId);
                            linkTransform = linkInst.GetTotalTransform();
                        }
                    }
                    else
                    {
                        currentHost = localElem;
                    }
                }
#endif

                bool originallyHadLeader = tag.HasLeader;

                // Auto-add leader to check if it points to wall
                if (!originallyHadLeader)
                {
                    tag.HasLeader = true;
                    tag.LeaderEndCondition = LeaderEndCondition.Attached;
                    tag.LeaderEndCondition = LeaderEndCondition.Free;
                    _autoAddedLeaderTagIds.Add(tag.Id);
                }

                XYZ leaderEndPoint = null;
                if (tag.HasLeader)
                {
                    try
                    {
#if REVIT2022_OR_GREATER
                        var refsCol = tag.GetTaggedReferences();
                        if (refsCol.Any()) leaderEndPoint = tag.GetLeaderEnd(refsCol.First());
#else
                        leaderEndPoint = tag.LeaderEnd;
#endif
                    }
                    catch { }
                }

                bool isValid = true;
                XYZ evalPoint = tag.HasLeader && leaderEndPoint != null ? 
                    (linkTransform.IsIdentity ? leaderEndPoint : linkTransform.Inverse.OfPoint(leaderEndPoint)) : 
                    (linkTransform.IsIdentity ? tag.TagHeadPosition : linkTransform.Inverse.OfPoint(tag.TagHeadPosition));

                if (currentHost == null || currentHost.Category == null || currentHost.Category.Id.GetIdValue() != (long)BuiltInCategory.OST_Walls)
                {
                    isValid = false;
                }
                else
                {
                    double tol = 0.5;
                    BoundingBoxXYZ bbox = currentHost.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        var min = bbox.Min - new XYZ(tol, tol, tol);
                        var max = bbox.Max + new XYZ(tol, tol, tol);
                        if (evalPoint.X < min.X || evalPoint.X > max.X ||
                            evalPoint.Y < min.Y || evalPoint.Y > max.Y ||
                            evalPoint.Z < min.Z || evalPoint.Z > max.Z)
                        {
                            isValid = false;
                        }
                    }
                }

                if (!isValid)
                {
                    bool rehosted = false;

                    if (AutoRehostSection)
                    {
                        Element correctWall = null;
                        foreach (var wall in allWalls)
                        {
                            BoundingBoxXYZ wBbox = wall.get_BoundingBox(null);
                            if (wBbox != null)
                            {
                                var wMin = wBbox.Min - new XYZ(0.5, 0.5, 0.5);
                                var wMax = wBbox.Max + new XYZ(0.5, 0.5, 0.5);
                                if (evalPoint.X >= wMin.X && evalPoint.X <= wMax.X &&
                                    evalPoint.Y >= wMin.Y && evalPoint.Y <= wMax.Y &&
                                    evalPoint.Z >= wMin.Z && evalPoint.Z <= wMax.Z)
                                {
                                    correctWall = wall;
                                    break;
                                }
                            }
                        }

                        if (correctWall != null)
                        {
                            // Re-host the tag (recreate it)
                            ElementId symbolId = tag.GetTypeId();
                            TagOrientation orientation = tag.TagOrientation;
                            XYZ headPos = tag.TagHeadPosition;
                            
                            IndependentTag newTag = IndependentTag.Create(doc, symbolId, view.Id, new Reference(correctWall), originallyHadLeader, orientation, headPos);
                            
                            // Restore leader end and elbow if it had a free leader
                            if (originallyHadLeader)
                            {
                                newTag.LeaderEndCondition = tag.LeaderEndCondition;
                                if (tag.LeaderEndCondition == LeaderEndCondition.Free)
                                {
                                    try
                                    {
#if REVIT2022_OR_GREATER
                                        var refsCol = tag.GetTaggedReferences();
                                        var newRefsCol = newTag.GetTaggedReferences();
                                        if (refsCol.Any() && newRefsCol.Any())
                                        {
                                            newTag.SetLeaderEnd(newRefsCol.First(), tag.GetLeaderEnd(refsCol.First()));
                                            newTag.SetLeaderElbow(newRefsCol.First(), tag.GetLeaderElbow(refsCol.First()));
                                        }
#else
                                        newTag.LeaderEnd = tag.LeaderEnd;
                                        newTag.LeaderElbow = tag.LeaderElbow;
#endif
                                    }
                                    catch { }
                                }
                            }
                            
                            // Force head position to exactly match original
                            newTag.TagHeadPosition = headPos;
                            newTag.HasLeader = originallyHadLeader;

                            doc.Delete(tag.Id);
                            rehosted = true;
                            rehostedCount++;

                            errorList.Add(new ViewModels.ErrorItemViewModel 
                            {
                                ElementId = newTag.Id,
                                IdValue = correctWall.Id.GetIdValue().ToString(),
                                Category = "Walls",
                                ErrorType = "Re-Hosted (Updated)"
                            });
                        }
                    }

                    if (!rehosted)
                    {
                        Color highlightColor = new Color(255, 0, 0); // Red
                        
                        OverrideGraphicSettings settings = new OverrideGraphicSettings();
                        settings.SetProjectionLineColor(highlightColor);

                        view.SetElementOverrides(tag.Id, settings);
                        warningCount++;

                        errorList.Add(new ViewModels.ErrorItemViewModel 
                        {
                            ElementId = tag.Id,
                            IdValue = currentHost?.Id.GetIdValue().ToString() ?? "N/A",
                            Category = "Walls",
                            ErrorType = "Misplaced Tag (Section)"
                        });
                    }
                }
                else
                {
                    view.SetElementOverrides(tag.Id, new OverrideGraphicSettings());
                }
            }

            ReportErrors?.Invoke(errorList);
            NotifyStatus?.Invoke($"Auto-Fixed {rehostedCount} tags. Found {warningCount} misplaced tags (Red).");
        }

        private void UpdateWallTagSection(Document doc, View view)
        {
            if (SelectedTagsToUpdate == null || !SelectedTagsToUpdate.Any()) return;

            int updatedCount = 0;
            var errorList = new List<ViewModels.ErrorItemViewModel>();

            var allWalls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .ToList();

            foreach (var selectedTagInfo in SelectedTagsToUpdate)
            {
                IndependentTag tag = doc.GetElement(selectedTagInfo.ElementId) as IndependentTag;
                if (tag == null) continue;

                Transform linkTransform = Transform.Identity;
                XYZ leaderEndPoint = null;
                bool originallyHadLeader = tag.HasLeader;

                if (tag.HasLeader)
                {
                    try
                    {
#if REVIT2022_OR_GREATER
                        var refsCol = tag.GetTaggedReferences();
                        if (refsCol.Any()) leaderEndPoint = tag.GetLeaderEnd(refsCol.First());
#else
                        leaderEndPoint = tag.LeaderEnd;
#endif
                    }
                    catch { }
                }

                XYZ evalPoint = tag.HasLeader && leaderEndPoint != null ? 
                    (linkTransform.IsIdentity ? leaderEndPoint : linkTransform.Inverse.OfPoint(leaderEndPoint)) : 
                    (linkTransform.IsIdentity ? tag.TagHeadPosition : linkTransform.Inverse.OfPoint(tag.TagHeadPosition));

                Element correctWall = null;
                foreach (var wall in allWalls)
                {
                    BoundingBoxXYZ wBbox = wall.get_BoundingBox(null);
                    if (wBbox != null)
                    {
                        var wMin = wBbox.Min - new XYZ(0.5, 0.5, 0.5);
                        var wMax = wBbox.Max + new XYZ(0.5, 0.5, 0.5);
                        if (evalPoint.X >= wMin.X && evalPoint.X <= wMax.X &&
                            evalPoint.Y >= wMin.Y && evalPoint.Y <= wMax.Y &&
                            evalPoint.Z >= wMin.Z && evalPoint.Z <= wMax.Z)
                        {
                            correctWall = wall;
                            break;
                        }
                    }
                }

                if (correctWall != null)
                {
                    // Re-host the tag (recreate it)
                    ElementId symbolId = tag.GetTypeId();
                    TagOrientation orientation = tag.TagOrientation;
                    XYZ headPos = tag.TagHeadPosition;
                    
                    IndependentTag newTag = IndependentTag.Create(doc, symbolId, view.Id, new Reference(correctWall), originallyHadLeader, orientation, headPos);
                    
                    if (originallyHadLeader)
                    {
                        newTag.LeaderEndCondition = tag.LeaderEndCondition;
                        if (tag.LeaderEndCondition == LeaderEndCondition.Free)
                        {
                            try
                            {
#if REVIT2022_OR_GREATER
                                var refsCol = tag.GetTaggedReferences();
                                var newRefsCol = newTag.GetTaggedReferences();
                                if (refsCol.Any() && newRefsCol.Any())
                                {
                                    newTag.SetLeaderEnd(newRefsCol.First(), tag.GetLeaderEnd(refsCol.First()));
                                    newTag.SetLeaderElbow(newRefsCol.First(), tag.GetLeaderElbow(refsCol.First()));
                                }
#else
                                newTag.LeaderEnd = tag.LeaderEnd;
                                newTag.LeaderElbow = tag.LeaderElbow;
#endif
                            }
                            catch { }
                        }
                    }

                    // Force head position to exactly match original
                    newTag.TagHeadPosition = headPos;
                    newTag.HasLeader = originallyHadLeader;

                    doc.Delete(tag.Id);
                    
                    // Highlight green for success
                    OverrideGraphicSettings settings = new OverrideGraphicSettings();
                    settings.SetProjectionLineColor(new Color(0, 128, 0)); // Green
                    view.SetElementOverrides(newTag.Id, settings);

                    updatedCount++;
                }
            }
            
            // Re-run CheckWallTagSection to refresh the list, but don't auto-rehost in that pass so we see the new state
            bool oldAutoRehost = AutoRehostSection;
            AutoRehostSection = false;
            CheckWallTagSection(doc, view);
            AutoRehostSection = oldAutoRehost;

            NotifyStatus?.Invoke($"Đã update thành công {updatedCount} tag(s).");
        }

        private XYZ GetElementCenter(Element elem, View view)
        {
            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
            if (bbox == null) return null;
            return (bbox.Min + bbox.Max) / 2;
        }

        private bool IsPointInFloorBoundary(Floor floor, XYZ point, View view)
        {
            Options opt = new Options { View = view, ComputeReferences = true };
            GeometryElement geo = floor.get_Geometry(opt);

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && !solid.Faces.IsEmpty)
                {
                    if (IsPointInSolidHorizontalProjection(solid, point)) return true;
                }
                else if (obj is GeometryInstance instance)
                {
                    GeometryElement instGeo = instance.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeo)
                    {
                        if (instObj is Solid instSolid && !instSolid.Faces.IsEmpty)
                        {
                            if (IsPointInSolidHorizontalProjection(instSolid, point)) return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool IsPointInSolidHorizontalProjection(Solid solid, XYZ point)
        {
            foreach (Face face in solid.Faces)
            {
                if (face is PlanarFace pf)
                {
                    // Check mostly horizontal faces (up or down)
                    if (Math.Abs(pf.FaceNormal.Z) > 0.01)
                    {
                        IntersectionResult projectResult = face.Project(point);
                        if (projectResult != null)
                        {
                            // Important: face.IsInside only cares about UV boundaries, regardless of face elevation
                            if (face.IsInside(projectResult.UVPoint)) return true;
                        }
                    }
                }
            }
            return false;
        }

        private Element FindNearestHost(Document doc, View view, IndependentTag tag, HashSet<int> categoryIds)
        {
            XYZ headPos = tag.TagHeadPosition;
            double searchRadius = 5.0; // 5 feet

            Outline searchOutline = new Outline(headPos - new XYZ(searchRadius, searchRadius, 0.5), 
                                               headPos + new XYZ(searchRadius, searchRadius, 0.5));
            
            var collector = new FilteredElementCollector(doc, view.Id)
                .WherePasses(new BoundingBoxIntersectsFilter(searchOutline))
                .WhereElementIsNotElementType();

            Element nearest = null;
            double minDistance = double.MaxValue;

            foreach (Element elem in collector)
            {
                if (elem.Category == null || !categoryIds.Contains((int)elem.Category.Id.GetIdValue())) continue;

                BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
                if (bbox == null) continue;
                
                XYZ center = (bbox.Min + bbox.Max) / 2;
                double dist = headPos.DistanceTo(center);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = elem;
                }
            }

            return nearest;
        }

        public string GetName() => "ElementsTagsHandler";
    }

    public static class TagExtensions
    {
        public static bool IsTaggingCategory(this IndependentTag tag, ElementId categoryId)
        {
#if REVIT2022_OR_GREATER
            var hostIds = tag.GetTaggedLocalElementIds();
#else
            var hostIds = new List<ElementId> { tag.TaggedLocalElementId };
#endif
            if (!hostIds.Any()) return false;
            var doc = tag.Document;
            var firstHost = doc.GetElement(hostIds.First());
            return firstHost != null && firstHost.Category != null && firstHost.Category.Id == categoryId;
        }
    }
}
