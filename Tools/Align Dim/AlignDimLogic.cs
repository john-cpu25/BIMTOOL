using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.Align_Dim
{
    public static class AlignDimLogic
    {
        public static void AlignDimensionToGrid(Document doc, Dimension dim, Grid grid, double distanceMm)
        {
            if (dim == null || grid == null || doc == null) return;
            double targetDistanceFeet = distanceMm / 304.8;
            MoveDimension(doc, dim, grid, targetDistanceFeet);
        }

        private static void MoveDimension(Document doc, Dimension dim, Grid grid, double targetDistanceFeet)
        {
            Curve gridCurve = grid.Curve;
            if (!(gridCurve is Line gridLine))
                throw new Exception("Selected grid is not a straight line.");

            Curve dimCurve = dim.Curve;
            if (!(dimCurve is Line dimLine))
                throw new Exception("Selected dimension is not a linear dimension.");

            XYZ gridDir = gridLine.Direction;
            XYZ dimPoint = dimLine.Origin;
            XYZ gridOrigin = gridLine.Origin;
            XYZ vectorFromGridOriginToDim = dimPoint - gridOrigin;
            double projectionLength = vectorFromGridOriginToDim.DotProduct(gridDir);
            XYZ projectedPointOnGrid = gridOrigin + gridDir * projectionLength;

            XYZ perpVector = dimPoint - projectedPointOnGrid;
            
            double currentDistance = perpVector.GetLength();
            if (currentDistance < 1e-6)
            {
                perpVector = gridDir.CrossProduct(XYZ.BasisZ).Normalize();
            }
            else
            {
                perpVector = perpVector.Normalize();
            }

            XYZ moveVector = perpVector * (targetDistanceFeet - currentDistance);

            using (Transaction trans = new Transaction(doc, "Align Dimension"))
            {
                trans.Start();
                ElementTransformUtils.MoveElement(doc, dim.Id, moveVector);
                trans.Commit();
            }
        }

        public static void MatchDimensionsInViews(Document doc, Dimension sourceDim, Grid grid, List<ElementId> targetViewIds)
        {
            if (sourceDim == null || grid == null || doc == null || targetViewIds == null || targetViewIds.Count == 0) return;

            Curve gridCurve = grid.Curve;
            if (!(gridCurve is Line gridLine))
                throw new Exception("Selected grid is not a straight line.");

            Curve dimCurve = sourceDim.Curve;
            if (!(dimCurve is Line sourceDimLine))
                throw new Exception("Selected dimension is not a linear dimension.");

            // Calculate the target distance from the source dimension
            XYZ gridDir = gridLine.Direction;
            XYZ sourceDimPoint = sourceDimLine.Origin;
            XYZ gridOrigin = gridLine.Origin;
            XYZ vectorFromGridToDim = sourceDimPoint - gridOrigin;
            double projectionLength = vectorFromGridToDim.DotProduct(gridDir);
            XYZ projectedPoint = gridOrigin + gridDir * projectionLength;
            
            XYZ perpVector = sourceDimPoint - projectedPoint;
            double targetDistanceFeet = perpVector.GetLength();
            XYZ targetDirection = perpVector.Normalize();

            if (targetDistanceFeet < 1e-6)
                targetDirection = gridDir.CrossProduct(XYZ.BasisZ).Normalize();

            using (Transaction trans = new Transaction(doc, "Match Dimension in Views"))
            {
                trans.Start();

                foreach (ElementId viewId in targetViewIds)
                {
                    View view = doc.GetElement(viewId) as View;
                    if (view == null) continue;

                    // Find all dimensions in the target view
                    var targetDims = new FilteredElementCollector(doc, viewId)
                        .OfClass(typeof(Dimension))
                        .Cast<Dimension>()
                        .Where(d => d.DimensionType.Id == sourceDim.DimensionType.Id)
                        .ToList();

                    Dimension closestDim = null;
                    double minDistanceDiff = double.MaxValue;
                    double closestTargetDist = 0;

                    foreach (var targetDim in targetDims)
                    {
                        if (targetDim.Curve is Line targetDimLine)
                        {
                            // Check if parallel to grid
                            if (targetDimLine.Direction.IsAlmostEqualTo(gridDir) || targetDimLine.Direction.IsAlmostEqualTo(-gridDir))
                            {
                                // Check if on the same side and roughly in the same location
                                XYZ tDimPoint = targetDimLine.Origin;
                                XYZ vFromGrid = tDimPoint - gridOrigin;
                                double pLen = vFromGrid.DotProduct(gridDir);
                                XYZ pPoint = gridOrigin + gridDir * pLen;
                                
                                XYZ tPerpVector = tDimPoint - pPoint;
                                double tDist = tPerpVector.GetLength();
                                
                                if (tDist > 1e-6)
                                {
                                    XYZ tDir = tPerpVector.Normalize();
                                    if (tDir.IsAlmostEqualTo(targetDirection))
                                    {
                                        double diff = Math.Abs(tDist - targetDistanceFeet);
                                        if (diff < minDistanceDiff)
                                        {
                                            minDistanceDiff = diff;
                                            closestDim = targetDim;
                                            closestTargetDist = tDist;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (closestDim != null)
                    {
                        // It's parallel, on the same side, and closest to original position. We move it.
                        XYZ moveVector = targetDirection * (targetDistanceFeet - closestTargetDist);
                        try
                        {
                            ElementTransformUtils.MoveElement(doc, closestDim.Id, moveVector);
                        }
                        catch
                        {
                            // Some dims might be pinned or unmovable, skip them
                        }
                    }
                }

                trans.Commit();
            }
        }
    }
}
