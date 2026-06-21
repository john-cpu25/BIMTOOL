using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public static class AutoDimGridLogic
    {
        public static void CreateDimensions(Document doc, View view, List<Grid> grids, XYZ pickPoint)
        {
            var groups = GroupGridsByDirection(grids);

            // 8mm trên bản in (1 feet = 304.8 mm)
            double offsetDistance = (8.0 / 304.8) * view.Scale;

            foreach (var group in groups)
            {
                if (group.Count < 2) continue;

                Line firstLine = group.First().Curve as Line;
                XYZ gridDir = firstLine.Direction.Normalize();

                // Hướng của đường Dim vuông góc với hướng của Grid và hướng nhìn của View
                XYZ viewDir = view.ViewDirection;
                XYZ dimDir = viewDir.CrossProduct(gridDir).Normalize();

                // Tính toán tọa độ trung tâm của nhóm Grid
                XYZ center = XYZ.Zero;
                foreach (var g in group)
                {
                    center += (g.Curve as Line).Origin;
                }
                center /= group.Count;

                // Xác định hướng đẩy ra ngoài của Dim tổng (ra xa so với trung tâm cụm Grid)
                double dot = (pickPoint - center).DotProduct(gridDir);
                XYZ offsetDir = dot >= 0 ? gridDir : -gridDir;

                // 1. Tạo Dim chi tiết
                ReferenceArray detailRefs = new ReferenceArray();
                foreach (var g in group)
                {
                    detailRefs.Append(new Reference(g));
                }

                Line detailDimLine = Line.CreateBound(pickPoint, pickPoint + dimDir * 10);
                Dimension detailDim = doc.Create.NewDimension(view, detailDimLine, detailRefs);

                // 2. Tạo Dim tổng
                // Sắp xếp các Grid theo thứ tự trên phương đường Dim để lấy đầu và cuối
                var sortedGroup = group.OrderBy(g => (g.Curve as Line).Origin.DotProduct(dimDir)).ToList();

                ReferenceArray overallRefs = new ReferenceArray();
                overallRefs.Append(new Reference(sortedGroup.First()));
                overallRefs.Append(new Reference(sortedGroup.Last()));

                XYZ overallPickPoint = pickPoint + offsetDir * offsetDistance;
                Line overallDimLine = Line.CreateBound(overallPickPoint, overallPickPoint + dimDir * 10);

                Dimension overallDim = doc.Create.NewDimension(view, overallDimLine, overallRefs);
            }
        }

        private static List<List<Grid>> GroupGridsByDirection(List<Grid> grids)
        {
            var groups = new List<List<Grid>>();

            foreach (var grid in grids)
            {
                Line line = grid.Curve as Line;
                if (line == null) continue;

                XYZ dir = line.Direction;
                bool added = false;

                foreach (var group in groups)
                {
                    Line firstLine = group.First().Curve as Line;
                    XYZ firstDir = firstLine.Direction;

                    if (dir.IsAlmostEqualTo(firstDir) || dir.IsAlmostEqualTo(-firstDir))
                    {
                        group.Add(grid);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    groups.Add(new List<Grid> { grid });
                }
            }

            return groups;
        }
    }
}
