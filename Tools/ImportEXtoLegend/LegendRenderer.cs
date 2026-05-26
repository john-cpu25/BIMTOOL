using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Tools.ImportEXtoLegend.Models;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    public class LegendRenderer
    {
        private static double ScalePtToFeet = 0.2 / 304.8; // User rule: 10 pt = 2mm => 1 pt = 0.2mm
        
        public static List<ElementId> RenderTable(Document doc, View view, ExcelTableData data, XYZ insertionPoint, double maxTableHeightFeet, ElementId targetTextTypeId, double rowSpacingMultiplier)
        {
            List<ElementId> createdElements = new List<ElementId>();

            TextNoteType textType = doc.GetElement(targetTextTypeId) as TextNoteType;
            double textSizeFeet = 2.0 / 304.8; // default 2mm
            if (textType != null)
            {
                Parameter p = textType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                if (p != null) textSizeFeet = p.AsDouble();
            }

            // Optional Line Style "Thin Lines"
            GraphicsStyle thinLineStyle = new FilteredElementCollector(doc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .FirstOrDefault(g => g.GraphicsStyleCategory.Name.Contains("Thin"));

            double startX = insertionPoint.X;
            double currentY = insertionPoint.Y;
            double currentHeight = 0;
            double columnWidthsTotal = 0;

            // Pre-calculate X coordinates for the first part
            var colX = new Dictionary<int, double>();
            double tempX = 0;
            for (int c = 1; c <= data.MaxCol; c++)
            {
                colX[c] = tempX;
                double w = data.ColumnWidths.ContainsKey(c) ? data.ColumnWidths[c] * ScalePtToFeet : 10 * 7.14 * ScalePtToFeet;
                tempX += w;
            }
            colX[data.MaxCol + 1] = tempX;
            columnWidthsTotal = tempX;

            var rowY = new Dictionary<int, double>();
            var partStartCoords = new List<XYZ>();
            partStartCoords.Add(new XYZ(startX, currentY, 0));

            // To handle splitting, we track which row starts at which offset
            var rowStartPart = new Dictionary<int, int>(); // row -> part index
            int currentPart = 0;

            // Compute Y coordinates
            rowY[1] = 0; // Relative to current part's top
            for (int r = 1; r <= data.MaxRow; r++)
            {
                double h = (data.RowHeights.ContainsKey(r) ? data.RowHeights[r] * ScalePtToFeet : 15 * ScalePtToFeet) * rowSpacingMultiplier;
                
                // Check if adding this row exceeds max table height, and it's not the first row of a part
                if (maxTableHeightFeet > 0 && currentHeight + h > maxTableHeightFeet && currentHeight > 0)
                {
                    // Split!
                    currentPart++;
                    startX += columnWidthsTotal + (10.0 / 304.8); // Add margin of 10mm
                    currentY = insertionPoint.Y;
                    partStartCoords.Add(new XYZ(startX, currentY, 0));
                    
                    currentHeight = 0;
                    rowY[r] = 0; // Reset relative Y for the new part
                }

                rowStartPart[r] = currentPart;
                currentHeight += h;
                rowY[r + 1] = -currentHeight;
            }

            // Draw cells
            foreach (var cell in data.Cells)
            {
                if (string.IsNullOrEmpty(cell.Value) && !cell.HasBottomBorder && !cell.HasTopBorder && !cell.HasLeftBorder && !cell.HasRightBorder)
                {
                    continue; // Empty cell to skip
                }

                int partIdx = rowStartPart[cell.Row];
                XYZ partOrigin = partStartCoords[partIdx];

                double minX = partOrigin.X + colX[cell.Column];
                double maxX = partOrigin.X + colX[cell.Column + cell.ColSpan];
                // Y coordinates are negative relative to origin
                double maxY = partOrigin.Y + rowY[cell.Row]; 
                double minY = partOrigin.Y + rowY[cell.Row + cell.RowSpan];

                // 1. Draw Borders
                if (cell.HasTopBorder || cell.HasBottomBorder || cell.HasLeftBorder || cell.HasRightBorder)
                {
                    // Just simple detail lines for boundaries. Optimization: this draws overlapping lines for adjacent cells.
                    Color bColor = new Color(cell.BorderColor.R, cell.BorderColor.G, cell.BorderColor.B);
                    OverrideGraphicSettings bOgs = new OverrideGraphicSettings();
                    bOgs.SetProjectionLineColor(bColor);

                    if (cell.HasTopBorder)
                    {
                        var line = doc.Create.NewDetailCurve(view, Line.CreateBound(new XYZ(minX, maxY, 0), new XYZ(maxX, maxY, 0)));
                        if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                        view.SetElementOverrides(line.Id, bOgs);
                        createdElements.Add(line.Id);
                    }
                    if (cell.HasBottomBorder)
                    {
                        var line = doc.Create.NewDetailCurve(view, Line.CreateBound(new XYZ(minX, minY, 0), new XYZ(maxX, minY, 0)));
                        if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                        view.SetElementOverrides(line.Id, bOgs);
                        createdElements.Add(line.Id);
                    }
                    if (cell.HasLeftBorder)
                    {
                        var line = doc.Create.NewDetailCurve(view, Line.CreateBound(new XYZ(minX, maxY, 0), new XYZ(minX, minY, 0)));
                        if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                        view.SetElementOverrides(line.Id, bOgs);
                        createdElements.Add(line.Id);
                    }
                    if (cell.HasRightBorder)
                    {
                        var line = doc.Create.NewDetailCurve(view, Line.CreateBound(new XYZ(maxX, maxY, 0), new XYZ(maxX, minY, 0)));
                        if (thinLineStyle != null) line.LineStyle = thinLineStyle;
                        view.SetElementOverrides(line.Id, bOgs);
                        createdElements.Add(line.Id);
                    }
                }

                // 2. Draw Text
                if (!string.IsNullOrEmpty(cell.Value))
                {
                    double textWidth = Math.Max((maxX - minX) * 0.95, 0.01);
                    XYZ textCenter = new XYZ(minX + (maxX - minX) / 2, minY + (maxY - minY) / 2, 0);

                    TextNoteOptions options = new TextNoteOptions(targetTextTypeId);
                    
                    options.HorizontalAlignment = cell.HorizontalAlignment switch
                    {
                        HorizontalAlignmentType.Center => HorizontalTextAlignment.Center,
                        HorizontalAlignmentType.Right => HorizontalTextAlignment.Right,
                        _ => HorizontalTextAlignment.Left
                    };

                    // Vertical centering logic: 
                    // Revit TextNote anchor is typically the Top-Left, Top-Center, or Top-Right of the text box.
                    // To center geometrically, we adjust the Anchor Point Y.
                    double margin = 1.0 / 304.8; // 1mm safety margin
                    double anchorY = textCenter.Y + (textSizeFeet / 2.0); // Default Middle

                    switch (cell.VerticalAlignment)
                    {
                        case VerticalAlignmentType.Top:
                            anchorY = maxY - margin;
                            break;
                        case VerticalAlignmentType.Bottom:
                            anchorY = minY + textSizeFeet + margin;
                            break;
                        case VerticalAlignmentType.Center:
                        default:
                            anchorY = (minY + maxY) / 2.0 + (textSizeFeet / 2.1); // 2.1 factor based on visual check for better optical centering
                            break;
                    }
                    
                    // Safety check: ensure anchorY is inside the cell
                    if (anchorY > maxY) anchorY = maxY - margin;
                    if (anchorY < minY + textSizeFeet) anchorY = minY + textSizeFeet + margin;
                    
                    XYZ anchorPoint = new XYZ(textCenter.X, anchorY, 0);
                    if (options.HorizontalAlignment == HorizontalTextAlignment.Left) 
                        anchorPoint = new XYZ(minX + margin, anchorY, 0);
                    else if (options.HorizontalAlignment == HorizontalTextAlignment.Right)
                        anchorPoint = new XYZ(maxX - margin, anchorY, 0);

                    try 
                    {
                        TextNote tn = TextNote.Create(doc, view.Id, anchorPoint, textWidth, cell.Value, options);
                        createdElements.Add(tn.Id);
                    }
                    catch { } // Ignore if text creation fails (e.g. text width too small)
                }
            }

            return createdElements;
        }

        private static TextNoteType GetOrCreateTextType(Document doc, TextNoteType baseType, ExcelCellData cell, Dictionary<string, TextNoteType> cache)
        {
            if (baseType == null) return null;

            double revitSize = cell.FontSizePt * ScalePtToFeet;
            string typeName = $"{cell.FontName}_{cell.FontSizePt}pt_{(cell.IsBold ? "B" : "")}{(cell.IsItalic ? "I" : "")}";
            if (typeName.Length > 100) typeName = typeName.Substring(0, 100);

            if (cache.ContainsKey(typeName)) return cache[typeName];

            // Search if it exists in doc
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(t => t.Name == typeName);

            if (existing != null)
            {
                cache[typeName] = existing;
                return existing;
            }

            // Create new
            try
            {
                TextNoteType newType = baseType.Duplicate(typeName) as TextNoteType;
                
                // Set parameters
                Parameter fontParam = newType.get_Parameter(BuiltInParameter.TEXT_FONT);
                if (fontParam != null && !fontParam.IsReadOnly) fontParam.Set(cell.FontName);

                Parameter sizeParam = newType.get_Parameter(BuiltInParameter.TEXT_SIZE);
                if (sizeParam != null && !sizeParam.IsReadOnly) sizeParam.Set(revitSize);

                Parameter boldParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD);
                if (boldParam != null && !boldParam.IsReadOnly) boldParam.Set(cell.IsBold ? 1 : 0);

                Parameter italicParam = newType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC);
                if (italicParam != null && !italicParam.IsReadOnly) italicParam.Set(cell.IsItalic ? 1 : 0);

                // Set background to transparent to not occlude fill regions
                Parameter bgParam = newType.get_Parameter(BuiltInParameter.TEXT_BACKGROUND);
                if (bgParam != null && !bgParam.IsReadOnly) bgParam.Set(1); // 1 = Transparent, 0 = Opaque

                cache[typeName] = newType;
                return newType;
            }
            catch
            {
                return baseType; // Fallback
            }
        }

        private static FilledRegionType GetOrCreateFilledRegionType(Document doc, FilledRegionType baseType, FillPatternElement solidPattern, System.Drawing.Color cellColor, Dictionary<System.Drawing.Color, FilledRegionType> cache)
        {
            if (baseType == null) return null;
            if (cache.ContainsKey(cellColor)) return cache[cellColor];

            string typeName = $"SolidFill_{cellColor.R}_{cellColor.G}_{cellColor.B}";

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(t => t.Name == typeName);

            if (existing != null)
            {
                cache[cellColor] = existing;
                return existing;
            }

            try
            {
                FilledRegionType newType = baseType.Duplicate(typeName) as FilledRegionType;
                if (solidPattern != null)
                {
                    // For Revit 2022+ the property is ForegroundPatternId
                    newType.ForegroundPatternId = solidPattern.Id;
                    newType.ForegroundPatternColor = new Color(cellColor.R, cellColor.G, cellColor.B);
                }
                cache[cellColor] = newType;
                return newType;
            }
            catch
            {
                return baseType;
            }
        }
    }
}
