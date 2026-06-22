using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public static class AutoDimWallLogic
    {
        public static void CreateDimensions(Document doc, View view, Wall wall, XYZ pickPoint, AutoDimGridViewModel viewModel)
        {
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null || !(locCurve.Curve is Line))
            {
                Autodesk.Revit.UI.TaskDialog.Show("Error", "Chỉ hỗ trợ tường thẳng.");
                return;
            }

            Line wallLine = locCurve.Curve as Line;
            XYZ wallDir = wallLine.Direction.Normalize();
            XYZ viewDir = view.ViewDirection;
            XYZ dimDir = viewDir.CrossProduct(wallDir).Normalize();

            XYZ center = (wallLine.GetEndPoint(0) + wallLine.GetEndPoint(1)) / 2.0;
            
            double dot = (pickPoint - center).DotProduct(dimDir);
            XYZ offsetDir = dot >= 0 ? dimDir : -dimDir;

            double scale = view.Scale;
            double actualOffset = 0;
            if (viewModel.DistanceToNearestDim > 0)
            {
                double distFeet = (viewModel.DistanceToNearestDim / 304.8) * scale;
                actualOffset = distFeet;
            }
            else
            {
                actualOffset = Math.Abs(dot);
            }

            XYZ detailDimOrigin = center + offsetDir * actualOffset;
            Line detailDimLine = Line.CreateBound(detailDimOrigin, detailDimOrigin + wallDir * 10);

            // 1. Get Wall Location Bounds
            double proj1 = wallLine.GetEndPoint(0).DotProduct(wallDir);
            double proj2 = wallLine.GetEndPoint(1).DotProduct(wallDir);
            double locMinProj = Math.Min(proj1, proj2);
            double locMaxProj = Math.Max(proj1, proj2);
            
            // 2. Get Intersecting Grids
            var gridRefsList = new List<Tuple<double, Reference>>();
            var grids = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .ToList();

            XYZ p1 = wallLine.GetEndPoint(0);
            XYZ p2 = wallLine.GetEndPoint(1);
            p1 = new XYZ(p1.X, p1.Y, 0);
            p2 = new XYZ(p2.X, p2.Y, 0);
            Line wL_unbound = null;
            try { 
                wL_unbound = Line.CreateBound(p1, p2); 
                wL_unbound.MakeUnbound();
            } catch { }

            if (wL_unbound != null)
            {
                foreach (var grid in grids)
                {
                    if (grid.Curve is Line gridLine)
                    {
                        XYZ gridDir = gridLine.Direction.Normalize();
                        if (!gridDir.IsAlmostEqualTo(wallDir) && !gridDir.IsAlmostEqualTo(-wallDir))
                        {
                            XYZ p3 = gridLine.GetEndPoint(0);
                            XYZ p4 = gridLine.GetEndPoint(1);
                            p3 = new XYZ(p3.X, p3.Y, 0);
                            p4 = new XYZ(p4.X, p4.Y, 0);
                            try
                            {
                                Line gL = Line.CreateBound(p3, p4);
                                gL.MakeUnbound(); // Quan trọng: Phải là Unbound để grid ngắn vẫn cắt được tường

                                if (wL_unbound.Intersect(gL, out IntersectionResultArray ira) == SetComparisonResult.Overlap)
                                {
                                    XYZ intersectionPt = ira.get_Item(0).XYZPoint;
                                    double proj = intersectionPt.DotProduct(wallDir);
                                    
                                    // Bắt các lưới trục nằm trong khoảng LocationCurve, mở rộng 3.5 feet (~1 mét)
                                    // để bao gồm cả các trục đi qua 2 đầu tường
                                    if (proj >= locMinProj - 3.5 && proj <= locMaxProj + 3.5)
                                    {
                                        gridRefsList.Add(new Tuple<double, Reference>(proj, new Reference(grid)));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }

            // 3. Determine Bounds (Wall Ends or Grids)
            Reference leftBound = null;
            Reference rightBound = null;

            if (gridRefsList.Count >= 2)
            {
                var sortedGrids = gridRefsList.OrderBy(x => x.Item1).ToList();
                leftBound = sortedGrids.First().Item2;
                rightBound = sortedGrids.Last().Item2;
            }
            else
            {
                var wallEndsList = GetWallEndReferencesWithProj(wall);
                if (wallEndsList.Count >= 2)
                {
                    var sortedEnds = wallEndsList.OrderBy(x => x.Item1).ToList();
                    leftBound = sortedEnds.First().Item2;
                    rightBound = sortedEnds.Last().Item2;
                    double leftProj = sortedEnds.First().Item1;
                    double rightProj = sortedEnds.Last().Item1;

                    if (gridRefsList.Count == 1)
                    {
                        double gridProj = gridRefsList[0].Item1;
                        if (Math.Abs(gridProj - leftProj) < Math.Abs(gridProj - rightProj))
                        {
                            leftBound = gridRefsList[0].Item2;
                        }
                        else
                        {
                            rightBound = gridRefsList[0].Item2;
                        }
                    }
                }
            }

            ReferenceArray overallRefs = new ReferenceArray();
            ReferenceArray detailRefs = new ReferenceArray();
            ReferenceArray gridRefs = new ReferenceArray();

            if (leftBound != null && rightBound != null)
            {
                overallRefs.Append(leftBound);
                overallRefs.Append(rightBound);

                detailRefs.Append(leftBound);
                detailRefs.Append(rightBound);
            }

            foreach (var g in gridRefsList)
            {
                gridRefs.Append(g.Item2);
            }

            // Hosted instances
            var hostedInstances = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Host != null && fi.Host.Id == wall.Id)
                .ToList();

            foreach (var instance in hostedInstances)
            {
                if (viewModel.DimOpenings)
                {
                    try
                    {
                        var leftRefs = instance.GetReferences(FamilyInstanceReferenceType.Left);
                        var rightRefs = instance.GetReferences(FamilyInstanceReferenceType.Right);
                        bool hasLeftRight = false;

                        if (leftRefs != null && leftRefs.Count > 0) 
                        {
                            detailRefs.Append(leftRefs.First());
                            hasLeftRight = true;
                        }
                        if (rightRefs != null && rightRefs.Count > 0) 
                        {
                            detailRefs.Append(rightRefs.First());
                            hasLeftRight = true;
                        }

                        if (!hasLeftRight)
                        {
                            var centerRefs = instance.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                            if (centerRefs != null && centerRefs.Count > 0) detailRefs.Append(centerRefs.First());
                        }
                    }
                    catch
                    {
                        try
                        {
                            var centerRefs = instance.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                            if (centerRefs != null && centerRefs.Count > 0) detailRefs.Append(centerRefs.First());
                        }
                        catch { }
                    }
                }
                else
                {
                    try
                    {
                        var centerRefs = instance.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                        if (centerRefs != null && centerRefs.Count > 0) detailRefs.Append(centerRefs.First());
                    }
                    catch { }
                }
            }

            // Create dimensions
            if (detailRefs.Size >= 2)
            {
                Dimension detailDim = doc.Create.NewDimension(view, detailDimLine, detailRefs);
                if (viewModel.SelectedDimensionType != null)
                {
                    detailDim.DimensionType = viewModel.SelectedDimensionType;
                }
            }

            int currentLayerIndex = 1;
            double distBetweenFeet = (viewModel.DistanceBetweenDims / 304.8) * scale;

            if (gridRefs.Size >= 2) 
            {
                XYZ gridDimOrigin = detailDimOrigin + offsetDir * distBetweenFeet * currentLayerIndex;
                Line gridDimLine = Line.CreateBound(gridDimOrigin, gridDimOrigin + wallDir * 10);
                
                Dimension gridDim = doc.Create.NewDimension(view, gridDimLine, gridRefs);
                if (viewModel.SelectedDimensionType != null)
                {
                    gridDim.DimensionType = viewModel.SelectedDimensionType;
                }
                currentLayerIndex++;
            }

            if (viewModel.DimOverall && overallRefs.Size >= 2)
            {
                XYZ overallDimOrigin = detailDimOrigin + offsetDir * distBetweenFeet * currentLayerIndex;
                Line overallDimLine = Line.CreateBound(overallDimOrigin, overallDimOrigin + wallDir * 10);
                
                Dimension overallDim = doc.Create.NewDimension(view, overallDimLine, overallRefs);
                if (viewModel.SelectedDimensionType != null)
                {
                    overallDim.DimensionType = viewModel.SelectedDimensionType;
                }
            }
        }

        private static List<Tuple<double, Reference>> GetWallEndReferencesWithProj(Wall wall)
        {
            var refs = new List<Tuple<double, Reference>>();
            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.IncludeNonVisibleObjects = true;

            GeometryElement geomElem = wall.get_Geometry(opt);
            if (geomElem == null) return refs;

            Line wallLine = (wall.Location as LocationCurve).Curve as Line;
            XYZ wallDir = wallLine.Direction.Normalize();

            var faceRefs = new List<Tuple<double, Reference>>();
            var edgeRefs = new List<Tuple<double, Reference>>();

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf)
                        {
                            if (pf.FaceNormal.IsAlmostEqualTo(wallDir) || pf.FaceNormal.IsAlmostEqualTo(-wallDir))
                            {
                                double proj = pf.Origin.DotProduct(wallDir);
                                faceRefs.Add(new Tuple<double, Reference>(proj, pf.Reference));
                            }
                        }
                    }
                    foreach (Edge edge in solid.Edges)
                    {
                        if (edge.AsCurve() is Line line)
                        {
                            XYZ dir = line.Direction.Normalize();
                            if (dir.IsAlmostEqualTo(XYZ.BasisZ) || dir.IsAlmostEqualTo(-XYZ.BasisZ))
                            {
                                double proj = line.Origin.DotProduct(wallDir);
                                if (edge.Reference != null)
                                {
                                    edgeRefs.Add(new Tuple<double, Reference>(proj, edge.Reference));
                                }
                            }
                        }
                    }
                }
            }

            var allRefs = new List<Tuple<double, Reference>>();
            allRefs.AddRange(faceRefs);
            allRefs.AddRange(edgeRefs);

            if (allRefs.Count >= 2)
            {
                double minProj = allRefs.Min(x => x.Item1);
                double maxProj = allRefs.Max(x => x.Item1);

                // For minProj, prefer face
                var minFace = faceRefs.Where(x => Math.Abs(x.Item1 - minProj) < 1e-4).FirstOrDefault();
                var minEdge = edgeRefs.Where(x => Math.Abs(x.Item1 - minProj) < 1e-4).FirstOrDefault();
                if (minFace != null) refs.Add(minFace);
                else if (minEdge != null) refs.Add(minEdge);

                // For maxProj, prefer face
                var maxFace = faceRefs.Where(x => Math.Abs(x.Item1 - maxProj) < 1e-4).FirstOrDefault();
                var maxEdge = edgeRefs.Where(x => Math.Abs(x.Item1 - maxProj) < 1e-4).FirstOrDefault();
                if (maxFace != null) refs.Add(maxFace);
                else if (maxEdge != null) refs.Add(maxEdge);
            }

            return refs;
        }
    }
}
