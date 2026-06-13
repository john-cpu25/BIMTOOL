using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Tools.CheckFold.Models;

namespace RincoNhan.Tools.CheckFold
{
    public static class CheckFoldLogic
    {
        // Conversion factor: 1 foot = 304.8 mm
        private const double FeetToMm = 304.8;

        // The built-in parameter name for step offset
        public const string StepParamName = "Height Offset From Level";

        /// <summary>
        /// Get all floors whose type name contains "fold" (case-insensitive) visible in the current view.
        /// </summary>
        public static List<Floor> GetFoldFloors(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f =>
                {
                    var floorType = doc.GetElement(f.GetTypeId()) as FloorType;
                    return floorType != null &&
                           floorType.Name.IndexOf("fold", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();
        }

        /// <summary>
        /// Get all floors in the view that are NOT fold floors.
        /// </summary>
        public static List<Floor> GetNonFoldFloors(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Where(f =>
                {
                    var floorType = doc.GetElement(f.GetTypeId()) as FloorType;
                    return floorType != null &&
                           floorType.Name.IndexOf("fold", StringComparison.OrdinalIgnoreCase) < 0;
                })
                .ToList();
        }

        /// <summary>
        /// Get the thickness of a floor type in mm.
        /// </summary>
        public static double GetFloorThickness(Document doc, Floor floor)
        {
            var floorType = doc.GetElement(floor.GetTypeId()) as FloorType;
            if (floorType == null) return 0;

            var cs = floorType.GetCompoundStructure();
            if (cs != null)
            {
                // Sum all layer widths
                double totalThickness = 0;
                for (int i = 0; i < cs.LayerCount; i++)
                {
                    totalThickness += cs.GetLayerWidth(i);
                }
                return totalThickness * FeetToMm;
            }

            // Fallback: try the Width parameter
            var widthParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
            if (widthParam != null && widthParam.HasValue)
            {
                return widthParam.AsDouble() * FeetToMm;
            }

            return 0;
        }

        /// <summary>
        /// Get the top elevation of a floor at its placement point (level offset + level elevation), in mm.
        /// </summary>
        public static double GetFloorTopElevation(Document doc, Floor floor)
        {
            // Get the level elevation
            var levelId = floor.LevelId;
            var level = doc.GetElement(levelId) as Level;
            double levelElevation = level != null ? level.Elevation : 0;

            // Get the height offset from level
            var offsetParam = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = 0;
            if (offsetParam != null && offsetParam.HasValue)
            {
                offset = offsetParam.AsDouble();
            }

            return (levelElevation + offset) * FeetToMm;
        }

        /// <summary>
        /// Get the bottom elevation of a floor = top elevation - thickness, in mm.
        /// </summary>
        public static double GetFloorBottomElevation(Document doc, Floor floor)
        {
            return GetFloorTopElevation(doc, floor) - GetFloorThickness(doc, floor);
        }

        /// <summary>
        /// Find the two adjacent slabs for a fold floor.
        /// Uses fold's own top/bottom elevation as reference to find the closest matching slabs.
        /// highSlab.top ≈ fold.top, lowSlab.top ≈ fold.bottom
        /// </summary>
        public static (Floor highSlab, Floor lowSlab) FindAdjacentSlabs(Document doc, Floor foldFloor, List<Floor> allNonFoldFloors)
        {
            var foldBB = foldFloor.get_BoundingBox(null);
            if (foldBB == null) return (null, null);

            // Get fold's own elevations as reference
            double foldTop = GetFloorTopElevation(doc, foldFloor);
            double foldBottom = GetFloorBottomElevation(doc, foldFloor);

            // Expand bounding box slightly for XY overlap search
            double tolerance = 0.5; // 0.5 feet ≈ 150mm
            XYZ expandMin = new XYZ(foldBB.Min.X - tolerance, foldBB.Min.Y - tolerance, foldBB.Min.Z - tolerance);
            XYZ expandMax = new XYZ(foldBB.Max.X + tolerance, foldBB.Max.Y + tolerance, foldBB.Max.Z + tolerance);

            // Find slabs whose bounding box overlaps in XY plane
            var nearbySlabs = new List<(Floor floor, double topElevation)>();

            foreach (var slab in allNonFoldFloors)
            {
                var slabBB = slab.get_BoundingBox(null);
                if (slabBB == null) continue;

                bool xOverlap = slabBB.Min.X <= expandMax.X && slabBB.Max.X >= expandMin.X;
                bool yOverlap = slabBB.Min.Y <= expandMax.Y && slabBB.Max.Y >= expandMin.Y;

                if (xOverlap && yOverlap)
                {
                    double topElev = GetFloorTopElevation(doc, slab);
                    nearbySlabs.Add((slab, topElev));
                }
            }

            if (nearbySlabs.Count < 2) return (nearbySlabs.FirstOrDefault().floor, null);

            // Find slab whose top is closest to fold's top → high slab
            Floor highSlab = null;
            double minHighDiff = double.MaxValue;

            // Find slab whose top is closest to fold's bottom → low slab
            Floor lowSlab = null;
            double minLowDiff = double.MaxValue;

            foreach (var (slab, topElev) in nearbySlabs)
            {
                double diffToTop = Math.Abs(topElev - foldTop);
                double diffToBottom = Math.Abs(topElev - foldBottom);

                if (diffToTop < minHighDiff)
                {
                    minHighDiff = diffToTop;
                    highSlab = slab;
                }

                if (diffToBottom < minLowDiff)
                {
                    minLowDiff = diffToBottom;
                    lowSlab = slab;
                }
            }

            // Ensure high and low are different slabs
            if (highSlab != null && lowSlab != null && highSlab.Id == lowSlab.Id)
            {
                // Same slab matched both → pick the 2nd best for the worse match
                if (minHighDiff <= minLowDiff)
                {
                    // highSlab is better match for top, find next best for bottom
                    double nextBest = double.MaxValue;
                    foreach (var (slab, topElev) in nearbySlabs)
                    {
                        if (slab.Id == highSlab.Id) continue;
                        double diff = Math.Abs(topElev - foldBottom);
                        if (diff < nextBest) { nextBest = diff; lowSlab = slab; }
                    }
                }
                else
                {
                    // lowSlab is better match for bottom, find next best for top
                    double nextBest = double.MaxValue;
                    foreach (var (slab, topElev) in nearbySlabs)
                    {
                        if (slab.Id == lowSlab.Id) continue;
                        double diff = Math.Abs(topElev - foldTop);
                        if (diff < nextBest) { nextBest = diff; highSlab = slab; }
                    }
                }
            }

            return (highSlab, lowSlab);
        }

        /// <summary>
        /// Build a FoldCheckItem for a single fold floor.
        /// </summary>
        public static FoldCheckItem BuildFoldCheckItem(Document doc, Floor foldFloor, List<Floor> allNonFoldFloors)
        {
            var floorType = doc.GetElement(foldFloor.GetTypeId()) as FloorType;
            var level = doc.GetElement(foldFloor.LevelId) as Level;

            var item = new FoldCheckItem
            {
                FoldFloorId = foldFloor.Id,
                FoldTypeName = floorType?.Name ?? "Unknown",
                LevelName = level?.Name ?? "Unknown",
                FoldThickness = GetFloorThickness(doc, foldFloor),
            };

            var (highSlab, lowSlab) = FindAdjacentSlabs(doc, foldFloor, allNonFoldFloors);

            if (highSlab != null)
            {
                item.Slab1Thickness = GetFloorThickness(doc, highSlab);
                var highType = doc.GetElement(highSlab.GetTypeId()) as FloorType;
                item.Slab1TypeName = highType?.Name ?? "Unknown";
                item.HighSlabElevation = GetFloorTopElevation(doc, highSlab);
            }

            if (lowSlab != null)
            {
                item.Slab2Thickness = GetFloorThickness(doc, lowSlab);
                var lowType = doc.GetElement(lowSlab.GetTypeId()) as FloorType;
                item.Slab2TypeName = lowType?.Name ?? "Unknown";
                item.LowSlabElevation = GetFloorTopElevation(doc, lowSlab);
            }

            if (highSlab != null && lowSlab != null)
            {
                // Gap = bottom of high slab - top of low slab
                double highSlabBottom = GetFloorBottomElevation(doc, highSlab);
                double lowSlabTop = GetFloorTopElevation(doc, lowSlab);
                item.Gap = Math.Abs(highSlabBottom - lowSlabTop);

                item.CalculatedThickness = item.Slab1Thickness + item.Slab2Thickness + item.Gap;
                item.StepHeight = item.HighSlabElevation - item.LowSlabElevation;

                // Compare with a tolerance of 1mm
                item.Status = Math.Abs(item.FoldThickness - item.CalculatedThickness) < 1.0
                    ? "OK"
                    : "Mismatch";
            }
            else
            {
                item.CalculatedThickness = 0;
                item.StepHeight = 0;
                item.Status = "Missing Slabs";
            }

            return item;
        }

        /// <summary>
        /// Get all RINCO_AN_Step family instances in the current view.
        /// </summary>
        public static List<FamilyInstance> GetStepFamilies(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi =>
                {
                    var symbol = fi.Symbol;
                    return symbol != null &&
                           symbol.Name.IndexOf("RINCO_AN_Step", StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .ToList();
        }

        /// <summary>
        /// Get the location point of a family instance.
        /// </summary>
        public static XYZ GetFamilyLocation(FamilyInstance fi)
        {
            var loc = fi.Location as LocationPoint;
            if (loc != null) return loc.Point;

            // Fallback to bounding box center
            var bb = fi.get_BoundingBox(null);
            if (bb != null)
            {
                return (bb.Min + bb.Max) / 2.0;
            }

            return XYZ.Zero;
        }

        /// <summary>
        /// Find the nearest fold floor to a step family instance.
        /// </summary>
        public static Floor FindNearestFold(FamilyInstance stepFamily, List<Floor> foldFloors)
        {
            var stepLocation = GetFamilyLocation(stepFamily);
            if (stepLocation == null) return null;

            Floor nearest = null;
            double minDist = double.MaxValue;

            foreach (var fold in foldFloors)
            {
                var bb = fold.get_BoundingBox(null);
                if (bb == null) continue;

                var center = (bb.Min + bb.Max) / 2.0;
                // Compare in XY plane only
                double dist = Math.Sqrt(
                    Math.Pow(center.X - stepLocation.X, 2) +
                    Math.Pow(center.Y - stepLocation.Y, 2));

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = fold;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get the "RL STEP" text parameter value from a step family.
        /// This is what displays on the view (e.g. "30").
        /// </summary>
        public static string GetRLStepValue(FamilyInstance stepFamily)
        {
            var param = stepFamily.LookupParameter("RL STEP");
            if (param != null && param.HasValue && param.StorageType == StorageType.String)
            {
                return param.AsString() ?? "";
            }
            return "";
        }

        /// <summary>
        /// Get the "Height Offset From Level" parameter value from a step family (in mm).
        /// </summary>
        public static double GetStepOffsetValue(FamilyInstance stepFamily)
        {
            var param = stepFamily.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            if (param != null && param.HasValue && param.StorageType == StorageType.Double)
            {
                return param.AsDouble() * FeetToMm;
            }
            var namedParam = stepFamily.LookupParameter(StepParamName);
            if (namedParam != null && namedParam.HasValue && namedParam.StorageType == StorageType.Double)
            {
                return namedParam.AsDouble() * FeetToMm;
            }
            return 0;
        }

        /// <summary>
        /// Update BOTH parameters on a step family:
        /// 1. "RL STEP" (text) = step value as string (e.g. "20")
        /// 2. "Height Offset From Level" = negative step value (e.g. -20mm)
        /// </summary>
        public static bool SetStepValues(FamilyInstance stepFamily, double valueMm)
        {
            bool anySuccess = false;

            // 1. Update "RL STEP" text parameter (displayed value)
            var rlParam = stepFamily.LookupParameter("RL STEP");
            if (rlParam != null && !rlParam.IsReadOnly)
            {
                if (rlParam.StorageType == StorageType.String)
                {
                    rlParam.Set(valueMm.ToString("F0"));
                    anySuccess = true;
                }
                else if (rlParam.StorageType == StorageType.Double)
                {
                    rlParam.Set(valueMm / FeetToMm);
                    anySuccess = true;
                }
            }

            // 2. Update "Height Offset From Level" (placement constraint)
            var offsetParam = stepFamily.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            if (offsetParam == null || offsetParam.IsReadOnly)
            {
                offsetParam = stepFamily.LookupParameter(StepParamName);
            }
            if (offsetParam != null && !offsetParam.IsReadOnly && offsetParam.StorageType == StorageType.Double)
            {
                offsetParam.Set(-Math.Abs(valueMm) / FeetToMm);
                anySuccess = true;
            }

            return anySuccess;
        }

        /// <summary>
        /// Build a StepCheckItem for a single RINCO_AN_Step family instance.
        /// Finds all floors at the step's XY location, picks the 2 slabs with different elevations.
        /// Compares "RL STEP" text value with calculated step height.
        /// </summary>
        public static StepCheckItem BuildStepCheckItem(Document doc, FamilyInstance stepFamily,
            List<Floor> allFloors)
        {
            string currentRL = GetRLStepValue(stepFamily);
            double currentOffset = GetStepOffsetValue(stepFamily);

            var item = new StepCheckItem
            {
                StepFamilyId = stepFamily.Id,
                TypeName = stepFamily.Symbol?.Name ?? "Unknown",
                CurrentRLValue = currentRL,
                CurrentOffsetValue = currentOffset,
                ParameterName = "RL STEP",
            };

            // Get step family location (XY)
            var loc = stepFamily.Location as LocationPoint;
            if (loc == null)
            {
                item.FoldTypeName = "No Location";
                item.Status = "Sai";
                return item;
            }
            XYZ stepXY = loc.Point;

            // Find all floors whose bounding box contains the step's XY (with tolerance)
            double tol = 0.5; // 0.5 feet ≈ 150mm
            var floorsAtLocation = new List<(Floor floor, double topElevation)>();

            foreach (var floor in allFloors)
            {
                var bb = floor.get_BoundingBox(null);
                if (bb == null) continue;

                // Check if step XY is within the floor's bounding box (expanded)
                bool xInside = stepXY.X >= bb.Min.X - tol && stepXY.X <= bb.Max.X + tol;
                bool yInside = stepXY.Y >= bb.Min.Y - tol && stepXY.Y <= bb.Max.Y + tol;

                if (xInside && yInside)
                {
                    double topElev = GetFloorTopElevation(doc, floor);
                    floorsAtLocation.Add((floor, topElev));
                }
            }

            if (floorsAtLocation.Count < 2)
            {
                item.FoldTypeName = floorsAtLocation.Count == 1
                    ? (doc.GetElement(floorsAtLocation[0].floor.GetTypeId()) as FloorType)?.Name ?? "1 slab only"
                    : "No Slab Found";
                item.Status = "Sai";
                return item;
            }

            // Sort by elevation descending
            floorsAtLocation.Sort((a, b) => b.topElevation.CompareTo(a.topElevation));

            // Find the two closest floors with DIFFERENT elevations (step pair)
            Floor highSlab = null;
            Floor lowSlab = null;
            double minDiff = double.MaxValue;

            for (int i = 0; i < floorsAtLocation.Count; i++)
            {
                for (int j = i + 1; j < floorsAtLocation.Count; j++)
                {
                    double diff = Math.Abs(floorsAtLocation[i].topElevation - floorsAtLocation[j].topElevation);
                    if (diff > 1.0 && diff < minDiff) // At least 1mm difference, find smallest step
                    {
                        minDiff = diff;
                        highSlab = floorsAtLocation[i].floor;
                        lowSlab = floorsAtLocation[j].floor;
                    }
                }
            }

            if (highSlab != null && lowSlab != null)
            {
                double highTop = GetFloorTopElevation(doc, highSlab);
                double lowTop = GetFloorTopElevation(doc, lowSlab);
                item.CalculatedValue = highTop - lowTop;

                // Slab info for display
                var highType = doc.GetElement(highSlab.GetTypeId()) as FloorType;
                var lowType = doc.GetElement(lowSlab.GetTypeId()) as FloorType;
                var highOffParam = highSlab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                var lowOffParam = lowSlab.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
                double highOffMm = highOffParam != null ? highOffParam.AsDouble() * FeetToMm : 0;
                double lowOffMm = lowOffParam != null ? lowOffParam.AsDouble() * FeetToMm : 0;

                item.HighSlabInfo = $"{highType?.Name} ({highOffMm:F0})";
                item.LowSlabInfo = $"{lowType?.Name} ({lowOffMm:F0})";
                item.FoldTypeName = $"{floorsAtLocation.Count} slabs";
            }
            else
            {
                item.FoldTypeName = "Same Elevation";
            }

            // Compare: parse RL STEP text value and compare with calculated
            double currentNumeric = 0;
            double.TryParse(currentRL, out currentNumeric);

            bool isCalculatedValid = highSlab != null && lowSlab != null;

            if (isCalculatedValid)
            {
                item.Status = Math.Abs(currentNumeric - item.CalculatedValue) < 1.0 ? "OK" : "Sai";
            }
            else
            {
                item.Status = "Sai"; // Force Sai if it couldn't calculate correctly
            }

            return item;
        }

        /// <summary>
        /// Override element color in the active view (orange for wrong, green for updated).
        /// </summary>
        public static void SetElementColorOverride(Document doc, View view, ElementId elementId, Color color)
        {
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(color);

            // Try to find solid fill pattern for surface override
            var solidFillId = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill)?.Id;

            if (solidFillId != null)
            {
                ogs.SetSurfaceForegroundPatternId(solidFillId);
                ogs.SetSurfaceForegroundPatternColor(color);
            }

            view.SetElementOverrides(elementId, ogs);
        }

        /// <summary>
        /// Reset element override to default (no override).
        /// </summary>
        public static void ResetElementOverride(View view, ElementId elementId)
        {
            view.SetElementOverrides(elementId, new OverrideGraphicSettings());
        }
    }
}
