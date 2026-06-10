using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.LoadingSchedule.Services
{
    /// <summary>
    /// Parses a template Legend view to identify its row structure:
    /// header elements, one data-row template, vertical lines, and row height.
    ///
    /// Algorithm:
    /// 1. Collect all elements in the template view.
    /// 2. Find all unique Y-levels from horizontal detail lines → row boundaries.
    /// 3. Classify elements into header row vs data row by their Y-center.
    /// 4. Separate vertical lines (they span the full table height and are redrawn).
    /// </summary>
    public class LegendTemplateParser
    {
        // ── Results ───────────────────────────────────────────────────────
        /// <summary>Element IDs belonging to the header row (text, lines, etc.).</summary>
        public List<ElementId> HeaderElementIds { get; private set; } = new List<ElementId>();

        /// <summary>Element IDs belonging to one data-row template.</summary>
        public List<ElementId> DataRowElementIds { get; private set; } = new List<ElementId>();

        /// <summary>X-positions of vertical lines (to be redrawn at full table height).</summary>
        public List<double> VerticalLineXPositions { get; private set; } = new List<double>();

        /// <summary>The GraphicsStyle used by vertical lines in the template.</summary>
        public GraphicsStyle VerticalLineStyle { get; private set; }

        /// <summary>Y-coordinate of the very top of the table.</summary>
        public double TableTopY { get; private set; }

        /// <summary>Y-coordinate of the boundary between header and first data row.</summary>
        public double HeaderBottomY { get; private set; }

        /// <summary>Height of one data row (positive value, measured downwards).</summary>
        public double RowHeight { get; private set; }

        /// <summary>The total width of the table.</summary>
        public double TableWidth { get; private set; }

        /// <summary>X-coordinate of the left edge of the table.</summary>
        public double TableLeftX { get; private set; }

        /// <summary>Human-readable parse summary for UI status display.</summary>
        public string Summary { get; private set; } = "";

        /// <summary>Whether the parse succeeded.</summary>
        public bool IsValid { get; private set; }

        // ── Tolerance ─────────────────────────────────────────────────────
        private const double Tolerance = 0.001; // ~0.3mm in feet

        // ── Public API ────────────────────────────────────────────────────
        /// <summary>
        /// Parses the template legend view.
        /// Returns true if a valid header + data row structure was found.
        /// </summary>
        public bool Parse(Document doc, View templateView, Action<string> log = null)
        {
            IsValid = false;
            HeaderElementIds.Clear();
            DataRowElementIds.Clear();
            VerticalLineXPositions.Clear();

            if (templateView == null || templateView.ViewType != ViewType.Legend)
            {
                Summary = "Error: Template view is not a Legend view.";
                log?.Invoke(Summary);
                return false;
            }

            // ── Step 1: Collect all elements ──────────────────────────────
            var allElements = new FilteredElementCollector(doc, templateView.Id)
                .WhereElementIsNotElementType()
                .ToList();

            log?.Invoke($"Template \"{templateView.Name}\": {allElements.Count} elements total");

            if (allElements.Count == 0)
            {
                Summary = "Error: Template legend is empty.";
                log?.Invoke(Summary);
                return false;
            }

            // ── Step 2: Separate detail lines ─────────────────────────────
            var horizontalLines = new List<DetailCurveInfo>();
            var verticalLines = new List<DetailCurveInfo>();
            var nonLineElements = new List<Element>();

            foreach (var elem in allElements)
            {
                if (elem is DetailCurve dc)
                {
                    var curve = dc.GeometryCurve;
                    if (curve is Line line)
                    {
                        var p0 = line.GetEndPoint(0);
                        var p1 = line.GetEndPoint(1);

                        bool isHorizontal = Math.Abs(p0.Y - p1.Y) < Tolerance;
                        bool isVertical = Math.Abs(p0.X - p1.X) < Tolerance;

                        if (isHorizontal)
                        {
                            horizontalLines.Add(new DetailCurveInfo
                            {
                                Element = dc,
                                Y = p0.Y,
                                MinX = Math.Min(p0.X, p1.X),
                                MaxX = Math.Max(p0.X, p1.X)
                            });
                        }
                        else if (isVertical)
                        {
                            verticalLines.Add(new DetailCurveInfo
                            {
                                Element = dc,
                                X = p0.X,
                                MinY = Math.Min(p0.Y, p1.Y),
                                MaxY = Math.Max(p0.Y, p1.Y)
                            });
                        }
                        else
                        {
                            // Diagonal line — treat as non-line element
                            nonLineElements.Add(elem);
                        }
                    }
                    else
                    {
                        nonLineElements.Add(elem);
                    }
                }
                else
                {
                    nonLineElements.Add(elem);
                }
            }

            log?.Invoke($"  Horizontal lines: {horizontalLines.Count}, Vertical lines: {verticalLines.Count}, Other elements: {nonLineElements.Count}");

            // ── Step 3: Find row boundaries from horizontal lines ─────────
            // Get unique Y-levels sorted descending (top to bottom)
            var yLevels = horizontalLines
                .Select(h => h.Y)
                .Distinct(new DoubleComparer(Tolerance))
                .OrderByDescending(y => y)
                .ToList();

            log?.Invoke($"  Y-levels found: {yLevels.Count} ({string.Join(", ", yLevels.Select(y => $"{y:F4}"))})");

            if (yLevels.Count < 3)
            {
                Summary = $"Error: The selected Template Legend does not contain the required table structure. Please select a valid Loading Schedule template that has at least 3 horizontal lines (Header Top, Header Bottom, Data Row Bottom). Found {yLevels.Count} lines.";
                log?.Invoke(Summary);
                return false;
            }

            TableTopY = yLevels[0];          // Top of header
            HeaderBottomY = yLevels[1];      // Bottom of header = top of first data row
            double dataRowBottomY = yLevels[2]; // Bottom of first data row
            RowHeight = HeaderBottomY - dataRowBottomY; // Positive value

            if (RowHeight <= 0)
            {
                Summary = "Error: Invalid row height calculated.";
                log?.Invoke(Summary);
                return false;
            }

            log?.Invoke($"  TableTopY={TableTopY:F4}, HeaderBottomY={HeaderBottomY:F4}, RowHeight={RowHeight:F4} ft ({RowHeight * 304.8:F1} mm)");

            // ── Step 4: Determine table extents from horizontal lines ─────
            TableLeftX = horizontalLines.Min(h => h.MinX);
            double tableRightX = horizontalLines.Max(h => h.MaxX);
            TableWidth = tableRightX - TableLeftX;

            log?.Invoke($"  Table: X=[{TableLeftX:F4}, {tableRightX:F4}], Width={TableWidth:F4} ft ({TableWidth * 304.8:F1} mm)");

            // ── Step 5: Store vertical line X-positions ───────────────────
            var uniqueXPositions = verticalLines
                .Select(v => v.X)
                .Distinct(new DoubleComparer(Tolerance))
                .OrderBy(x => x)
                .ToList();

            VerticalLineXPositions = uniqueXPositions;

            // Capture the line style from the first vertical line
            if (verticalLines.Count > 0)
            {
                VerticalLineStyle = doc.GetElement(verticalLines[0].Element.LineStyle.Id) as GraphicsStyle;
            }

            log?.Invoke($"  Vertical line X-positions: {uniqueXPositions.Count}");

            // ── Step 6: Classify non-line elements into header vs data row ─
            foreach (var elem in nonLineElements)
            {
                double elemCenterY = GetElementCenterY(elem);

                if (elemCenterY > HeaderBottomY - Tolerance)
                {
                    // Above header-bottom boundary → header
                    HeaderElementIds.Add(elem.Id);
                }
                else if (elemCenterY > dataRowBottomY - Tolerance)
                {
                    // Between header-bottom and data-row-bottom → data row
                    DataRowElementIds.Add(elem.Id);
                }
                // Elements below data row bottom are ignored (extra rows in template)
            }

            // ── Step 7: Classify horizontal lines into header vs data row ─
            // Header horizontal lines: at TableTopY and HeaderBottomY
            // Data row horizontal lines: at HeaderBottomY (top) and dataRowBottomY (bottom)
            // We only include the BOTTOM line of each section to avoid duplication
            foreach (var hLine in horizontalLines)
            {
                if (Math.Abs(hLine.Y - TableTopY) < Tolerance)
                {
                    // Top border of table → header
                    HeaderElementIds.Add(hLine.Element.Id);
                }
                else if (Math.Abs(hLine.Y - HeaderBottomY) < Tolerance)
                {
                    // Shared border → put in header (data rows will each get a bottom line)
                    HeaderElementIds.Add(hLine.Element.Id);
                }
                else if (Math.Abs(hLine.Y - dataRowBottomY) < Tolerance)
                {
                    // Bottom of data row → data row template
                    DataRowElementIds.Add(hLine.Element.Id);
                }
                // Lines at other Y-levels are ignored
            }

            // Capture horizontal line style too
            if (VerticalLineStyle == null && horizontalLines.Count > 0)
            {
                VerticalLineStyle = doc.GetElement(horizontalLines[0].Element.LineStyle.Id) as GraphicsStyle;
            }

            log?.Invoke($"  Header elements: {HeaderElementIds.Count}");
            log?.Invoke($"  Data row elements: {DataRowElementIds.Count}");

            // Validate
            if (HeaderElementIds.Count == 0)
            {
                Summary = "Warning: No header elements found. Will proceed with data rows only.";
                log?.Invoke(Summary);
            }

            if (DataRowElementIds.Count == 0)
            {
                Summary = "Error: No data row elements found in template.";
                log?.Invoke(Summary);
                return false;
            }

            // Count element types in data row for info
            int frCount = 0, tagCount = 0, textCount = 0, lineCount = 0, otherCount = 0;
            foreach (var id in DataRowElementIds)
            {
                var e = doc.GetElement(id);
                if (e is FilledRegion) frCount++;
                else if (e is IndependentTag) tagCount++;
                else if (e is TextNote) textCount++;
                else if (e is DetailCurve) lineCount++;
                else otherCount++;
            }

            Summary = $"Template OK: Header={HeaderElementIds.Count} elements, " +
                       $"DataRow={DataRowElementIds.Count} (FR:{frCount}, Tag:{tagCount}, Text:{textCount}, Line:{lineCount}, Other:{otherCount}), " +
                       $"RowHeight={RowHeight * 304.8:F1}mm, VLines={VerticalLineXPositions.Count}";
            log?.Invoke($"✓ {Summary}");

            IsValid = true;
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Gets the approximate Y-center of an element using its bounding box or known position.
        /// </summary>
        private double GetElementCenterY(Element elem)
        {
            // Try bounding box first
            var bb = elem.get_BoundingBox(null);
            if (bb != null)
                return (bb.Min.Y + bb.Max.Y) / 2.0;

            // Fallback for TextNote
            if (elem is TextNote tn)
                return tn.Coord.Y;

            // Fallback for IndependentTag
            if (elem is IndependentTag tag)
                return tag.TagHeadPosition.Y;

            // Fallback for FilledRegion — use bounding box (should have worked above)
            return 0;
        }

        // ── Internal types ────────────────────────────────────────────────

        private class DetailCurveInfo
        {
            public DetailCurve Element;
            public double Y;   // for horizontal lines
            public double X;   // for vertical lines
            public double MinX, MaxX; // for horizontal lines
            public double MinY, MaxY; // for vertical lines
        }

        /// <summary>
        /// Compares doubles with a tolerance for use in LINQ Distinct().
        /// </summary>
        private class DoubleComparer : IEqualityComparer<double>
        {
            private readonly double _tolerance;
            public DoubleComparer(double tolerance) { _tolerance = tolerance; }

            public bool Equals(double x, double y) => Math.Abs(x - y) < _tolerance;

            public int GetHashCode(double obj)
            {
                // Round to tolerance-based buckets
                return Math.Round(obj / _tolerance).GetHashCode();
            }
        }
    }
}
