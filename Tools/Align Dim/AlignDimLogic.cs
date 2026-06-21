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

            XYZ dimDir = dimLine.Direction;
            XYZ dimPoint = dimLine.Origin;
            XYZ gridOrigin = gridLine.Origin;

            View dimView = dim.View;
            if (dimView == null) dimView = doc.ActiveView;

            XYZ viewNormal = dimView.ViewDirection;
            XYZ moveDir = dimDir.CrossProduct(viewNormal).Normalize();

            XYZ vectorFromGridOriginToDim = dimPoint - gridOrigin;
            double currentSignedDistance = vectorFromGridOriginToDim.DotProduct(moveDir);

            double currentDistance = Math.Abs(currentSignedDistance);
            XYZ perpVectorFromGridToDim;

            if (currentDistance < 1e-6)
            {
                perpVectorFromGridToDim = moveDir;
            }
            else
            {
                perpVectorFromGridToDim = moveDir * Math.Sign(currentSignedDistance);
            }

            XYZ moveVector = perpVectorFromGridToDim * (targetDistanceFeet - currentDistance);

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

            XYZ gridDir = gridLine.Direction;
            XYZ sourceDimPoint = sourceDimLine.Origin;
            XYZ gridOrigin = gridLine.Origin;

            View sourceView = sourceDim.View;
            if (sourceView == null) sourceView = doc.ActiveView;
            XYZ sourceViewNormal = sourceView.ViewDirection;
            XYZ moveDir = sourceDimLine.Direction.CrossProduct(sourceViewNormal).Normalize();

            XYZ vectorFromGridToDim = sourceDimPoint - gridOrigin;
            double sourceSignedDist = vectorFromGridToDim.DotProduct(moveDir);
            double targetDistanceFeet = Math.Abs(sourceSignedDist);

            XYZ targetDirection;
            if (targetDistanceFeet < 1e-6)
                targetDirection = moveDir;
            else
                targetDirection = moveDir * Math.Sign(sourceSignedDist);

            using (Transaction trans = new Transaction(doc, "Match Dimension in Views"))
            {
                trans.Start();

                foreach (ElementId viewId in targetViewIds)
                {
                    View view = doc.GetElement(viewId) as View;
                    if (view == null) continue;
                    XYZ targetViewNormal = view.ViewDirection;

                    // Find all dimensions in the target view
                    var targetDims = new FilteredElementCollector(doc, viewId)
                        .OfClass(typeof(Dimension))
                        .Cast<Dimension>()
                        .Where(d => d.DimensionType.Id == sourceDim.DimensionType.Id)
                        .ToList();

                    Dimension closestDim = null;
                    double minDistanceDiff = double.MaxValue;
                    double closestTargetDist = 0;
                    XYZ closestTargetDir = null;

                    foreach (var targetDim in targetDims)
                    {
                        if (targetDim.Curve is Line targetDimLine)
                        {
                            // Check if parallel to grid
                            if (targetDimLine.Direction.IsAlmostEqualTo(gridDir) || targetDimLine.Direction.IsAlmostEqualTo(-gridDir))
                            {
                                XYZ tMoveDir = targetDimLine.Direction.CrossProduct(targetViewNormal).Normalize();
                                XYZ tDimPoint = targetDimLine.Origin;
                                XYZ tVector = tDimPoint - gridOrigin;
                                
                                double tSignedDist = tVector.DotProduct(tMoveDir);
                                double tDist = Math.Abs(tSignedDist);
                                
                                XYZ tDir;
                                if (tDist < 1e-6)
                                {
                                    tDir = targetDirection;
                                }
                                else
                                {
                                    tDir = tMoveDir * Math.Sign(tSignedDist);
                                }

                                if (tDist < 1e-6 || tDir.IsAlmostEqualTo(targetDirection))
                                {
                                    double diff = Math.Abs(tDist - targetDistanceFeet);
                                    if (diff < minDistanceDiff)
                                    {
                                        minDistanceDiff = diff;
                                        closestDim = targetDim;
                                        closestTargetDist = tDist;
                                        closestTargetDir = tDir;
                                    }
                                }
                            }
                        }
                    }

                    if (closestDim != null && closestTargetDir != null)
                    {
                        // It's parallel, on the same side, and closest to original position. We move it.
                        XYZ moveVector = closestTargetDir * (targetDistanceFeet - closestTargetDist);
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
