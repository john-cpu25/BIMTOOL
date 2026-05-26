using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace RincoNhan.Tools.CreateLevel
{
    /// <summary>
    /// Lightweight Excel reader that reads .xlsx files using only built-in .NET libraries.
    /// No ClosedXML or OpenXML SDK dependency — avoids AssemblyLoadContext issues in Revit 2025+.
    /// </summary>
    public static class SimpleExcelReader
    {
        private static readonly XNamespace NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace NS_R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace NS_WB = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        /// <summary>
        /// Get all sheet names from an xlsx file.
        /// </summary>
        public static List<string> GetSheetNames(string filePath)
        {
            var sheets = new List<string>();

            // Copy to temp file to avoid file lock issues
            string tempPath = Path.GetTempFileName();
            File.Copy(filePath, tempPath, true);

            try
            {
                using (var archive = ZipFile.OpenRead(tempPath))
                {
                    var workbookEntry = archive.GetEntry("xl/workbook.xml");
                    if (workbookEntry == null) return sheets;

                    using (var stream = workbookEntry.Open())
                    {
                        var doc = XDocument.Load(stream);
                        var sheetsElement = doc.Descendants(NS + "sheet");
                        foreach (var sheet in sheetsElement)
                        {
                            string name = sheet.Attribute("name")?.Value;
                            if (!string.IsNullOrEmpty(name))
                            {
                                sheets.Add(name);
                            }
                        }
                    }
                }
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            return sheets;
        }

        /// <summary>
        /// Read cell data from a specific sheet. Returns list of (row, col, value) tuples.
        /// </summary>
        public static List<(string name, double? height)> ReadLevelData(string filePath, string sheetName)
        {
            var result = new List<(string name, double? height)>();

            string tempPath = Path.GetTempFileName();
            File.Copy(filePath, tempPath, true);

            try
            {
                using (var archive = ZipFile.OpenRead(tempPath))
                {
                    // 1. Load shared strings
                    var sharedStrings = LoadSharedStrings(archive);

                    // 2. Find the sheet index
                    int sheetIndex = GetSheetIndex(archive, sheetName);
                    if (sheetIndex < 0) return result;

                    // 3. Read sheet data
                    string sheetPath = $"xl/worksheets/sheet{sheetIndex}.xml";
                    var sheetEntry = archive.GetEntry(sheetPath);
                    if (sheetEntry == null)
                    {
                        // Try alternative naming
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.StartsWith("xl/worksheets/sheet") && entry.FullName.EndsWith(".xml"))
                            {
                                // Check relationship mapping
                                sheetEntry = entry;
                                break;
                            }
                        }
                        if (sheetEntry == null) return result;
                    }

                    using (var stream = sheetEntry.Open())
                    {
                        var doc = XDocument.Load(stream);
                        var rows = doc.Descendants(NS + "row").OrderBy(r => int.Parse(r.Attribute("r")?.Value ?? "0"));

                        foreach (var row in rows)
                        {
                            var cells = row.Elements(NS + "c").ToList();

                            string nameValue = null;
                            double? heightValue = null;

                            foreach (var cell in cells)
                            {
                                string cellRef = cell.Attribute("r")?.Value ?? "";
                                string colLetter = GetColumnLetter(cellRef);
                                string cellType = cell.Attribute("t")?.Value;
                                string cellValue = cell.Element(NS + "v")?.Value;

                                if (string.IsNullOrEmpty(cellValue)) continue;

                                string actualValue;
                                if (cellType == "s")
                                {
                                    // Shared string reference
                                    int ssIndex = int.Parse(cellValue);
                                    actualValue = ssIndex < sharedStrings.Count ? sharedStrings[ssIndex] : cellValue;
                                }
                                else
                                {
                                    actualValue = cellValue;
                                }

                                if (colLetter == "A")
                                {
                                    nameValue = actualValue?.Trim();
                                }
                                else if (colLetter == "B")
                                {
                                    if (double.TryParse(actualValue, System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out double h))
                                    {
                                        heightValue = h;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(nameValue))
                            {
                                result.Add((nameValue, heightValue));
                            }
                        }
                    }
                }
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            return result;
        }

        private static List<string> LoadSharedStrings(ZipArchive archive)
        {
            var strings = new List<string>();
            var ssEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (ssEntry == null) return strings;

            using (var stream = ssEntry.Open())
            {
                var doc = XDocument.Load(stream);
                foreach (var si in doc.Descendants(NS + "si"))
                {
                    // Handle both simple <t> and rich text <r><t>
                    var tElements = si.Descendants(NS + "t");
                    string text = string.Join("", tElements.Select(t => t.Value));
                    strings.Add(text);
                }
            }

            return strings;
        }

        private static int GetSheetIndex(ZipArchive archive, string sheetName)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            if (workbookEntry == null) return -1;

            using (var stream = workbookEntry.Open())
            {
                var doc = XDocument.Load(stream);
                var sheets = doc.Descendants(NS + "sheet").ToList();

                for (int i = 0; i < sheets.Count; i++)
                {
                    if (sheets[i].Attribute("name")?.Value == sheetName)
                    {
                        return i + 1; // Sheet indices are 1-based
                    }
                }
            }

            return -1;
        }

        private static string GetColumnLetter(string cellRef)
        {
            // Extract column letters from cell reference like "A1", "B2", "AA3"
            string col = "";
            foreach (char c in cellRef)
            {
                if (char.IsLetter(c))
                    col += c;
                else
                    break;
            }
            return col.ToUpper();
        }
    }
}
