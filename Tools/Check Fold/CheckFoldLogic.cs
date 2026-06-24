using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoModeling.Tools.CheckFold.Models;

namespace RincoModeling.Tools.CheckFold
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
        /// Check if a floor is sloped (has modified shape or slope arrow).
        /// </summary>
        public static bool IsFloorSloped(Document doc, Floor floor)
        {
            // 1. Check BoundingBox height vs Thickness
            try
            {
                var bb = floor.get_BoundingBox(null);
                if (bb != null)
                {
                    double bbHeightMm = (bb.Max.Z - bb.Min.Z) * FeetToMm;
                    double thicknessMm = GetFloorThickness(doc, floor);

                    // If bounding box is taller than thickness by more than 5mm, it's sloped
                    if (bbHeightMm - thicknessMm > 5.0)
                    {
                        return true;
                    }
                }
            }
            catch { }

            // 2. Fallback to SlabShapeEditor check
            try 
            {
#if REVIT2024_OR_GREATER
                var shapeEditor = floor.GetSlabShapeEditor();
#else
                var shapeEditor = floor.SlabShapeEditor;
#endif
                if (shapeEditor != null && shapeEditor.SlabShapeVertices != null)
                {
                    var vertices = shapeEditor.SlabShapeVertices;
                    if (vertices.Size > 0)
                    {
                        double firstZ = double.MaxValue;
                        foreach (Autodesk.Revit.DB.SlabShapeVertex v in vertices)
                        {
                            if (firstZ == double.MaxValue) firstZ = v.Position.Z;
                            else if (Math.Abs(v.Position.Z - firstZ) > 0.001) return true;
                        }
                    }
                }
            } 
            catch { }

            var slopeParam = floor.get_Parameter(BuiltInParameter.ROOF_SLOPE);
            if (slopeParam != null && Math.Abs(slopeParam.AsDouble()) > 0.0001) return true;

            return false;
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
                double slabBottom = topElev - GetFloorThickness(doc, slab);
                
                double diffToTop = Math.Abs(topElev - foldTop);
                double diffToBottom = Math.Min(Math.Abs(topElev - foldBottom), Math.Abs(slabBottom - foldBottom));

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
                        double slabBottom = topElev - GetFloorThickness(doc, slab);
                        double diff = Math.Min(Math.Abs(topElev - foldBottom), Math.Abs(slabBottom - foldBottom));
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
                           (symbol.Name.IndexOf("RINCO_AN_Step", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            symbol.FamilyName.IndexOf("RINCO_AN_Step", StringComparison.OrdinalIgnoreCase) >= 0);
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
        public static bool SetStepValues(FamilyInstance stepFamily, double valueMm, bool isVaries = false)
        {
            bool anySuccess = false;

            // 1. Update "RL STEP" text parameter (displayed value)
            var rlParam = stepFamily.LookupParameter("RL STEP");
            if (rlParam != null && !rlParam.IsReadOnly)
            {
                if (rlParam.StorageType == StorageType.String)
                {
                    rlParam.Set(isVaries ? "VARIES" : valueMm.ToString("F0"));
                    anySuccess = true;
                }
                else if (rlParam.StorageType == StorageType.Double && !isVaries)
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
                offsetParam.Set(isVaries ? 0 : -Math.Abs(valueMm) / FeetToMm);
                anySuccess = true;
            }

            return anySuccess;
        }

        private static List<XYZ> CreateOffsetPoints(XYZ pt, double offset)
        {
            return new List<XYZ>
            {
                pt,
                new XYZ(pt.X + offset, pt.Y, pt.Z),
                new XYZ(pt.X - offset, pt.Y, pt.Z),
                new XYZ(pt.X, pt.Y + offset, pt.Z),
                new XYZ(pt.X, pt.Y - offset, pt.Z)
            };
        }

        private static bool IsFloorContainingPoint(Floor floor, XYZ stepPoint)
        {
            double offset = 10.0 / 304.8;
            var testPoints = CreateOffsetPoints(stepPoint, offset);
            
            var topFaceRefs = HostObjectUtils.GetTopFaces(floor);
            foreach (var r in topFaceRefs)
            {
                var geomObj = floor.GetGeometryObjectFromReference(r);
                var face = geomObj as Face;
                if (face == null) continue;

                foreach (XYZ tpt in testPoints)
                {
                    IntersectionResult ir = face.Project(tpt);
                    if (ir == null) continue;

                    if (face.IsInside(ir.UVPoint))
                    {
                        return true;
                    }
                }
            }
            return false;
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
            double stepZMm = stepXY.Z * FeetToMm;

            var floorData = new List<(Floor floor, double topElev, List<Face> topFaces)>();
            foreach (var floor in allFloors)
            {
                var bb = floor.get_BoundingBox(null);
                if (bb == null) continue;

                if (stepXY.X < bb.Min.X - tol || stepXY.X > bb.Max.X + tol ||
                    stepXY.Y < bb.Min.Y - tol || stepXY.Y > bb.Max.Y + tol)
                    continue;

                double topElev = GetFloorTopElevation(doc, floor);
                
                // Filter floors to only those within a reasonable vertical distance (e.g., 5000mm)
                if (Math.Abs(topElev - stepZMm) > 5000.0) continue;

                var faces = new List<Face>();
                var topFaceRefs = HostObjectUtils.GetTopFaces(floor);
                foreach (var r in topFaceRefs)
                {
                    var geomObj = floor.GetGeometryObjectFromReference(r);
                    if (geomObj is Face f) faces.Add(f);
                }
                floorData.Add((floor, topElev, faces));
            }

            var testPoints = CreateOffsetPoints(stepXY, 10.0 / 304.8);
            var hitFloors = new Dictionary<double, Floor>();

            foreach (var data in floorData)
            {
                bool hit = false;
                foreach (var face in data.topFaces)
                {
                    foreach (var tpt in testPoints)
                    {
                        var ir = face.Project(tpt);
                        if (ir != null && face.IsInside(ir.UVPoint))
                        {
                            hit = true;
                            break;
                        }
                    }
                    if (hit) break;
                }

                if (hit)
                {
                    // Round to 1mm to group
                    double roundedElev = Math.Round(data.topElev, 0);
                    if (!hitFloors.ContainsKey(roundedElev))
                    {
                        hitFloors[roundedElev] = data.floor;
                    }
                }
            }

            item.AllSlabIds = hitFloors.Values.Select(f => f.Id).ToList();

            if (hitFloors.Count == 0)
            {
                item.FoldTypeName = "0 slabs";
                item.Status = "Sai";
                return item;
            }

            if (hitFloors.Count == 1)
            {
                item.FoldTypeName = "1 elevation only";
                item.Status = "Sai";
                return item;
            }

            var sortedElevs = hitFloors.Keys.OrderByDescending(k => k).ToList();
            
            // Lấy thằng có ELEVATION AT TOP CAO NHẤT VÀ THẤP NHẤT
            double elev1 = sortedElevs.First(); // Cao nhất
            double elev2 = sortedElevs.Last();  // Thấp nhất
            
            Floor highSlab = hitFloors[elev1];
            Floor lowSlab = hitFloors[elev2];

            if (highSlab != null && lowSlab != null)
            {
                item.HighSlabId = highSlab.Id;
                item.LowSlabId = lowSlab.Id;

                bool highSloped = IsFloorSloped(doc, highSlab);
                bool lowSloped = IsFloorSloped(doc, lowSlab);

                if (highSloped || lowSloped)
                {
                    item.IsVaries = true;
                    item.CalculatedValue = 0;
                    
                    var highType = doc.GetElement(highSlab.GetTypeId()) as FloorType;
                    var lowType = doc.GetElement(lowSlab.GetTypeId()) as FloorType;
                    item.HighSlabInfo = $"{highType?.Name} (Sloped)";
                    item.LowSlabInfo = $"{lowType?.Name} (Sloped)";
                    item.FoldTypeName = $"{hitFloors.Count} slabs";

                    item.Status = currentRL.Equals("VARIES", StringComparison.OrdinalIgnoreCase) ? "OK" : "Sai";
                }
                else
                {
                    double highTop = GetFloorTopElevation(doc, highSlab);
                    double lowTop = GetFloorTopElevation(doc, lowSlab);
                    item.CalculatedValue = highTop - lowTop;

                    var highType = doc.GetElement(highSlab.GetTypeId()) as FloorType;
                    var lowType = doc.GetElement(lowSlab.GetTypeId()) as FloorType;
                    item.HighSlabInfo = $"{highType?.Name} ({highTop:F0})";
                    item.LowSlabInfo = $"{lowType?.Name} ({lowTop:F0})";
                    item.FoldTypeName = $"{hitFloors.Count} slabs";

                    double currentNumeric = 0;
                    double.TryParse(currentRL, out currentNumeric);
                    item.Status = Math.Abs(currentNumeric - item.CalculatedValue) < 1.0 && !currentRL.Equals("VARIES", StringComparison.OrdinalIgnoreCase) ? "OK" : "Sai";
                }
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

        /// <summary>
        /// Analyze all floors in the view and find edges that represent steps but have no step family nearby.
        /// </summary>
        public static List<Models.MissingStepItem> FindMissing3DSteps(Document doc, View view)
        {
            var missingSteps = new List<Models.MissingStepItem>();
            var allFloors = GetNonFoldFloors(doc, view).ToList();
            var allStepFamilies = GetStepFamilies(doc, view).ToList();

            var floorGeometries = new Dictionary<ElementId, (Solid ActualSolid, List<Curve> TopEdges, double TopElev, List<Curve> FootprintEdges)>();

            foreach (var floor in allFloors)
            {
                var topFaces = HostObjectUtils.GetTopFaces(floor);
                if (topFaces.Count == 0) continue;

                var geomObj = floor.GetGeometryObjectFromReference(topFaces[0]) as Face;
                if (geomObj == null) continue;

                var edges = new List<Curve>();
                var footprintEdges = new List<Curve>();
                var loops = geomObj.GetEdgesAsCurveLoops();
                
                foreach (var loop in loops)
                {
                    foreach (var curve in loop)
                    {
                        edges.Add(curve);
                        // Flatten for touching check
                        var p1 = new XYZ(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, 0);
                        var p2 = new XYZ(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, 0);
                        try { footprintEdges.Add(Line.CreateBound(p1, p2)); } catch { }
                    }
                }

                Solid actualSolid = GetElementSolid(floor);
                double topElev = GetFloorTopElevation(doc, floor);
                floorGeometries[floor.Id] = (actualSolid, edges, topElev, footprintEdges);
            }

            int idCounter = 1;

            for (int i = 0; i < allFloors.Count; i++)
            {
                var highFloor = allFloors[i];
                var highBB = highFloor.get_BoundingBox(view);
                if (highBB == null || !floorGeometries.ContainsKey(highFloor.Id)) continue;

                var highData = floorGeometries[highFloor.Id];

                for (int j = 0; j < allFloors.Count; j++)
                {
                    if (i == j) continue;

                    var lowFloor = allFloors[j];
                    var lowBB = lowFloor.get_BoundingBox(view);
                    if (lowBB == null || !floorGeometries.ContainsKey(lowFloor.Id)) continue;

                    var lowData = floorGeometries[lowFloor.Id];

                    double diff = highData.TopElev - lowData.TopElev;
                    if (diff < 10.0 / FeetToMm) continue; // Minimum step height 10mm

                    // XY overlap check
                    bool xOverlap = highBB.Min.X <= lowBB.Max.X + 0.1 && highBB.Max.X >= lowBB.Min.X - 0.1;
                    bool yOverlap = highBB.Min.Y <= lowBB.Max.Y + 0.1 && highBB.Max.Y >= lowBB.Min.Y - 0.1;

                    if (xOverlap && yOverlap)
                    {
                        foreach (var edge in highData.TopEdges)
                        {
                            List<Curve> stepSegments = new List<Curve>();

                            // 1. Check overlap with low floor's solid
                            if (lowData.ActualSolid != null)
                            {
                                // Lower the edge to intersect with the low floor's solid
                                double targetZ = (lowBB.Min.Z + lowBB.Max.Z) / 2.0;
                                Curve loweredEdge = edge.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, targetZ - edge.GetEndPoint(0).Z)));
                                
                                try
                                {
                                    var sci = lowData.ActualSolid.IntersectWithCurve(loweredEdge, new SolidCurveIntersectionOptions());
                                    foreach (Curve seg in sci)
                                    {
                                        if (seg.Length > 0.5) // Minimum edge length 150mm
                                        {
                                            // Raise it back to original elevation for highlighting
                                            stepSegments.Add(seg.CreateTransformed(Transform.CreateTranslation(new XYZ(0, 0, edge.GetEndPoint(0).Z - targetZ))));
                                        }
                                    }
                                }
                                catch { }
                            }

                            // 2. Check touching boundaries (edge to edge)
                            if (stepSegments.Count == 0 && edge is Line lineA)
                            {
                                foreach (var lowEdge in lowData.FootprintEdges)
                                {
                                    var touchingSeg = GetTouchingSegment(lineA, lowEdge, 0.05); // 0.05 ft ~ 15mm tolerance
                                    if (touchingSeg != null && touchingSeg.Length > 0.5)
                                    {
                                        stepSegments.Add(touchingSeg);
                                    }
                                }
                            }

                            // Check if step segments are missing families
                            foreach (var seg in stepSegments)
                            {
                                bool hasStep = false;
                                
                                // Flatten the segment for 2D distance checking
                                var p1Seg = new XYZ(seg.GetEndPoint(0).X, seg.GetEndPoint(0).Y, 0);
                                var p2Seg = new XYZ(seg.GetEndPoint(1).X, seg.GetEndPoint(1).Y, 0);
                                Curve flatSeg = null;
                                try { flatSeg = Line.CreateBound(p1Seg, p2Seg); } catch { }

                                foreach (var stepFam in allStepFamilies)
                                {
                                    if (flatSeg == null) break;

                                    bool isClose = false;
                                    var stepLoc = stepFam.Location;

                                    if (stepLoc is LocationPoint lp)
                                    {
                                        var pt = new XYZ(lp.Point.X, lp.Point.Y, 0);
                                        isClose = flatSeg.Distance(pt) < 1.5; // ~450mm tolerance
                                    }
                                    else if (stepLoc is LocationCurve lc)
                                    {
                                        var pt1 = new XYZ(lc.Curve.GetEndPoint(0).X, lc.Curve.GetEndPoint(0).Y, 0);
                                        var pt2 = new XYZ(lc.Curve.GetEndPoint(1).X, lc.Curve.GetEndPoint(1).Y, 0);
                                        var ptMid = new XYZ(lc.Curve.Evaluate(0.5, true).X, lc.Curve.Evaluate(0.5, true).Y, 0);
                                        
                                        isClose = flatSeg.Distance(pt1) < 1.5 || 
                                                  flatSeg.Distance(pt2) < 1.5 || 
                                                  flatSeg.Distance(ptMid) < 1.5;
                                    }
                                    else
                                    {
                                        var loc = GetFamilyLocation(stepFam);
                                        if (loc != null && !loc.IsAlmostEqualTo(XYZ.Zero))
                                        {
                                            var pt = new XYZ(loc.X, loc.Y, 0);
                                            isClose = flatSeg.Distance(pt) < 1.5;
                                        }
                                    }

                                    if (isClose)
                                    {
                                        hasStep = true;
                                        break;
                                    }
                                }

                                if (!hasStep)
                                {
                                    var highType = doc.GetElement(highFloor.GetTypeId()) as FloorType;
                                    var lowType = doc.GetElement(lowFloor.GetTypeId()) as FloorType;
                                    missingSteps.Add(new Models.MissingStepItem
                                    {
                                        Id = idCounter++,
                                        HighSlabId = highFloor.Id,
                                        HighSlabInfo = $"{highType?.Name} ({highData.TopElev:F0})",
                                        LowSlabId = lowFloor.Id,
                                        LowSlabInfo = $"{lowType?.Name} ({lowData.TopElev:F0})",
                                        StepHeight = diff * FeetToMm,
                                        StepEdge = seg
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return missingSteps;
        }

        private static Curve GetTouchingSegment(Curve a, Curve b, double tolerance)
        {
            if (a is Line lineA && b is Line lineB)
            {
                XYZ pA1 = new XYZ(lineA.GetEndPoint(0).X, lineA.GetEndPoint(0).Y, 0);
                XYZ pA2 = new XYZ(lineA.GetEndPoint(1).X, lineA.GetEndPoint(1).Y, 0);
                
                Line flatA = null;
                try { flatA = Line.CreateBound(pA1, pA2); } catch { return null; }

                if (flatA.Direction.CrossProduct(lineB.Direction).GetLength() > 0.01) return null; // Not parallel
                
                if (flatA.Distance(lineB.GetEndPoint(0)) > tolerance) return null; // Not collinear

                double p1 = flatA.Project(lineB.GetEndPoint(0)).Parameter;
                double p2 = flatA.Project(lineB.GetEndPoint(1)).Parameter;
                
                double minB = Math.Min(p1, p2);
                double maxB = Math.Max(p1, p2);
                
                double minA = flatA.GetEndParameter(0);
                double maxA = flatA.GetEndParameter(1);
                
                double overlapMin = Math.Max(minA, minB);
                double overlapMax = Math.Min(maxA, maxB);
                
                if (overlapMax - overlapMin > 0.5) // at least 0.5 ft
                {
                    try { return Line.CreateBound(lineA.Evaluate(overlapMin, false), lineA.Evaluate(overlapMax, false)); } catch { }
                }
            }
            return null;
        }

        public static Solid GetElementSolid(Element element)
        {
            var options = new Options { DetailLevel = ViewDetailLevel.Fine };
            var geomElem = element.get_Geometry(options);
            if (geomElem != null)
            {
                foreach (var geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                    {
                        return solid;
                    }
                    if (geomObj is GeometryInstance inst)
                    {
                        var instGeom = inst.GetInstanceGeometry();
                        foreach (var instObj in instGeom)
                        {
                            if (instObj is Solid instSolid && instSolid.Faces.Size > 0 && instSolid.Volume > 0)
                            {
                                return instSolid;
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}

