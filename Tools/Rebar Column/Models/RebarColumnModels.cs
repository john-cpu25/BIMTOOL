using System;
using System.Collections.Generic;

namespace RincoNhan.Tools.RebarColumn.Models
{
    public class ColumnData
    {
        public double Width { get; set; }
        public double Depth { get; set; }
        public double Cover { get; set; }
    }

    public class RebarConfig
    {
        // General
        public double Cover { get; set; } = 30; // mm

        // Main Rebar
        public string MainBarTypeName { get; set; }
        public double MainDiameter { get; set; }
        public int CountX { get; set; } = 3;
        public int CountY { get; set; } = 3;

        // Stirrups
        public string StirrupBarTypeName { get; set; }
        public double StirrupDiameter { get; set; }
        public StirrupPattern SelectedPattern { get; set; } = StirrupPattern.Standard;
        public string SelectedStirrupShapeName { get; set; } = "";
        
        // Spacing
        public bool IsSeismic { get; set; } = false;
        public double Spacing1 { get; set; } = 100;
        public double Spacing2 { get; set; } = 200;
        public double Spacing3 { get; set; } = 100;
        
        // Lap
        public double LapFactor { get; set; } = 40; // 40D
    }

    public enum StirrupPattern
    {
        Standard,           // 1 main tie
        WithInternalLinks,  // 1 main tie + internal links
        CrossTies           // 1 main tie + cross ties in both directions
    }
}
