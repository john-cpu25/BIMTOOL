using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.OpeningFillRegion
{
    public static class OpeningFillRegionLogic
    {
        /// <summary>
        /// Thực hiện đục lỗ trên Filled Region theo hình dạng Shaft Opening.
        /// </summary>
        /// <returns>Số Filled Region xử lý thành công</returns>
        public static int Execute(
            Document doc,
            UIDocument uidoc,
            View activeView,
            List<Element> shaftOpenings,
            List<FilledRegion> filledRegions)
        {
            // 1. Thu thập tất cả boundary 2D của Shaft Openings
            List<CurveLoop> openingLoops = new List<CurveLoop>();
            foreach (Element shaft in shaftOpenings)
            {
                List<CurveLoop> loops = GetShaftBoundaryLoops(doc, shaft, activeView);
                openingLoops.AddRange(loops);
            }

            if (openingLoops.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không thể lấy boundary của Shaft Opening nào.");
                return 0;
            }

            int successCount = 0;

            using (Transaction t = new Transaction(doc, "Opening Fill Region - Đục lỗ"))
            {
                t.Start();

                foreach (FilledRegion fr in filledRegions)
                {
                    try
                    {
                        bool result = CutFilledRegionWithOpenings(doc, uidoc, activeView, fr, openingLoops);
                        if (result) successCount++;
                    }
                    catch
                    {
                        // Bỏ qua lỗi trên từng Filled Region, tiếp tục xử lý các cái khác
                    }
                }

                t.Commit();
            }

            return successCount;
        }

        /// <summary>
        /// Lấy boundary 2D (CurveLoop) của Shaft Opening trên plan view.
        /// Shaft Opening là element thuộc BuiltInCategory.OST_ShaftOpening.
        /// Ta lấy sketch boundary bằng cách tìm bottom face nằm ngang từ Solid geometry.
        /// </summary>
        private static List<CurveLoop> GetShaftBoundaryLoops(Document doc, Element shaft, View activeView)
        {
            List<CurveLoop> result = new List<CurveLoop>();

            // Lấy elevation của view hiện tại để project geometry
            double viewElevation = activeView.GenLevel != null ? activeView.GenLevel.Elevation : 0;

            Options geomOptions = new Options();
            geomOptions.ComputeReferences = true;
            geomOptions.View = activeView;

            GeometryElement geomElem = shaft.get_Geometry(geomOptions);
            if (geomElem == null) return result;

            foreach (GeometryObject geomObj in geomElem)
            {
                Solid solid = geomObj as Solid;
                if (solid == null || solid.Volume < 1e-9) continue;

                // Tìm face nằm ngang (bottom face) — normal hướng xuống (0, 0, -1)
                // hoặc hướng lên (0, 0, 1)
                foreach (Face face in solid.Faces)
                {
                    PlanarFace planarFace = face as PlanarFace;
                    if (planarFace == null) continue;

                    XYZ normal = planarFace.FaceNormal;
                    // Chọn face nằm ngang (normal gần song song với Z axis)
                    if (Math.Abs(normal.Z) < 0.9) continue;

                    // Ưu tiên bottom face (normal hướng xuống) 
                    // hoặc face gần nhất với view elevation
                    IList<CurveLoop> faceEdges = planarFace.GetEdgesAsCurveLoops();
                    foreach (CurveLoop loop in faceEdges)
                    {
                        // Project CurveLoop xuống elevation của view
                        CurveLoop projectedLoop = ProjectCurveLoopToElevation(loop, viewElevation);
                        if (projectedLoop != null && !projectedLoop.IsOpen())
                        {
                            result.Add(projectedLoop);
                        }
                    }

                    // Chỉ cần lấy 1 face nằm ngang là đủ (bottom hoặc top đều có cùng profile)
                    if (result.Count > 0) break;
                }

                if (result.Count > 0) break;
            }

            // Fallback: nếu không tìm được face ngang, thử lấy từ geometry instances
            if (result.Count == 0)
            {
                foreach (GeometryObject geomObj in geomElem)
                {
                    GeometryInstance geomInst = geomObj as GeometryInstance;
                    if (geomInst == null) continue;

                    GeometryElement symbolGeom = geomInst.GetSymbolGeometry();
                    if (symbolGeom == null) continue;

                    foreach (GeometryObject symObj in symbolGeom)
                    {
                        Solid solid = symObj as Solid;
                        if (solid == null || solid.Volume < 1e-9) continue;

                        foreach (Face face in solid.Faces)
                        {
                            PlanarFace planarFace = face as PlanarFace;
                            if (planarFace == null) continue;

                            XYZ normal = planarFace.FaceNormal;
                            if (Math.Abs(normal.Z) < 0.9) continue;

                            IList<CurveLoop> faceEdges = planarFace.GetEdgesAsCurveLoops();
                            foreach (CurveLoop loop in faceEdges)
                            {
                                CurveLoop projectedLoop = ProjectCurveLoopToElevation(loop, viewElevation);
                                if (projectedLoop != null && !projectedLoop.IsOpen())
                                {
                                    result.Add(projectedLoop);
                                }
                            }

                            if (result.Count > 0) break;
                        }

                        if (result.Count > 0) break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Project một CurveLoop xuống elevation (Z) cho trước.
        /// </summary>
        private static CurveLoop ProjectCurveLoopToElevation(CurveLoop sourceLoop, double elevation)
        {
            try
            {
                CurveLoop newLoop = new CurveLoop();

                foreach (Curve curve in sourceLoop)
                {
                    Curve projected = ProjectCurveToElevation(curve, elevation);
                    if (projected != null)
                    {
                        newLoop.Append(projected);
                    }
                }

                return newLoop;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Project một Curve xuống Z = elevation.
        /// </summary>
        private static Curve ProjectCurveToElevation(Curve curve, double elevation)
        {
            try
            {
                if (curve is Line line)
                {
                    XYZ p0 = new XYZ(line.GetEndPoint(0).X, line.GetEndPoint(0).Y, elevation);
                    XYZ p1 = new XYZ(line.GetEndPoint(1).X, line.GetEndPoint(1).Y, elevation);
                    if (p0.IsAlmostEqualTo(p1)) return null;
                    return Line.CreateBound(p0, p1);
                }
                else if (curve is Arc arc)
                {
                    XYZ center = new XYZ(arc.Center.X, arc.Center.Y, elevation);
                    XYZ p0 = new XYZ(arc.GetEndPoint(0).X, arc.GetEndPoint(0).Y, elevation);
                    XYZ p1 = new XYZ(arc.GetEndPoint(1).X, arc.GetEndPoint(1).Y, elevation);

                    // Tính midpoint trên arc để tạo 3-point arc
                    XYZ midParam = arc.Evaluate(0.5, true);
                    XYZ mid = new XYZ(midParam.X, midParam.Y, elevation);

                    return Arc.Create(p0, p1, mid);
                }
                else
                {
                    // Cho các loại curve phức tạp khác, tessellate rồi nối line
                    IList<XYZ> tessPoints = curve.Tessellate();
                    if (tessPoints.Count >= 2)
                    {
                        XYZ p0 = new XYZ(tessPoints[0].X, tessPoints[0].Y, elevation);
                        XYZ p1 = new XYZ(tessPoints[tessPoints.Count - 1].X, tessPoints[tessPoints.Count - 1].Y, elevation);
                        if (p0.IsAlmostEqualTo(p1)) return null;
                        return Line.CreateBound(p0, p1);
                    }
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Đục lỗ trên 1 Filled Region theo các opening loops.
        /// Xóa FR cũ, tạo FR mới với boundaries gốc + void loops.
        /// </summary>
        private static bool CutFilledRegionWithOpenings(
            Document doc,
            UIDocument uidoc,
            View activeView,
            FilledRegion fr,
            List<CurveLoop> openingLoops)
        {
            // Lưu lại thông tin type và override graphics
            ElementId typeId = fr.GetTypeId();
            ElementId viewId = activeView.Id;

            // Lấy override graphics hiện tại
            OverrideGraphicSettings overrides = activeView.GetElementOverrides(fr.Id);

            // Lấy boundaries hiện tại của Filled Region
            IList<CurveLoop> existingBoundaries = fr.GetBoundaries();
            if (existingBoundaries == null || existingBoundaries.Count == 0)
                return false;

            // Tạo list boundaries mới = boundaries cũ + opening loops
            List<CurveLoop> newBoundaries = new List<CurveLoop>();

            // Giữ lại tất cả boundaries hiện có
            foreach (CurveLoop loop in existingBoundaries)
            {
                newBoundaries.Add(loop);
            }

            // Lấy outer boundary (loop đầu tiên thường là outer)
            // Kiểm tra xem opening loop có nằm trong outer boundary không
            int addedHoles = 0;
            foreach (CurveLoop openingLoop in openingLoops)
            {
                // Kiểm tra opening loop có giao với filled region không
                if (IsLoopInsideOrIntersectingRegion(existingBoundaries, openingLoop))
                {
                    // Đảo hướng opening loop nếu cần (void loop phải ngược chiều outer loop)
                    CurveLoop holeLoop = EnsureCorrectWindingForHole(existingBoundaries[0], openingLoop);
                    newBoundaries.Add(holeLoop);
                    addedHoles++;
                }
            }

            if (addedHoles == 0)
                return false; // Không có opening nào nằm trong filled region

            // Xóa Filled Region cũ
            ElementId oldId = fr.Id;
            doc.Delete(oldId);

            // Tạo Filled Region mới với boundaries đã đục lỗ
            try
            {
                FilledRegion newFr = FilledRegion.Create(doc, typeId, viewId, newBoundaries);

                // Khôi phục override graphics
                if (overrides != null)
                {
                    activeView.SetElementOverrides(newFr.Id, overrides);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra xem một CurveLoop (opening) có nằm trong vùng Filled Region không.
        /// Đơn giản: kiểm tra xem centroid của opening có nằm trong outer boundary không.
        /// </summary>
        private static bool IsLoopInsideOrIntersectingRegion(IList<CurveLoop> regionBoundaries, CurveLoop openingLoop)
        {
            if (regionBoundaries == null || regionBoundaries.Count == 0) return false;

            // Lấy outer boundary (loop đầu tiên)
            CurveLoop outerLoop = regionBoundaries[0];

            // Tính centroid của opening loop
            XYZ centroid = GetLoopCentroid(openingLoop);
            if (centroid == null) return false;

            // Kiểm tra centroid có nằm trong outer boundary không bằng ray casting
            return IsPointInsideLoop(centroid, outerLoop);
        }

        /// <summary>
        /// Tính centroid (trọng tâm) của một CurveLoop.
        /// </summary>
        private static XYZ GetLoopCentroid(CurveLoop loop)
        {
            List<XYZ> points = new List<XYZ>();
            foreach (Curve curve in loop)
            {
                points.Add(curve.GetEndPoint(0));
            }

            if (points.Count == 0) return null;

            double x = points.Average(p => p.X);
            double y = points.Average(p => p.Y);
            double z = points.Average(p => p.Z);
            return new XYZ(x, y, z);
        }

        /// <summary>
        /// Kiểm tra một điểm có nằm trong CurveLoop (closed polygon) không.
        /// Sử dụng thuật toán ray casting.
        /// </summary>
        private static bool IsPointInsideLoop(XYZ point, CurveLoop loop)
        {
            // Thu thập tất cả vertices
            List<XYZ> vertices = new List<XYZ>();
            foreach (Curve curve in loop)
            {
                // Tessellate để handle cả arc
                IList<XYZ> tessPoints = curve.Tessellate();
                for (int i = 0; i < tessPoints.Count - 1; i++)
                {
                    vertices.Add(tessPoints[i]);
                }
            }

            if (vertices.Count < 3) return false;

            // Ray casting algorithm (2D, bỏ qua Z)
            int crossings = 0;
            double px = point.X;
            double py = point.Y;

            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                double yi = vertices[i].Y;
                double yj = vertices[j].Y;
                double xi = vertices[i].X;
                double xj = vertices[j].X;

                if ((yi <= py && yj > py) || (yj <= py && yi > py))
                {
                    double xIntersect = xi + (py - yi) / (yj - yi) * (xj - xi);
                    if (px < xIntersect)
                    {
                        crossings++;
                    }
                }
            }

            return (crossings % 2) != 0;
        }

        /// <summary>
        /// Đảm bảo opening loop có winding order ngược với outer loop.
        /// Nếu outer loop đi counterclockwise thì hole phải đi clockwise, và ngược lại.
        /// </summary>
        private static CurveLoop EnsureCorrectWindingForHole(CurveLoop outerLoop, CurveLoop holeLoop)
        {
            bool outerIsCCW = IsCounterClockwise(outerLoop);
            bool holeIsCCW = IsCounterClockwise(holeLoop);

            // Hole phải ngược hướng với outer
            if (outerIsCCW == holeIsCCW)
            {
                // Cần đảo hướng hole loop
                return ReverseCurveLoop(holeLoop);
            }

            return holeLoop;
        }

        /// <summary>
        /// Kiểm tra CurveLoop có đi counterclockwise không (trên mặt phẳng XY).
        /// Sử dụng shoelace formula.
        /// </summary>
        private static bool IsCounterClockwise(CurveLoop loop)
        {
            List<XYZ> points = new List<XYZ>();
            foreach (Curve curve in loop)
            {
                points.Add(curve.GetEndPoint(0));
            }

            if (points.Count < 3) return true;

            // Shoelace formula
            double sum = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                sum += (points[j].X - points[i].X) * (points[j].Y + points[i].Y);
            }

            return sum < 0; // Negative = counterclockwise trong hệ tọa độ standard
        }

        /// <summary>
        /// Đảo hướng CurveLoop (reverse).
        /// </summary>
        private static CurveLoop ReverseCurveLoop(CurveLoop loop)
        {
            List<Curve> curves = new List<Curve>();
            foreach (Curve curve in loop)
            {
                curves.Add(curve.CreateReversed());
            }

            curves.Reverse();

            CurveLoop reversed = new CurveLoop();
            foreach (Curve c in curves)
            {
                reversed.Append(c);
            }

            return reversed;
        }
    }
}
