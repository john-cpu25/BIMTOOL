using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingSchedule.Models;

namespace RincoNhan.Tools.LoadingSchedule
{
    /// <summary>
    /// Renders the Loading Schedule legend table into a Revit Legend View.
    /// 
    /// Workflow:
    /// 1. Draw table borders (detail lines)
    /// 2. Draw header text using selected TextNoteType
    /// 3. For each row: create FilledRegion in column 2 (8×23mm)
    /// 4. Tag each FilledRegion using IndependentTag at columns 1, 3, 5, 6, 7
    ///    - Col 1: RINCO_LH_Loading Tag (N.o number)
    ///    - Col 3: RINCO_LH_Info Loading Tag - Name (Load Type)
    ///    - Col 5: RINCO_LH_Info Loading Tag - SDL
    ///    - Col 6: RINCO_LH_Info Loading Tag - LL (kPa)
    ///    - Col 7: RINCO_LH_Info Loading Tag - LL (kN)
    /// </summary>
    public class LoadingScheduleLegendRenderer
    {
        // Column widths in mm (from reference image)
        private static readonly double[] ColumnWidthsMm = { 10, 23, 52, 17, 33, 32, 32 };
        private const double HeaderRowHeightMm = 13.0;
        private const double DataRowHeightMm = 13.0;
        private const double HatchHeightMm = 8.0;
        private const double HatchWidthMm = 23.0;
        private const double MmToFeet = 1.0 / 304.8;

        // Column header texts
        private static readonly string[] HeaderTexts =
        {
            "N.o",
            "LOAD\nHATCH",
            "LOAD TYPE",
            "CASE",
            "SDL ALLOWANCE\n(kPa)",
            "LL ALLOWANCE\n(kPa)",
            "LL ALLOWANCE\n(kN)"
        };

