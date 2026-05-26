using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.ElevationView
{
    public static class ElevationViewLogic
    {
        public const double MM_TO_FEET = 1.0 / 304.8;

        /// <summary>
        /// Updates the crop box of a view based on offsets from a base box.
        /// </summary>
        public static void UpdateCropBox(View view, BoundingBoxXYZ baseBox, double leftMm, double rightMm, double topMm, double bottomMm)
        {
            if (baseBox == null) return;
            BoundingBoxXYZ newBox = new BoundingBoxXYZ();
            newBox.Transform = baseBox.Transform;
            
            newBox.Min = new XYZ(baseBox.Min.X - leftMm * MM_TO_FEET, baseBox.Min.Y - bottomMm * MM_TO_FEET, baseBox.Min.Z);
            newBox.Max = new XYZ(baseBox.Max.X + rightMm * MM_TO_FEET, baseBox.Max.Y + topMm * MM_TO_FEET, baseBox.Max.Z);
            
            view.CropBox = newBox;
        }

        /// <summary>
        /// Aligns all levels in the view with custom head and tail offsets.
        /// </summary>
        public static void AlignLevels(View view, double headOffsetMm = 1620, double tailOffsetMm = 500)
        {
            Document doc = view.Document;
            BoundingBoxXYZ cropBox = view.CropBox;
            
            // Transform to model space
            Transform vTrans = cropBox.Transform;
            Transform invTrans = vTrans.Inverse;

            // Crop boundaries in local coordinates
            double leftX = cropBox.Min.X;
            double rightX = cropBox.Max.X;
            double headX = leftX - (headOffsetMm * MM_TO_FEET);
            double tailX = rightX + (tailOffsetMm * MM_TO_FEET);

            var levels = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            foreach (Level level in levels)
            {
                // 1. Ensure 2D (View Specific)
                level.SetDatumExtentType(Autodesk.Revit.DB.DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                level.SetDatumExtentType(Autodesk.Revit.DB.DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                // 2. Get current curve
                IList<Curve> curves = level.GetCurvesInView(DatumExtentType.ViewSpecific, view);
                if (curves.Count == 0) continue;

                Line line = curves[0] as Line;
                if (line == null) continue;

                // 3. Create new line in View Space
                XYZ p0 = line.GetEndPoint(0);
                XYZ vP0 = invTrans.OfPoint(p0);
                
                // We want the line to go from headX to tailX at the same local Y and Z
                XYZ vStart = new XYZ(headX, vP0.Y, vP0.Z);
                XYZ vEnd = new XYZ(tailX, vP0.Y, vP0.Z);

                // Transform back to model space
                Curve newCurve = Line.CreateBound(vTrans.OfPoint(vStart), vTrans.OfPoint(vEnd));
                
                try 
                {
                    level.SetCurveInView(DatumExtentType.ViewSpecific, view, newCurve);
                    
                    // 4. Handle Bubbles
                    // End0 in the created line is at vStart (Left/Head)
                    level.ShowBubbleInView(Autodesk.Revit.DB.DatumEnds.End0, view);
                    level.HideBubbleInView(Autodesk.Revit.DB.DatumEnds.End1, view);
                }
                catch { /* Skip levels that can't be modified */ }
            }
        }

        /// <summary>
        /// Calculates the combined min and max X coordinates for multiple walls.
        /// </summary>
        public static (double minX, double maxX) GetWallsBoundariesInView(System.Collections.Generic.IEnumerable<Wall> walls, View view)
        {
            double globalMinX = double.MaxValue;
            double globalMaxX = double.MinValue;

            foreach (var wall in walls)
            {
                var (minX, maxX) = GetWallBoundariesInView(wall, view);
                globalMinX = Math.Min(globalMinX, minX);
                globalMaxX = Math.Max(globalMaxX, maxX);
            }

            if (globalMinX == double.MaxValue)
            {
                BoundingBoxXYZ viewCrop = view.CropBox;
                return (viewCrop.Min.X, viewCrop.Max.X);
            }

            return (globalMinX, globalMaxX);
        }

        /// <summary>
        /// Calculates the min and max X coordinates of a wall in the view's local coordinate system.
        /// Uses the wall's LocationCurve for reliable results.
        /// </summary>
        public static (double minX, double maxX) GetWallBoundariesInView(Wall wall, View view)
        {
            BoundingBoxXYZ viewCrop = view.CropBox;
            Transform invTrans = viewCrop.Transform.Inverse;

            // Use LocationCurve - most reliable way to get wall extents
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve != null)
            {
                Curve curve = locCurve.Curve;
                XYZ startPt = curve.GetEndPoint(0);
                XYZ endPt = curve.GetEndPoint(1);

                // Transform to the current view's local coordinate system
                XYZ localStart = invTrans.OfPoint(startPt);
                XYZ localEnd = invTrans.OfPoint(endPt);

                double minX = Math.Min(localStart.X, localEnd.X);
                double maxX = Math.Max(localStart.X, localEnd.X);

                return (minX, maxX);
            }

            return (viewCrop.Min.X, viewCrop.Max.X);
        }

        /// <summary>
        /// Updates the crop box based on reference wall boundaries and user offsets.
        /// Uses current view's transform but baseBox's Y values to avoid cumulative errors.
        /// </summary>
        public static void UpdateCropByReference(View view, BoundingBoxXYZ baseBox, double wallLeft, double wallRight, 
            double offLeft, double offRight, double offTop, double offBottom)
        {
            BoundingBoxXYZ crop = view.CropBox; // Use current to avoid jumping
            
            // New boundaries in view space
            // For X: use wall bounds
            // For Y: use INITIAL crop bounds (baseBox) to avoid cumulative shrinking/growing
            crop.Min = new XYZ(wallLeft - (offLeft * MM_TO_FEET), baseBox.Min.Y - (offBottom * MM_TO_FEET), crop.Min.Z);
            crop.Max = new XYZ(wallRight + (offRight * MM_TO_FEET), baseBox.Max.Y + (offTop * MM_TO_FEET), crop.Max.Z);

            view.CropBox = crop;
        }
    }
}
