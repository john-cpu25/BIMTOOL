using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public static class AutoDimMultiWallsLogic
    {
        public static void CreateDimensions(Document doc, UIDocument uidoc, AutoDimGridViewModel viewModel)
        {
            View view = doc.ActiveView;
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.CeilingPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.Section &&
                view.ViewType != ViewType.Elevation)
            {
                TaskDialog.Show("Lỗi", "Vui lòng sử dụng trên mặt bằng, mặt đứng hoặc mặt cắt.");
                return;
            }

            XYZ p1 = null;
            XYZ p2 = null;
            try
            {
                p1 = uidoc.Selection.PickPoint("Chọn điểm bắt đầu của đường thẳng cắt qua các tường");
                p2 = uidoc.Selection.PickPoint("Chọn điểm kết thúc");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            if (p1 == null || p2 == null || p1.DistanceTo(p2) < 0.1)
                return;

            using (Transaction t = new Transaction(doc, "Dim Multi Walls"))
            {
                t.Start();

                // Dịch đường line cắt vào bên trong tường một chút để đảm bảo cắt qua Solid
                XYZ offset = view.ViewDirection.Negate() * 3.28;
                XYZ p1Elevated = p1 + offset;
                XYZ p2Elevated = p2 + offset;
                Line cutLine = null;
                try { cutLine = Line.CreateBound(p1Elevated, p2Elevated); } catch { }
                if (cutLine == null) { t.RollBack(); return; }

                XYZ lineDir = (p2Elevated - p1Elevated).Normalize();
                XYZ dimDir = Math.Abs(lineDir.X) > Math.Abs(lineDir.Y) ? XYZ.BasisX : XYZ.BasisY;

                var walls = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .ToList();

                Options opt = new Options();
                opt.ComputeReferences = true;
                opt.IncludeNonVisibleObjects = true;

                HashSet<double> projections = new HashSet<double>();
                ReferenceArray refArray = new ReferenceArray();

                foreach (var wall in walls)
                {
                    if (wall.Width <= 0.1) continue; // Lọc bỏ tường vữa

                    GeometryElement geomElem = wall.get_Geometry(opt);
                    if (geomElem == null) continue;

                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is Solid solid)
                        {
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace pf)
                                {
                                    if (pf.FaceNormal.IsAlmostEqualTo(dimDir) || pf.FaceNormal.IsAlmostEqualTo(-dimDir))
                                    {
                                        if (face.Intersect(cutLine, out IntersectionResultArray ira) == SetComparisonResult.Overlap)
                                        {
                                            double proj = Math.Round(pf.Origin.DotProduct(dimDir), 4);
                                            if (!projections.Contains(proj))
                                            {
                                                projections.Add(proj);
                                                refArray.Append(pf.Reference);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (refArray.Size >= 2)
                {
                    Line dimLine = Line.CreateBound(p1, p1 + dimDir * 10);
                    Dimension dim = doc.Create.NewDimension(view, dimLine, refArray);
                    if (viewModel.SelectedDimensionType != null)
                    {
                        dim.DimensionType = viewModel.SelectedDimensionType;
                    }
                }
                else
                {
                    TaskDialog.Show("Thông báo", "Không tìm thấy mặt tường nào cắt qua đường vẽ hoặc không đủ 2 mặt để tạo Dim.");
                }

                t.Commit();
            }
        }

        public static void CreateDimensionsBySelection(Document doc, UIDocument uidoc, AutoDimGridViewModel viewModel)
        {
            View view = doc.ActiveView;
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.CeilingPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.Section &&
                view.ViewType != ViewType.Elevation)
            {
                TaskDialog.Show("Lỗi", "Vui lòng sử dụng trên mặt bằng, mặt đứng hoặc mặt cắt.");
                return;
            }

            IList<Reference> selectedRefs = null;
            try
            {
                selectedRefs = uidoc.Selection.PickObjects(ObjectType.Element, new WallSelectionFilter(), "Chọn các tường cần Dim (có thể quét chuột). Nhấn Finish khi xong.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            if (selectedRefs == null || selectedRefs.Count == 0) return;

            XYZ placePoint = null;
            try
            {
                placePoint = uidoc.Selection.PickPoint("Chọn vị trí đặt đường Dim");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return;
            }

            using (Transaction t = new Transaction(doc, "Dim Multi Walls by Selection"))
            {
                t.Start();

                List<Wall> validWalls = new List<Wall>();
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                foreach (var r in selectedRefs)
                {
                    Wall w = doc.GetElement(r) as Wall;
                    if (w != null && w.Location is LocationCurve lc && lc.Curve is Line)
                    {
                        // Lọc bỏ các tường vữa/hoàn thiện (độ dày <= 30mm ~ 0.1 feet)
                        if (w.Width <= 0.1) continue;

                        validWalls.Add(w);
                        BoundingBoxXYZ wBox = w.get_BoundingBox(view);
                        if (wBox != null)
                        {
                            minX = Math.Min(minX, wBox.Min.X);
                            minY = Math.Min(minY, wBox.Min.Y);
                            minZ = Math.Min(minZ, wBox.Min.Z);
                            maxX = Math.Max(maxX, wBox.Max.X);
                            maxY = Math.Max(maxY, wBox.Max.Y);
                            maxZ = Math.Max(maxZ, wBox.Max.Z);
                        }
                    }
                }

                if (validWalls.Count == 0)
                {
                    t.RollBack();
                    return;
                }

                XYZ center = new XYZ((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);

                XYZ dimDir = XYZ.BasisY;
                if (Math.Abs(placePoint.X - center.X) < Math.Abs(placePoint.Y - center.Y))
                {
                    dimDir = XYZ.BasisX;
                }

                ReferenceArray refArray = new ReferenceArray();
                HashSet<double> projections = new HashSet<double>();

                foreach (var wall in validWalls)
                {
                    Line l = (wall.Location as LocationCurve).Curve as Line;
                    if (l == null) continue;
                    XYZ wallDir = l.Direction.Normalize();

                    if (Math.Abs(wallDir.DotProduct(dimDir)) < 0.1)
                    {
                        IList<Reference> extFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior);
                        IList<Reference> intFaces = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);

                        foreach (var ext in extFaces)
                        {
                            GeometryObject geomObj = wall.GetGeometryObjectFromReference(ext);
                            if (geomObj is PlanarFace pf)
                            {
                                double proj = Math.Round(pf.Origin.DotProduct(dimDir), 4);
                                if (!projections.Contains(proj))
                                {
                                    projections.Add(proj);
                                    refArray.Append(ext);
                                }
                            }
                        }
                        foreach (var intF in intFaces)
                        {
                            GeometryObject geomObj = wall.GetGeometryObjectFromReference(intF);
                            if (geomObj is PlanarFace pf)
                            {
                                double proj = Math.Round(pf.Origin.DotProduct(dimDir), 4);
                                if (!projections.Contains(proj))
                                {
                                    projections.Add(proj);
                                    refArray.Append(intF);
                                }
                            }
                        }
                    }
                    else if (Math.Abs(wallDir.DotProduct(dimDir)) > 0.9)
                    {
                        var endRefs = GetAllWallEndAndOpeningReferences(wall);
                        foreach (var r in endRefs)
                        {
                            GeometryObject geomObj = wall.GetGeometryObjectFromReference(r);
                            if (geomObj is PlanarFace pf)
                            {
                                double proj = Math.Round(pf.Origin.DotProduct(dimDir), 4);
                                if (!projections.Contains(proj))
                                {
                                    projections.Add(proj);
                                    refArray.Append(r);
                                }
                            }
                        }
                    }
                }

                if (dimDir == null) dimDir = XYZ.BasisY;

                if (refArray.Size >= 2)
                {
                    Line dimLine = Line.CreateBound(placePoint, placePoint + dimDir * 10);
                    try
                    {
                        Dimension dim = doc.Create.NewDimension(view, dimLine, refArray);
                        if (viewModel.SelectedDimensionType != null)
                        {
                            dim.DimensionType = viewModel.SelectedDimensionType;
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Lỗi", "Không thể tạo Dim. Đảm bảo các tường song song với nhau.\n" + ex.Message);
                    }
                }
                else
                {
                    TaskDialog.Show("Thông báo", "Không đủ mặt tường để tạo Dim.");
                }

                t.Commit();
            }
        }
        public static double DistanceToUnboundedLine(Line line, XYZ point)
        {
            XYZ v = line.Direction.Normalize();
            XYZ w = point - line.GetEndPoint(0);
            double c1 = w.DotProduct(v);
            XYZ proj = line.GetEndPoint(0) + c1 * v;
            return point.DistanceTo(proj);
        }

        public static List<Reference> GetAllWallEndAndOpeningReferences(Wall wall)
        {
            var refs = new List<Reference>();
            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.IncludeNonVisibleObjects = true;

            GeometryElement geomElem = wall.get_Geometry(opt);
            if (geomElem == null) return refs;

            Line wallLine = (wall.Location as LocationCurve).Curve as Line;
            XYZ wallDir = wallLine.Direction.Normalize();

            HashSet<double> projs = new HashSet<double>();

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
                                double proj = Math.Round(pf.Origin.DotProduct(wallDir), 4);
                                if (!projs.Contains(proj))
                                {
                                    projs.Add(proj);
                                    refs.Add(pf.Reference);
                                }
                            }
                        }
                    }
                }
            }

            return refs;
        }
    }
}