        /// <summary>
        /// Renders the complete Loading Schedule legend table.
        /// </summary>
        public static List<ElementId> RenderLegend(
            Document doc,
            View view,
            List<LoadingScheduleItem> items,
            XYZ insertionPoint,
            ElementId headerTextTypeId,
            Action<string> log = null)
        {
            var createdElements = new List<ElementId>();

            // Convert dimensions to feet
            double[] colWidths = ColumnWidthsMm.Select(w => w * MmToFeet).ToArray();
            double headerRowHeight = HeaderRowHeightMm * MmToFeet;
            double dataRowHeight = DataRowHeightMm * MmToFeet;
            double hatchHeight = HatchHeightMm * MmToFeet;
            double totalWidth = colWidths.Sum();

            // Cumulative X positions for column edges
            double[] colX = new double[colWidths.Length + 1];
            colX[0] = 0;
            for (int i = 0; i < colWidths.Length; i++)
                colX[i + 1] = colX[i] + colWidths[i];

            double startX = insertionPoint.X;
            double startY = insertionPoint.Y;

            // ─── 0. Find title TextNoteType: RINCO_3.0_Arial N/T_ARW ──────
            ElementId titleTextTypeId = FindTextNoteType(doc, "RINCO_3.0_Arial N/T_ARW");
            log?.Invoke(titleTextTypeId != ElementId.InvalidElementId
                ? "✓ Found title TextNoteType: RINCO_3.0_Arial N/T_ARW"
                : "✗ Title TextNoteType not found");

            // ─── 1. Draw Title ─────────────────────────────────────────────
            double tableTopY = startY;
            if (titleTextTypeId != null && titleTextTypeId != ElementId.InvalidElementId)
            {
                double titleY = startY + 3.0 * MmToFeet;
                PlaceTextNote(doc, view, titleTextTypeId, "LOADING SCHEDULE",
                    startX + totalWidth / 2.0, titleY, totalWidth,
                    HorizontalTextAlignment.Center, createdElements);
                log?.Invoke("✓ Title placed");
            }

            // ─── 2. Get line style ────────────────────────────────────────
            GraphicsStyle thinLineStyle = GetThinLineStyle(doc);

            int totalRows = items.Count + 1; // +1 for header

            // ─── 3. Draw Table Grid ───────────────────────────────────────
            // Horizontal lines
            for (int r = 0; r <= totalRows; r++)
            {
                double y;
                if (r == 0)
                    y = tableTopY;
                else if (r == 1)
                    y = tableTopY - headerRowHeight;
                else
                    y = tableTopY - headerRowHeight - (r - 1) * dataRowHeight;

                XYZ p1 = new XYZ(startX, y, 0);
                XYZ p2 = new XYZ(startX + totalWidth, y, 0);

                try
                {
                    var line = doc.Create.NewDetailCurve(view, Line.CreateBound(p1, p2));
                    if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                    createdElements.Add(line.Id);
                }
                catch { }
            }

            // Vertical lines
            double tableBottomY = tableTopY - headerRowHeight - items.Count * dataRowHeight;
            for (int c = 0; c <= colWidths.Length; c++)
            {
                double x = startX + colX[c];
                XYZ p1 = new XYZ(x, tableTopY, 0);
                XYZ p2 = new XYZ(x, tableBottomY, 0);

                try
                {
                    var line = doc.Create.NewDetailCurve(view, Line.CreateBound(p1, p2));
                    if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                    createdElements.Add(line.Id);
                }
                catch { }
            }

            log?.Invoke($"✓ Table grid drawn ({totalRows + 1} h-lines, {colWidths.Length + 1} v-lines)");
            // ─── 4. Draw Header Row ────────────────────────────────────────
            double headerTopY = tableTopY;
            for (int c = 0; c < HeaderTexts.Length; c++)
            {
                double cellLeft = startX + colX[c];
                double cellRight = startX + colX[c + 1];
                double cellCenterX = (cellLeft + cellRight) / 2.0;
                double cellCenterY = headerTopY - headerRowHeight / 2.0;

                PlaceTextNote(doc, view, headerTextTypeId, HeaderTexts[c],
                    cellCenterX, cellCenterY, cellRight - cellLeft,
                    HorizontalTextAlignment.Center, createdElements);
            }

            log?.Invoke("✓ Header row text placed");
            // ─── 5. Find Tag Families (single scan for performance) ──────
            var allFamilySymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();

            var loadingTagTypeId = FindTagFromCache(allFamilySymbols, "RINCO_LH_Loading Tag", null);
            var infoTagName = FindTagFromCache(allFamilySymbols, "RINCO_LH_Info Loading Tag", "Name");
            var infoTagSdl = FindTagFromCache(allFamilySymbols, "RINCO_LH_Info Loading Tag", "SDL");
            var infoTagLlKpa = FindTagFromCache(allFamilySymbols, "RINCO_LH_Info Loading Tag", "LL (kPa)");
            var infoTagLlKn = FindTagFromCache(allFamilySymbols, "RINCO_LH_Info Loading Tag", "LL (kN)");

            log?.Invoke($"  RINCO_LH_Loading Tag: {(loadingTagTypeId != ElementId.InvalidElementId ? "✓ found" : "✗ not found")}");
            log?.Invoke($"  Info Tag - Name: {(infoTagName != ElementId.InvalidElementId ? "✓" : "✗")}  SDL: {(infoTagSdl != ElementId.InvalidElementId ? "✓" : "✗")}  LL(kPa): {(infoTagLlKpa != ElementId.InvalidElementId ? "✓" : "✗")}  LL(kN): {(infoTagLlKn != ElementId.InvalidElementId ? "✓" : "✗")}");

            // ─── 6. Draw Data Rows ─────────────────────────────────────────
            double dataStartY = tableTopY - headerRowHeight;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                double rowTopY = dataStartY - i * dataRowHeight;
                double rowBottomY = rowTopY - dataRowHeight;
                double rowCenterY = (rowTopY + rowBottomY) / 2.0;

                // ── Step 1: Create FilledRegion in Column 2 (LOAD HATCH) ──
                double hatchColLeft = startX + colX[1]; // column index 1 = LOAD HATCH
                double hatchColRight = startX + colX[2];
                double hatchMarginX = 0.5 * MmToFeet;
                double hatchMarginY = (dataRowHeight - hatchHeight) / 2.0; // center vertically

                double hLeft = hatchColLeft + hatchMarginX;
                double hRight = hatchColRight - hatchMarginX;
                double hTop = rowTopY - hatchMarginY;
                double hBottom = rowTopY - hatchMarginY - hatchHeight;

                ElementId filledRegionId = CreateFilledRegionInCell(
                    doc, view, item.FilledRegionTypeId,
                    hLeft, hTop, hRight, hBottom, createdElements);

                bool hatchOk = filledRegionId != null && filledRegionId != ElementId.InvalidElementId;
                log?.Invoke($"  Row {i + 1}/{items.Count}: \"{item.TypeName}\" → Hatch: {(hatchOk ? "✓" : "✗")}");

                // ── Step 2: Tag the FilledRegion ──
                if (hatchOk)
                {
                    Element filledRegionElem = doc.GetElement(filledRegionId);
                    int tagCount = 0;

                    // Set Mark parameter for the N.o tag to read
                    if (filledRegionElem != null)
                    {
                        SetParameterValue(filledRegionElem, BuiltInParameter.ALL_MODEL_MARK, item.Number.ToString());
                    }

                    Reference hostRef = new Reference(filledRegionElem);

                    // Tag Col 1: N.o (RINCO_LH_Loading Tag)
                    if (loadingTagTypeId != null && loadingTagTypeId != ElementId.InvalidElementId)
                    {
                        double col1CenterX = startX + (colX[0] + colX[1]) / 2.0;
                        if (CreateTag(doc, view, loadingTagTypeId, hostRef,
                            new XYZ(col1CenterX, rowCenterY, 0), createdElements))
                            tagCount++;
                    }

                    // Tag Col 3: LOAD TYPE — same approach as Col 5 (center of column)
                    if (infoTagName != null && infoTagName != ElementId.InvalidElementId)
                    {
                        double col3CenterX = startX + (colX[2] + colX[3]) / 2.0;
                        if (CreateTag(doc, view, infoTagName, hostRef,
                            new XYZ(col3CenterX, rowCenterY, 0), createdElements))
                            tagCount++;
                    }

                    // Tag Col 5: SDL ALLOWANCE
                    if (infoTagSdl != null && infoTagSdl != ElementId.InvalidElementId)
                    {
                        double col5CenterX = startX + (colX[4] + colX[5]) / 2.0;
                        if (CreateTag(doc, view, infoTagSdl, hostRef,
                            new XYZ(col5CenterX, rowCenterY, 0), createdElements))
                            tagCount++;
                    }

                    // Tag Col 6: LL ALLOWANCE kPa
                    if (infoTagLlKpa != null && infoTagLlKpa != ElementId.InvalidElementId)
                    {
                        double col6CenterX = startX + (colX[5] + colX[6]) / 2.0;
                        if (CreateTag(doc, view, infoTagLlKpa, hostRef,
                            new XYZ(col6CenterX, rowCenterY, 0), createdElements))
                            tagCount++;
                    }

                    // Tag Col 7: LL ALLOWANCE kN
                    if (infoTagLlKn != null && infoTagLlKn != ElementId.InvalidElementId)
                    {
                        double col7CenterX = startX + (colX[6] + colX[7]) / 2.0;
                        if (CreateTag(doc, view, infoTagLlKn, hostRef,
                            new XYZ(col7CenterX, rowCenterY, 0), createdElements))
                            tagCount++;
                    }

                    log?.Invoke($"           Tags: {tagCount}/5 placed");
                }
            }

