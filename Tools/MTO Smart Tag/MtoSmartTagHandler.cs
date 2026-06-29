using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.MtoSmartTag
{
    /// <summary>
    /// Offset direction for tag placement relative to the insertion point (circle).
    /// </summary>
    public enum OffsetDirection
    {
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public class MtoSmartTagHandler : IExternalEventHandler
    {
        public string Action { get; set; }
        public List<string> TargetFamilyNames { get; set; } = new List<string>();
        public OffsetDirection Direction { get; set; } = OffsetDirection.TopLeft;
        public double OffsetDistanceMm { get; set; } = 150; // mm
        public double OffsetXMm { get; set; } = 0; // Direct X offset in mm
        public double OffsetYMm { get; set; } = 0; // Direct Y offset in mm
        public bool UseDirectOffset { get; set; } = false; // Use X/Y instead of direction
        public bool AddLeader { get; set; } = false;
        public bool ForceRetag { get; set; } = false;
        public bool OnlyAlreadyTagged { get; set; } = false;
        public bool ApplyColorOverride { get; set; } = false;
        public byte ColorR { get; set; } = 255;
        public byte ColorG { get; set; } = 0;
        public byte ColorB { get; set; } = 0;
        public string LayerDirection { get; set; } // "X" or "Y"
        public ElementId SelectedTagTypeId { get; set; }
        public Action<string> NotifyStatus { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            View view = doc.ActiveView;

            using (Transaction trans = new Transaction(doc, "MTO Smart Tag"))
            {
                trans.Start();

                try
                {
                    if (Action == "TagAll")
                    {
                        TagAllReinforcementDistributions(doc, view);
                    }
                    else if (Action == "ResetColor")
                    {
                        ResetColorOverrides(doc, view);
                    }
                    else if (Action == "HideLayer")
                    {
                        SetLayerVisibility(doc, view, LayerDirection, hide: true);
                    }
                    else if (Action == "ShowLayer")
                    {
                        SetLayerVisibility(doc, view, LayerDirection, hide: false);
                    }
                    else if (Action == "ShowAll")
                    {
                        ShowAllLayers(doc, view);
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

        private void TagAllReinforcementDistributions(Document doc, View view)
        {
            // 1. Find all Detail Items matching selected families
            var detailItems = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName != null && TargetFamilyNames.Contains(type.FamilyName);
                })
                .ToList();

            if (!detailItems.Any())
            {
                NotifyStatus?.Invoke($"No matching detail items found in current view for the selected families.");
                return;
            }

            // 2. Find tag type
            FamilySymbol tagType = null;
            if (SelectedTagTypeId != null && SelectedTagTypeId != ElementId.InvalidElementId)
            {
                tagType = doc.GetElement(SelectedTagTypeId) as FamilySymbol;
            }

            if (tagType == null)
            {
                // Try to find tag family automatically (RINCO_TAG_Reo or Reo Tag_Mark)
                tagType = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(fs =>
                        fs.Category != null &&
                        fs.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_DetailComponentTags &&
                        (fs.FamilyName.Contains("RINCO_TAG_Reo") || fs.FamilyName.Contains("Reo Tag_Mark")));
            }

            if (tagType == null)
            {
                NotifyStatus?.Invoke("Cannot find Detail Item Tag family. Please ensure a tag family (e.g. 'Reo Tag_Mark') is loaded.");
                return;
            }

            // 3. Get already tagged element IDs
            var existingTags = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            var taggedIds = new HashSet<ElementId>();
            foreach (var tag in existingTags)
            {
#if REVIT2022_OR_GREATER
                foreach (var id in tag.GetTaggedLocalElementIds())
#else
                foreach (var id in new List<ElementId> { tag.TaggedLocalElementId })
#endif
                {
                    taggedIds.Add(id);
                }
            }

            // 4. Calculate offset vector
            XYZ offsetVector;
            if (UseDirectOffset)
            {
                double ox = OffsetXMm / 304.8;
                double oy = OffsetYMm / 304.8;
                offsetVector = new XYZ(ox, oy, 0);
            }
            else
            {
                double offsetFeet = OffsetDistanceMm / 304.8;
                offsetVector = GetOffsetVector(Direction, offsetFeet);
            }

            // 5. Tag each detail item
            int taggedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;
            string debugInfo = "";

            foreach (var item in detailItems)
            {
                bool alreadyTagged = taggedIds.Contains(item.Id);

                // OnlyAlreadyTagged: skip items that DON'T have a tag
                if (OnlyAlreadyTagged && !alreadyTagged)
                {
                    skippedCount++;
                    continue;
                }

                // Normal mode: skip items that already have a tag (unless ForceRetag)
                if (!OnlyAlreadyTagged && alreadyTagged && !ForceRetag)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    // Get dot position (the circle on the distribution symbol)
                    XYZ dotPos = GetDotPosition(item, doc, view);
                    if (dotPos == null)
                    {
                        failedCount++;
                        if (string.IsNullOrEmpty(debugInfo))
                        {
                            debugInfo += $"\n❌ Element {item.Id} has no location or bounding box.";
                        }
                        continue;
                    }

                    // Build debug info for first item
                    if (string.IsNullOrEmpty(debugInfo))
                    {
                        // Get all positions for comparison
                        XYZ locPt = (item.Location is LocationPoint lp) ? lp.Point : null;
                        BoundingBoxXYZ bbox = item.get_BoundingBox(view);
                        XYZ bboxCenter = bbox != null ? (bbox.Min + bbox.Max) / 2 : null;

                        double toMm = 304.8;
                        debugInfo = $"\n📍 Dot: ({dotPos.X * toMm:F0}, {dotPos.Y * toMm:F0})";
                        if (locPt != null)
                            debugInfo += $"\n📌 LocPt: ({locPt.X * toMm:F0}, {locPt.Y * toMm:F0})";
                        if (bboxCenter != null)
                            debugInfo += $"\n📦 BBox: ({bboxCenter.X * toMm:F0}, {bboxCenter.Y * toMm:F0})";
                        if (bbox != null)
                            debugInfo += $"\n   Min:({bbox.Min.X * toMm:F0},{bbox.Min.Y * toMm:F0}) Max:({bbox.Max.X * toMm:F0},{bbox.Max.Y * toMm:F0})";
                    }

                    // Tag position = dot position + offset
                    XYZ tagPosition = dotPos + offsetVector;

                    // Create the tag — ALWAYS with leader first so TagHeadPosition works
                    // (Without leader, Revit ignores position and snaps to element center)
                    Reference hostRef = new Reference(item);
                    IndependentTag tag = IndependentTag.Create(
                        doc,
                        tagType.Id,
                        view.Id,
                        hostRef,
                        true, // Always create with leader first
                        TagOrientation.Horizontal,
                        tagPosition);

                    if (tag != null)
                    {
                        // Toggle leader based on user preference BEFORE setting position
                        if (AddLeader)
                        {
                            tag.HasLeader = true;
                            tag.LeaderEndCondition = LeaderEndCondition.Free;
                            try
                            {
                                tag.SetLeaderEnd(hostRef, dotPos);
                            }
                            catch { }
                        }
                        else
                        {
                            tag.HasLeader = false;
                        }

                        // Force the tag head to the exact desired position AFTER modifying the leader state
                        tag.TagHeadPosition = tagPosition;

                        taggedCount++;

                        // Apply color override to the detail item
                        if (ApplyColorOverride)
                        {
                            var overrideSettings = new OverrideGraphicSettings();
                            var color = new Color(ColorR, ColorG, ColorB);
                            overrideSettings.SetProjectionLineColor(color);
                            overrideSettings.SetSurfaceForegroundPatternColor(color);
                            overrideSettings.SetCutLineColor(color);
                            view.SetElementOverrides(item.Id, overrideSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    if (string.IsNullOrEmpty(debugInfo)) debugInfo += $"\n❌ Error on {item.Id}: {ex.Message}";
                }
            }

            string msg = $"Tagged {taggedCount} items.";
            if (skippedCount > 0) msg += $" Skipped {skippedCount}.";
            if (failedCount > 0) msg += $" Failed {failedCount}.";
            msg += $" (Total: {detailItems.Count})";
            msg += debugInfo;
            NotifyStatus?.Invoke(msg);
        }

        /// <summary>
        /// Resets color overrides for target family items in the current view.
        /// </summary>
        private void ResetColorOverrides(Document doc, View view)
        {
            var detailItems = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName != null && TargetFamilyNames.Contains(type.FamilyName);
                })
                .ToList();

            int resetCount = 0;
            foreach (var item in detailItems)
            {
                // Reset to default (empty override)
                view.SetElementOverrides(item.Id, new OverrideGraphicSettings());
                resetCount++;
            }

            NotifyStatus?.Invoke($"Reset color for {resetCount} items.");
        }

        /// <summary>
        /// Hides or shows detail items of the target family based on their rotation angle.
        /// X = horizontal (0° or 180°), Y = vertical (90° or 270°).
        /// </summary>
        private void SetLayerVisibility(Document doc, View view, string direction, bool hide)
        {
            var detailItems = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName != null && TargetFamilyNames.Contains(type.FamilyName);
                })
                .ToList();

            var targetIds = new List<ElementId>();

            foreach (var item in detailItems)
            {
                if (item is FamilyInstance fi)
                {
                    double angleDeg = GetRotationDegrees(fi);

                    bool isX = IsHorizontal(angleDeg);
                    bool isY = IsVertical(angleDeg);

                    if ((direction == "X" && isX) || (direction == "Y" && isY))
                    {
                        targetIds.Add(item.Id);

                        // Also include sub-components (the nested dot annotation)
                        var subIds = fi.GetSubComponentIds();
                        if (subIds != null)
                        {
                            foreach (var subId in subIds)
                                targetIds.Add(subId);
                        }
                    }
                }
            }

            if (targetIds.Any())
            {
                if (hide)
                {
                    view.HideElements(targetIds);
                    NotifyStatus?.Invoke($"Hidden {targetIds.Count} Layer {direction} items (incl. dots).");
                }
                else
                {
                    view.UnhideElements(targetIds);
                    NotifyStatus?.Invoke($"Shown {targetIds.Count} Layer {direction} items.");
                }
            }
            else
            {
                NotifyStatus?.Invoke($"No Layer {direction} items found for selected families.");
            }
        }

        /// <summary>
        /// Shows all hidden target family items in the view.
        /// </summary>
        private void ShowAllLayers(Document doc, View view)
        {
            // Collect ALL instances of target family from the document (includes hidden ones)
            var allItems = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName != null && TargetFamilyNames.Contains(type.FamilyName);
                })
                .ToList();

            var allIds = new List<ElementId>();
            foreach (var item in allItems)
            {
                allIds.Add(item.Id);
                // Also include sub-components (nested dots)
                if (item is FamilyInstance fi)
                {
                    var subIds = fi.GetSubComponentIds();
                    if (subIds != null)
                    {
                        foreach (var subId in subIds)
                            allIds.Add(subId);
                    }
                }
            }

            if (allIds.Any())
            {
                try
                {
                    view.UnhideElements(allIds);
                }
                catch { /* Some elements may not be hidden */ }
                NotifyStatus?.Invoke($"Shown all {allIds.Count} items (incl. dots) for selected families.");
            }
            else
            {
                NotifyStatus?.Invoke($"No items found for selected families.");
            }
        }

        /// <summary>
        /// Gets the rotation angle in degrees from a FamilyInstance.
        /// </summary>
        private double GetRotationDegrees(FamilyInstance fi)
        {
            // Try to get rotation from the transform
            Transform transform = fi.GetTransform();
            // BasisX shows the direction the family's X axis points after placement
            XYZ basisX = transform.BasisX;

            // Angle of BasisX relative to world X axis
            double angleRad = Math.Atan2(basisX.Y, basisX.X);
            double angleDeg = angleRad * 180.0 / Math.PI;

            // Normalize to 0-360
            if (angleDeg < 0) angleDeg += 360;
            return angleDeg;
        }

        private bool IsHorizontal(double angleDeg)
        {
            // 0° ± 15° or 180° ± 15°
            double tolerance = 15;
            return (angleDeg <= tolerance || angleDeg >= 360 - tolerance) ||
                   (Math.Abs(angleDeg - 180) <= tolerance);
        }

        private bool IsVertical(double angleDeg)
        {
            // 90° ± 15° or 270° ± 15°
            double tolerance = 15;
            return (Math.Abs(angleDeg - 90) <= tolerance) ||
                   (Math.Abs(angleDeg - 270) <= tolerance);
        }

        /// <summary>
        /// Gets the position of the circle/dot on the Reinforcement Distribution symbol.
        /// The dot is a NESTED Generic Annotation family (e.g. Rincovitch_G_Anno_Dot)
        /// inside the main Reinforcement_Distribution family.
        /// Multiple strategies are used to locate it.
        /// </summary>
        private XYZ GetDotPosition(Element item, Document doc, View view)
        {
            // Strategy 1: Find nested sub-component via GetSubComponentIds()
            if (item is FamilyInstance fi)
            {
                var subIds = fi.GetSubComponentIds();
                if (subIds != null && subIds.Any())
                {
                    foreach (var subId in subIds)
                    {
                        Element subElem = doc.GetElement(subId);
                        if (subElem == null) continue;

                        if (subElem.Location is LocationPoint dotLoc)
                        {
                            return dotLoc.Point;
                        }
                    }
                }
            }

            // Strategy 2: Find the nearest Generic Annotation (dot) in the view
            // that is spatially close to this detail item
            XYZ dotFromView = FindNearestDotAnnotation(item, doc, view);
            if (dotFromView != null) return dotFromView;

            // Strategy 3: Scan the geometry for circle/arc shapes
            XYZ dotFromGeometry = FindDotFromGeometry(item, view);
            if (dotFromGeometry != null) return dotFromGeometry;

            // Fallback: LocationPoint of the main element
            if (item.Location is LocationPoint locPt)
            {
                return locPt.Point;
            }

            // Fallback for line-based elements
            if (item.Location is LocationCurve locCurve)
            {
                return locCurve.Curve.Evaluate(0.5, true); // Midpoint
            }

            // Last fallback: BoundingBox center
            BoundingBoxXYZ bbox = item.get_BoundingBox(view);
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2;
            }

            return null;
        }

        /// <summary>
        /// Searches the view for Generic Annotation instances (dots) that are
        /// spatially within the bounding box of the given detail item.
        /// </summary>
        private XYZ FindNearestDotAnnotation(Element item, Document doc, View view)
        {
            BoundingBoxXYZ itemBbox = item.get_BoundingBox(view);
            if (itemBbox == null) return null;

            // Expand search area slightly
            double tolerance = 1.0; // 1 foot
            XYZ searchMin = itemBbox.Min - new XYZ(tolerance, tolerance, tolerance);
            XYZ searchMax = itemBbox.Max + new XYZ(tolerance, tolerance, tolerance);

            var dotAnnotations = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    // Check if it's a dot family
                    if (e is FamilyInstance annFi)
                    {
                        var annType = doc.GetElement(annFi.GetTypeId()) as FamilySymbol;
                        if (annType != null &&
                            (annType.FamilyName.Contains("Dot") ||
                             annType.FamilyName.Contains("Anno_Dot") ||
                             annType.FamilyName.Contains("Rincovitch")))
                        {
                            return true;
                        }
                    }
                    return false;
                })
                .ToList();

            // Find the dot annotation whose location is within the detail item's bounding box
            XYZ bestDot = null;
            double bestDist = double.MaxValue;
            XYZ itemCenter = (itemBbox.Min + itemBbox.Max) / 2;

            foreach (var dot in dotAnnotations)
            {
                if (dot.Location is LocationPoint dotLoc)
                {
                    XYZ dotPt = dotLoc.Point;

                    // Check if the dot is within the expanded bounding box
                    if (dotPt.X >= searchMin.X && dotPt.X <= searchMax.X &&
                        dotPt.Y >= searchMin.Y && dotPt.Y <= searchMax.Y)
                    {
                        double dist = itemCenter.DistanceTo(dotPt);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestDot = dotPt;
                        }
                    }
                }
            }

            return bestDot;
        }

        /// <summary>
        /// Scans the geometry of the element to find circle/arc shapes.
        /// The center of the largest arc is assumed to be the dot position.
        /// </summary>
        private XYZ FindDotFromGeometry(Element item, View view)
        {
            Options opt = new Options { View = view, ComputeReferences = true };
            GeometryElement geo = item.get_Geometry(opt);
            if (geo == null) return null;

            XYZ bestCenter = null;
            double bestRadius = 0;

            foreach (GeometryObject obj in geo)
            {
                SearchGeometryForArcs(obj, ref bestCenter, ref bestRadius);
            }

            return bestCenter;
        }

        private void SearchGeometryForArcs(GeometryObject obj, ref XYZ bestCenter, ref double bestRadius)
        {
            if (obj is Arc arc)
            {
                // Full circle or arc
                if (arc.Radius > bestRadius)
                {
                    bestRadius = arc.Radius;
                    bestCenter = arc.Center;
                }
            }
            else if (obj is GeometryInstance geoInst)
            {
                GeometryElement instGeo = geoInst.GetInstanceGeometry();
                if (instGeo != null)
                {
                    foreach (GeometryObject subObj in instGeo)
                    {
                        SearchGeometryForArcs(subObj, ref bestCenter, ref bestRadius);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the offset vector for the given direction.
        /// </summary>
        private XYZ GetOffsetVector(OffsetDirection direction, double distance)
        {
            double diag = distance / Math.Sqrt(2);

            switch (direction)
            {
                case OffsetDirection.Top:
                    return new XYZ(0, distance, 0);
                case OffsetDirection.Bottom:
                    return new XYZ(0, -distance, 0);
                case OffsetDirection.Left:
                    return new XYZ(-distance, 0, 0);
                case OffsetDirection.Right:
                    return new XYZ(distance, 0, 0);
                case OffsetDirection.TopLeft:
                    return new XYZ(-diag, diag, 0);
                case OffsetDirection.TopRight:
                    return new XYZ(diag, diag, 0);
                case OffsetDirection.BottomLeft:
                    return new XYZ(-diag, -diag, 0);
                case OffsetDirection.BottomRight:
                    return new XYZ(diag, -diag, 0);
                default:
                    return new XYZ(0, distance, 0);
            }
        }

        public string GetName() => "MtoSmartTagHandler";
    }
}
