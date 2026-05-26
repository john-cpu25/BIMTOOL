using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RincoNhan.Tools.RebarColumn.Models;

namespace RincoNhan.Tools.RebarColumn
{
    public class RebarColumnLogic
    {
        private Document _doc;
        private FamilyInstance _column;

        public RebarColumnLogic(Document doc, FamilyInstance column)
        {
            _doc = doc;
            _column = column;
        }

        public void CreateRebar(RebarConfig config)
        {
            if (_column == null) return;

            using (Transaction trans = new Transaction(_doc, "Create Column Rebar"))
            {
                trans.Start();

                // 1. Get Column dimensions and orientation
                BoundingBoxXYZ bbox = _column.get_BoundingBox(null);
                double height = bbox.Max.Z - bbox.Min.Z;
                
                // Try to get actual parameters for width and depth
                double b = 0, h = 0;
                Parameter pB = _column.Symbol.LookupParameter("b") ?? _column.Symbol.LookupParameter("Width") ?? _column.Symbol.LookupParameter("B");
                Parameter pH = _column.Symbol.LookupParameter("h") ?? _column.Symbol.LookupParameter("Depth") ?? _column.Symbol.LookupParameter("H");
                
                if (pB != null) b = pB.AsDouble();
                if (pH != null) h = pH.AsDouble();
                
                if (b == 0 || h == 0)
                {
                    // Fallback to bounding box if parameters not found
                    b = bbox.Max.X - bbox.Min.X;
                    h = bbox.Max.Y - bbox.Min.Y;
                }

                XYZ origin = (_column.Location as LocationPoint).Point;
                XYZ basisX = _column.HandOrientation;
                XYZ basisY = _column.FacingOrientation;
                XYZ basisZ = XYZ.BasisZ;

                double cover = config.Cover / 304.8; // mm to feet

                // 2. Find Rebar Types
                RebarBarType barType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(t => t.Name.Contains(config.MainDiameter.ToString())) ??
                    new FilteredElementCollector(_doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().FirstOrDefault();

                RebarBarType stirrupType = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(t => t.Name.Contains(config.StirrupDiameter.ToString())) ?? barType;

                if (barType == null) { trans.RollBack(); return; }

                double d_stirrup = stirrupType.BarNominalDiameter;
                double d_main = barType.BarNominalDiameter;

                // Stirrup dimensions: centerline to centerline. The outer edge sits exactly at the cover boundary.
                double b_stirrup = b - 2 * cover - d_stirrup;
                double h_stirrup = h - 2 * cover - d_stirrup;

                // Main vertical bar dimensions: centerline to centerline. Sit exactly inside the stirrup inner edge.
                double b_main = b - 2 * (cover + d_stirrup + 0.5 * d_main);
                double h_main = h - 2 * (cover + d_stirrup + 0.5 * d_main);

                List<Rebar> createdRebars = new List<Rebar>();

                // 3. Create Vertical Bars (Perimeter Only)
                double dx = 1.0 / (config.CountX - 1);
                double dy = 1.0 / (config.CountY - 1);

                for (int i = 0; i < config.CountX; i++)
                {
                    for (int j = 0; j < config.CountY; j++)
                    {
                        if (i == 0 || i == config.CountX - 1 || j == 0 || j == config.CountY - 1)
                        {
                            // Map local (0,1) to actual coordinates relative to origin
                            double lx = (i * dx - 0.5) * b_main;
                            double ly = (j * dy - 0.5) * h_main;

                            XYZ pBottom = origin + basisX * lx + basisY * ly;
                            XYZ pTop = pBottom + basisZ * height;

                            Line line = Line.CreateBound(pBottom, pTop);
                            IList<Curve> curves = new List<Curve> { line };

                            Rebar rebar = Rebar.CreateFromCurves(_doc, RebarStyle.Standard, barType, null, null, _column, basisX, curves, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
                            if (rebar != null)
                            {
                                createdRebars.Add(rebar);
                            }
                        }
                    }
                }

                // 4. Create Stirrups (2 or 3 Zones)
                if (config.IsSeismic)
                {
                    // 3 Zones: Bottom (1/4), Middle (1/2), Top (1/4)
                    double h1 = height / 4.0;
                    double h3 = height / 4.0;
                    double h2 = height - h1 - h3;

                    double s1 = config.Spacing1 / 304.8;
                    double s2 = config.Spacing2 / 304.8;
                    double s3 = config.Spacing3 / 304.8;

                    Rebar s1_bar = CreateStirrupZone(origin, basisX, basisY, basisZ, b_stirrup, h_stirrup, 0.05, h1, s1, stirrupType, config.SelectedStirrupShapeName); // Bottom
                    Rebar s2_bar = CreateStirrupZone(origin, basisX, basisY, basisZ, b_stirrup, h_stirrup, h1, h2, s2, stirrupType, config.SelectedStirrupShapeName);   // Middle
                    Rebar s3_bar = CreateStirrupZone(origin, basisX, basisY, basisZ, b_stirrup, h_stirrup, h1 + h2, h3 - 0.05, s3, stirrupType, config.SelectedStirrupShapeName); // Top

                    if (s1_bar != null) createdRebars.Add(s1_bar);
                    if (s2_bar != null) createdRebars.Add(s2_bar);
                    if (s3_bar != null) createdRebars.Add(s3_bar);
                }
                else
                {
                    // 2 Zones: Bottom (1/4), Rest (3/4)
                    double h1 = height / 4.0;
                    double h2 = height - h1;

                    double s1 = config.Spacing1 / 304.8;
                    double s2 = config.Spacing2 / 304.8;

                    Rebar s1_bar = CreateStirrupZone(origin, basisX, basisY, basisZ, b_stirrup, h_stirrup, 0.05, h1, s1, stirrupType, config.SelectedStirrupShapeName); // Bottom/End zone
                    Rebar s2_bar = CreateStirrupZone(origin, basisX, basisY, basisZ, b_stirrup, h_stirrup, h1, h2 - 0.05, s2, stirrupType, config.SelectedStirrupShapeName); // Middle/Rest zone

                    if (s1_bar != null) createdRebars.Add(s1_bar);
                    if (s2_bar != null) createdRebars.Add(s2_bar);
                }

                // 5. Make all created rebars solid and unobscured in 3D views
                IList<View3D> views3D = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => !v.IsTemplate)
                    .ToList();

                foreach (View3D view3D in views3D)
                {
                    try
                    {
                        if (view3D.DetailLevel != ViewDetailLevel.Fine)
                        {
                            view3D.DetailLevel = ViewDetailLevel.Fine;
                        }
                    }
                    catch
                    {
                        // Ignore if view detail level cannot be changed (e.g. read-only, template, or locked)
                    }
                }

                foreach (Rebar rebar in createdRebars)
                {
                    foreach (View3D view3D in views3D)
                    {
                        try
                        {
#if NET48
                            rebar.SetSolidInView(view3D, true);
#endif
                            rebar.SetUnobscuredInView(view3D, true);
                        }
                        catch
                        {
                            // Ignore if view is locked, read-only, or otherwise does not support visibility overrides
                        }
                    }
                }

                trans.Commit();
            }
        }

        private Rebar CreateStirrupZone(XYZ origin, XYZ basisX, XYZ basisY, XYZ basisZ, double b, double h, double startHeight, double zoneHeight, double spacing, RebarBarType type, string selectedShapeName)
        {
            if (zoneHeight <= 0 || spacing <= 0) return null;

            XYZ corner0 = origin + basisX * (-0.5 * b) + basisY * (-0.5 * h) + basisZ * startHeight;
            XYZ corner1 = corner0 + basisX * b;
            XYZ corner2 = corner1 + basisY * h;
            XYZ corner3 = corner2 - basisX * b;

            IList<Curve> curves = new List<Curve>
            {
                Line.CreateBound(corner0, corner1),
                Line.CreateBound(corner1, corner2),
                Line.CreateBound(corner2, corner3),
                Line.CreateBound(corner3, corner0)
            };

            Rebar stirrup = Rebar.CreateFromCurves(_doc, RebarStyle.StirrupTie, type, null, null, _column, basisZ, curves, RebarHookOrientation.Right, RebarHookOrientation.Right, true, true);
            
            if (stirrup != null && !string.IsNullOrEmpty(selectedShapeName))
            {
                RebarShape rebarShape = new FilteredElementCollector(_doc)
                    .OfClass(typeof(RebarShape))
                    .Cast<RebarShape>()
                    .FirstOrDefault(s => s.Name == selectedShapeName);

                if (rebarShape != null)
                {
                    try
                    {
                        RebarShapeDrivenAccessor shapeAccessor = stirrup.GetShapeDrivenAccessor();
                        shapeAccessor.SetRebarShapeId(rebarShape.Id);
                    }
                    catch
                    {
                        // Fallback gracefully if the selected shape is not compatible with a closed rectangular loop
                    }
                }
            }

            RebarShapeDrivenAccessor accessor = stirrup.GetShapeDrivenAccessor();
            int count = (int)(zoneHeight / spacing);
            if (count < 1) count = 1;
            
            accessor.SetLayoutAsMaximumSpacing(spacing, zoneHeight, true, true, true);
            return stirrup;
        }
    }
}
