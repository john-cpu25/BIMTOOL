using System;
using System.Drawing;

namespace RincoNhan.Tools.ImportEXtoLegend.Models
{
    public class ExcelCellData
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string Value { get; set; }
        
        // Ranges / Merged Cells
        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;

        // Styling
        public Color BackgroundColor { get; set; } = Color.White;
        
        // Font
        public string FontName { get; set; }
        public double FontSizePt { get; set; }
        public Color FontColor { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }

        public HorizontalAlignmentType HorizontalAlignment { get; set; }
        public VerticalAlignmentType VerticalAlignment { get; set; }
        
        // Borders (Simplification: True if has any border, or could store styles. Let's just store presence and color for now)
        public bool HasTopBorder { get; set; }
        public bool HasBottomBorder { get; set; }
        public bool HasLeftBorder { get; set; }
        public bool HasRightBorder { get; set; }
        public Color BorderColor { get; set; } = Color.Black;
    }

    public enum HorizontalAlignmentType
    {
        Left,
        Center,
        Right
    }

    public enum VerticalAlignmentType
    {
        Top,
        Center,
        Bottom
    }
}
