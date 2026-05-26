using System;
using System.Linq;
using System.Drawing;
using System.IO;
using ClosedXML.Excel;
using RincoNhan.Tools.ImportEXtoLegend.Models;
using System.Collections.Generic;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    public class ExcelReader
    {
        public static List<string> GetSheetNames(string filePath)
        {
            var sheets = new List<string>();
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var wb = new XLWorkbook(fs))
                    {
                        foreach (var ws in wb.Worksheets)
                        {
                            sheets.Add(ws.Name);
                        }
                    }
                }
            }
            catch { }
            return sheets;
        }

        public static ExcelTableData ReadTable(string filePath, string sheetName)
        {
            var data = new ExcelTableData();
            
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var wb = new XLWorkbook(fs))
                {
                    var ws = wb.Worksheet(sheetName);
                if (ws == null) return data;

                var range = ws.RangeUsed();
                if (range == null) return data;

                int firstRow = range.FirstRow().RowNumber();
                int lastRow = range.LastRow().RowNumber();
                int firstCol = range.FirstColumn().ColumnNumber();
                int lastCol = range.LastColumn().ColumnNumber();

                data.MaxRow = lastRow - firstRow + 1;
                data.MaxCol = lastCol - firstCol + 1;

                // Read row heights
                for (int r = firstRow; r <= lastRow; r++)
                {
                    double heightPts = ws.Row(r).Height;
                    // Provide a default 15 points if zero
                    if (heightPts <= 0) heightPts = 15.0;
                    data.RowHeights[r - firstRow + 1] = heightPts;
                }

                // Read column widths
                for (int c = firstCol; c <= lastCol; c++)
                {
                    double widthUnits = ws.Column(c).Width;
                    // Standard Excel width is 8.43. If it's less than 2, it's likely "empty/default".
                    // We set a visible default for empty columns.
                    if (widthUnits < 1.0) widthUnits = 8.43; 
                    data.ColumnWidths[c - firstCol + 1] = widthUnits * 7.14;
                }

                // Temporary structure to skip cells that are hidden by a spanned range
                bool[,] covered = new bool[lastRow + 1, lastCol + 1];

                for (int r = firstRow; r <= lastRow; r++)
                {
                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        if (covered[r, c]) continue; // Part of a merged cell handled earlier

                        var cell = ws.Cell(r, c);
                        var cellData = new ExcelCellData
                        {
                            Row = r - firstRow + 1,
                            Column = c - firstCol + 1,
                            Value = cell.GetFormattedString() ?? ""
                        };

                        // Check for merged ranges
                        var mergedRange = ws.MergedRanges.FirstOrDefault(m => m.Contains(cell));
                        if (mergedRange != null)
                        {
                            int rSpan = mergedRange.RowCount();
                            int cSpan = mergedRange.ColumnCount();
                            cellData.RowSpan = rSpan;
                            cellData.ColSpan = cSpan;

                            // Mark spanned cells as covered
                            for (int i = 0; i < rSpan; i++)
                            {
                                for (int j = 0; j < cSpan; j++)
                                {
                                    covered[r + i, c + j] = true;
                                }
                            }
                        }

                        var style = cell.Style;

                        // Font
                        cellData.FontName = style.Font.FontName;
                        cellData.FontSizePt = style.Font.FontSize > 0 ? style.Font.FontSize : 11.0;
                        cellData.IsBold = style.Font.Bold;
                        cellData.IsItalic = style.Font.Italic;
                        
                        // We do not read cell colors or font colors anymore per user request.

                        // Alignment
                        cellData.HorizontalAlignment = style.Alignment.Horizontal switch
                        {
                            XLAlignmentHorizontalValues.Center => HorizontalAlignmentType.Center,
                            XLAlignmentHorizontalValues.Right => HorizontalAlignmentType.Right,
                            _ => HorizontalAlignmentType.Left
                        };

                        cellData.VerticalAlignment = style.Alignment.Vertical switch
                        {
                            XLAlignmentVerticalValues.Center => VerticalAlignmentType.Center,
                            XLAlignmentVerticalValues.Bottom => VerticalAlignmentType.Bottom,
                            _ => VerticalAlignmentType.Top
                        };

                        // Borders - If it is not 'None', we consider it has a border
                        cellData.HasTopBorder = style.Border.TopBorder != XLBorderStyleValues.None;
                        cellData.HasBottomBorder = style.Border.BottomBorder != XLBorderStyleValues.None;
                        cellData.HasLeftBorder = style.Border.LeftBorder != XLBorderStyleValues.None;
                        cellData.HasRightBorder = style.Border.RightBorder != XLBorderStyleValues.None;

                        data.Cells.Add(cellData);
                    }
                }
            } // End utilizing XLWorkbook
            } // End utilizing FileStream

            return data;
        }
    }
}
