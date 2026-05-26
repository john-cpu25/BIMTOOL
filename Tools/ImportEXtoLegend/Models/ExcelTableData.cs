using System.Collections.Generic;

namespace RincoNhan.Tools.ImportEXtoLegend.Models
{
    public class ExcelTableData
    {
        public List<ExcelCellData> Cells { get; set; } = new List<ExcelCellData>();
        
        // Key: row index (1-based), Value: height in points
        public Dictionary<int, double> RowHeights { get; set; } = new Dictionary<int, double>();
        
        // Key: col index (1-based), Value: width in characters or points (need consistent unit, let's use points or standard width)
        public Dictionary<int, double> ColumnWidths { get; set; } = new Dictionary<int, double>();

        public int MaxRow { get; set; }
        public int MaxCol { get; set; }
    }
}