            log?.Invoke($"✓ Rendering complete: {createdElements.Count} elements total");
            return createdElements;
        }

        #region Tag & Element Creation

        /// <summary>
        /// Creates an IndependentTag. Returns true if successful.
        /// </summary>
        private static bool CreateTag(Document doc, View view, ElementId tagTypeId,
            Reference hostRef, XYZ position, List<ElementId> createdElements)
        {
            try
            {
                IndependentTag tag = IndependentTag.Create(
                    doc,
                    tagTypeId,
                    view.Id,
                    hostRef,
                    false, // no leader
                    TagOrientation.Horizontal,
                    position);

                if (tag != null)
                {
                    tag.TagHeadPosition = position;
                    tag.HasLeader = false;
                    createdElements.Add(tag.Id);
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>
        /// Creates a FilledRegion rectangle and returns its ElementId.
        /// </summary>
        private static ElementId CreateFilledRegionInCell(Document doc, View view,
            ElementId filledRegionTypeId,
            double left, double top, double right, double bottom,
            List<ElementId> createdElements)
        {
            if (filledRegionTypeId == null || filledRegionTypeId == ElementId.InvalidElementId)
                return ElementId.InvalidElementId;

            try
            {
                var curveLoop = new CurveLoop();
                curveLoop.Append(Line.CreateBound(new XYZ(left, top, 0), new XYZ(right, top, 0)));
                curveLoop.Append(Line.CreateBound(new XYZ(right, top, 0), new XYZ(right, bottom, 0)));
                curveLoop.Append(Line.CreateBound(new XYZ(right, bottom, 0), new XYZ(left, bottom, 0)));
                curveLoop.Append(Line.CreateBound(new XYZ(left, bottom, 0), new XYZ(left, top, 0)));

                var loops = new List<CurveLoop> { curveLoop };
                var filledRegion = FilledRegion.Create(doc, filledRegionTypeId, view.Id, loops);

                if (filledRegion != null)
                {
                    createdElements.Add(filledRegion.Id);
                    return filledRegion.Id;
                }
            }
            catch { }

            return ElementId.InvalidElementId;
        }

        #endregion

        #region Lookup Helpers

        /// <summary>
        /// Finds a TextNoteType by name (partial match).
        /// </summary>
        private static ElementId FindTextNoteType(Document doc, string typeName)
        {
            var textType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name.Contains(typeName) ||
                                     typeName.Contains(t.Name));

            return textType?.Id ?? ElementId.InvalidElementId;
        }

        /// <summary>
        /// Finds a tag family type from a pre-loaded symbols list (avoids repeated FilteredElementCollector scans).
        /// </summary>
        private static ElementId FindTagFromCache(List<FamilySymbol> allSymbols, string familyName, string typeName)
        {
            var familySymbols = allSymbols
                .Where(fs => fs.FamilyName != null &&
                             fs.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!familySymbols.Any())
                return ElementId.InvalidElementId;

            FamilySymbol match;

            if (string.IsNullOrEmpty(typeName))
            {
                match = familySymbols.First();
            }
            else
            {
                match = familySymbols.FirstOrDefault(fs =>
                    fs.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    match = familySymbols.FirstOrDefault(fs => fs.Name.Contains(typeName));

                if (match == null)
                    match = familySymbols.First();
            }

            if (match != null)
            {
                try { if (!match.IsActive) match.Activate(); } catch { }
                return match.Id;
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Gets the "Thin Lines" graphic style if available.
        /// </summary>
        private static GraphicsStyle GetThinLineStyle(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .FirstOrDefault(g => g.GraphicsStyleCategory != null &&
                                     g.GraphicsStyleCategory.Name.Contains("Thin"));
        }

        #endregion

        #region Text & Parameter Helpers

        /// <summary>
        /// Places a TextNote at the specified position.
        /// </summary>
        private static void PlaceTextNote(Document doc, View view, ElementId textTypeId,
            string text, double x, double centerY, double width,
            HorizontalTextAlignment alignment, List<ElementId> createdElements)
        {
            if (string.IsNullOrEmpty(text) || textTypeId == null || textTypeId == ElementId.InvalidElementId)
                return;

            try
            {
                double textSizeFeet = 2.0 * MmToFeet;
                TextNoteType textType = doc.GetElement(textTypeId) as TextNoteType;
                if (textType != null)
                {
                    Parameter p = textType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                    if (p != null) textSizeFeet = p.AsDouble();
                }

                double anchorY = centerY + textSizeFeet / 2.0;
                double textWidth = Math.Max(width * 0.95, 0.01);

                TextNoteOptions options = new TextNoteOptions(textTypeId);
                options.HorizontalAlignment = alignment;

                double margin = 1.0 * MmToFeet;
                XYZ anchorPoint;
                if (alignment == HorizontalTextAlignment.Left)
                    anchorPoint = new XYZ(x + margin, anchorY, 0);
                else if (alignment == HorizontalTextAlignment.Right)
                    anchorPoint = new XYZ(x - margin, anchorY, 0);
                else
                    anchorPoint = new XYZ(x, anchorY, 0);

                TextNote tn = TextNote.Create(doc, view.Id, anchorPoint, textWidth, text, options);
                if (tn != null)
                    createdElements.Add(tn.Id);
            }
            catch { }
        }

        /// <summary>
        /// Sets a built-in parameter value on an element.
        /// </summary>
        private static void SetParameterValue(Element element, BuiltInParameter bip, string value)
        {
            try
            {
                Parameter param = element.get_Parameter(bip);
                if (param != null && !param.IsReadOnly)
                {
                    if (param.StorageType == StorageType.String)
                        param.Set(value);
                    else if (param.StorageType == StorageType.Integer && int.TryParse(value, out int intVal))
                        param.Set(intVal);
                }
            }
            catch { }
        }

        #endregion
    }
}
