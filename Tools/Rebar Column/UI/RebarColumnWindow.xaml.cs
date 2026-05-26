using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RincoNhan.Tools.RebarColumn.ViewModels;

namespace RincoNhan.Tools.RebarColumn.UI
{
    public partial class RebarColumnWindow : Window
    {
        private RebarColumnViewModel _vm;

        // Shared colors
        private static readonly Brush ColFill = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        private static readonly Brush ColStroke = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private static readonly Brush CoverStroke = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        private static readonly Brush StirrupStroke = new SolidColorBrush(Color.FromRgb(71, 85, 105));
        private static readonly Brush RebarFill = new SolidColorBrush(Color.FromRgb(220, 38, 38));     // Red
        private static readonly Brush RebarStroke = new SolidColorBrush(Color.FromRgb(153, 27, 27));
        private static readonly Brush DimText = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        private static readonly Brush LinkStroke = new SolidColorBrush(Color.FromRgb(100, 116, 139));

        public RebarColumnWindow(RebarColumnViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = _vm;

            _vm.BarPositions.CollectionChanged += OnPreviewDataChanged;
            _vm.StirrupLines.CollectionChanged += OnPreviewDataChanged;

            Loaded += (s, e) => DrawAll();
        }

        private void OnPreviewDataChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DrawAll();
        }

        private void OnApplyToOtherFloorsClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ApplyToOtherFloorsWindow();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        private void DrawAll()
        {
            DrawCrossSection();
        }

        // ==================== CROSS SECTION ====================
        private void DrawCrossSection()
        {
            var c = CrossSectionCanvas;
            if (c == null) return;
            c.Children.Clear();

            double colW = _vm.ColumnWidth > 0 ? _vm.ColumnWidth : 400;
            double colD = _vm.ColumnDepth > 0 ? _vm.ColumnDepth : 400;
            double cover = _vm.Cover > 0 ? _vm.Cover : 30;

            double maxDim = Math.Max(colW, colD);
            double scale = 160 / maxDim;
            double dW = colW * scale;
            double dD = colD * scale;
            double cPx = cover * scale;
            double dsPx = _vm.StirrupDiameter * scale;
            double dmPx = _vm.MainDiameter * scale;

            c.Width = dW + 40;
            c.Height = dD + 40;
            double ox = 20, oy = 20;

            // Column outline
            AddRect(c, ox, oy, dW, dD, ColFill, ColStroke, 1.5);

            // Cover dashed
            AddDashedRect(c, ox + cPx, oy + cPx, dW - 2 * cPx, dD - 2 * cPx, CoverStroke);

            // Stirrup: centerline to centerline. Outer edge sits exactly at the cover boundary.
            double sw = dW - 2 * cPx - dsPx;
            double sh = dD - 2 * cPx - dsPx;
            double sx = ox + cPx + 0.5 * dsPx;
            double sy = oy + cPx + 0.5 * dsPx;
            
            // Draw stirrup with its actual scaled diameter
            AddRect(c, sx - 0.5 * dsPx, sy - 0.5 * dsPx, sw + dsPx, sh + dsPx, Brushes.Transparent, StirrupStroke, dsPx > 0 ? dsPx : 2.5);

            // Main vertical bar centers (sit exactly inside stirrup inner edges)
            double swMain = sw - dsPx - dmPx;
            double shMain = sh - dsPx - dmPx;
            double sxMain = sx + 0.5 * dsPx + 0.5 * dmPx;
            double syMain = sy + 0.5 * dsPx + 0.5 * dmPx;

            // Internal links
            foreach (var pts in _vm.StirrupLines)
            {
                if (pts.Count <= 2)
                {
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        AddLine(c,
                            sxMain + pts[i].X * swMain, syMain + pts[i].Y * shMain,
                            sxMain + pts[i + 1].X * swMain, syMain + pts[i + 1].Y * shMain,
                            LinkStroke, dsPx > 0 ? dsPx * 0.6 : 1.5);
                    }
                }
            }

            // Rebar dots (drawn inside the stirrup with actual scaled main diameter)
            double dotSize = Math.Max(6, dmPx);
            foreach (var pt in _vm.BarPositions)
            {
                double cx = sxMain + pt.X * swMain;
                double cy = syMain + pt.Y * shMain;
                AddDot(c, cx, cy, dotSize, RebarFill, RebarStroke);
            }

            // Dimension labels
            AddText(c, ox + dW / 2, oy + dD + 6, $"{colW:0}", 9, DimText, true);
            AddRotatedText(c, ox + dW + 8, oy + dD / 2, $"{colD:0}", 9, DimText);
        }

        // ==================== DRAWING HELPERS ====================
        private void AddRect(Canvas c, double x, double y, double w, double h, Brush fill, Brush stroke, double thickness)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = fill, Stroke = stroke, StrokeThickness = thickness };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }

        private void AddDashedRect(Canvas c, double x, double y, double w, double h, Brush stroke)
        {
            var r = new Rectangle
            {
                Width = w, Height = h, Fill = Brushes.Transparent,
                Stroke = stroke, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            c.Children.Add(r);
        }

        private void AddLine(Canvas c, double x1, double y1, double x2, double y2, Brush stroke, double thickness)
        {
            c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = thickness });
        }

        private void AddDot(Canvas c, double cx, double cy, double size, Brush fill, Brush stroke)
        {
            var e = new Ellipse { Width = size, Height = size, Fill = fill, Stroke = stroke, StrokeThickness = 1 };
            Canvas.SetLeft(e, cx - size / 2);
            Canvas.SetTop(e, cy - size / 2);
            c.Children.Add(e);
        }

        private void AddText(Canvas c, double x, double y, string text, double fontSize, Brush color, bool centerH, bool rightAlign = false)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = color
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (centerH)
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
            else if (rightAlign)
                Canvas.SetLeft(tb, x - tb.DesiredSize.Width);
            else
                Canvas.SetLeft(tb, x);

            Canvas.SetTop(tb, y - tb.DesiredSize.Height / 2);
            c.Children.Add(tb);
        }

        private void AddRotatedText(Canvas c, double x, double y, string text, double fontSize, Brush color)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = color,
                RenderTransform = new RotateTransform(90)
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y - tb.DesiredSize.Width / 2);
            c.Children.Add(tb);
        }
    }
}
