using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.FillRegionManager
{
    public static class HatchRenderer
    {
        private static Dictionary<ElementId, ImageSource> _imageCache = new Dictionary<ElementId, ImageSource>();

        public static ImageSource GetHatchPreview(Document doc, FilledRegionType type)
        {
            // Simple caching by Type Id (Wait, if color changes we should cache by pattern+color)
            // But since Type is unique, we can cache by Type Id
            if (_imageCache.ContainsKey(type.Id))
            {
                return _imageCache[type.Id];
            }

            double width = 80;
            double height = 40;

            DrawingGroup drawingGroup = new DrawingGroup();
            drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, width, height));

            using (DrawingContext dc = drawingGroup.Open())
            {
                // Base White Background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                // Draw Background Pattern
                ElementId bgId = type.BackgroundPatternId;
                Autodesk.Revit.DB.Color bgColor = type.BackgroundPatternColor;
                if (bgId != ElementId.InvalidElementId && bgColor != null && bgColor.IsValid)
                {
                    System.Windows.Media.Color wpfBgColor = System.Windows.Media.Color.FromRgb(bgColor.Red, bgColor.Green, bgColor.Blue);
                    FillPatternElement fpeBg = doc.GetElement(bgId) as FillPatternElement;
                    if (fpeBg != null)
                    {
                        FillPattern fpBg = fpeBg.GetFillPattern();
                        if (fpBg.IsSolidFill)
                        {
                            dc.DrawRectangle(new SolidColorBrush(wpfBgColor), null, new Rect(0, 0, width, height));
                        }
                        else
                        {
                            DrawFillPattern(dc, fpBg, wpfBgColor, width, height);
                        }
                    }
                }

                // Foreground
                ElementId fgId = type.ForegroundPatternId;
                Autodesk.Revit.DB.Color fgColor = type.ForegroundPatternColor;
                System.Windows.Media.Color wpfFgColor = Colors.Black; // default
                if (fgColor != null && fgColor.IsValid)
                {
                    wpfFgColor = System.Windows.Media.Color.FromRgb(fgColor.Red, fgColor.Green, fgColor.Blue);
                }

                if (fgId != ElementId.InvalidElementId)
                {
                    FillPatternElement fpe = doc.GetElement(fgId) as FillPatternElement;
                    if (fpe != null)
                    {
                        FillPattern fp = fpe.GetFillPattern();
                        if (fp.IsSolidFill)
                        {
                            dc.DrawRectangle(new SolidColorBrush(wpfFgColor), null, new Rect(0, 0, width, height));
                        }
                        else
                        {
                            DrawFillPattern(dc, fp, wpfFgColor, width, height);
                        }
                    }
                }

                // Draw Border
                dc.DrawRectangle(null, new System.Windows.Media.Pen(Brushes.DarkGray, 1), new Rect(0, 0, width, height));
            }

            DrawingImage drawingImage = new DrawingImage(drawingGroup);
            drawingImage.Freeze();
            
            _imageCache[type.Id] = drawingImage;
            return drawingImage;
        }

        private static void DrawFillPattern(DrawingContext dc, FillPattern fp, System.Windows.Media.Color color, double width, double height)
        {
            double scale = 1500; // 1 Revit foot = 1500 WPF units (adjust for visual scale)
            System.Windows.Media.Pen pen = new System.Windows.Media.Pen(new SolidColorBrush(color), 1.0);
            
            IList<FillGrid> grids = fp.GetFillGrids();
            foreach (FillGrid grid in grids)
            {
                double angle = grid.Angle;
                double offset = grid.Offset * scale;
                double shift = grid.Shift * scale;
                
                UV origin = grid.Origin;
                double ox = origin.U * scale;
                double oy = origin.V * scale; // Invert Y maybe? WPF Y goes down

                IList<double> segments = grid.GetSegments();
                DashStyle dashStyle = null;
                if (segments != null && segments.Count > 0)
                {
                    DoubleCollection dashes = new DoubleCollection();
                    foreach (double seg in segments)
                    {
                        dashes.Add(Math.Abs(seg * scale));
                    }
                    dashStyle = new DashStyle(dashes, 0);
                }

                System.Windows.Media.Pen gridPen = new System.Windows.Media.Pen(new SolidColorBrush(color), 1.0);
                if (dashStyle != null)
                {
                    gridPen.DashStyle = dashStyle;
                }

                Vector dir = new Vector(Math.Cos(angle), Math.Sin(angle));
                Vector perp = new Vector(-Math.Sin(angle), Math.Cos(angle));

                // We want to draw enough lines to cover the bounding box
                // Bounding box center is (width/2, height/2)
                int minIndex = -50;
                int maxIndex = 50;
                
                if (offset < 0.001) offset = 1; // Prevent infinite loop

                for (int i = minIndex; i <= maxIndex; i++)
                {
                    double startX = ox + i * offset * perp.X + i * shift * dir.X;
                    double startY = oy + i * offset * perp.Y + i * shift * dir.Y;

                    // Draw a long line through startX, startY along dir
                    double length = 200; // Long enough to cover 80x40
                    
                    System.Windows.Point p1 = new System.Windows.Point(startX - dir.X * length, startY - dir.Y * length);
                    System.Windows.Point p2 = new System.Windows.Point(startX + dir.X * length, startY + dir.Y * length);

                    dc.DrawLine(gridPen, p1, p2);
                }
            }
        }
        
        public static void ClearCache()
        {
            _imageCache.Clear();
        }
    }
}
