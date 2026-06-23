using System.Collections.Generic;

namespace RincoNhan.Tools.ConvertHatch
{
    public class ConvertHatchModel
    {
        public string Version { get; set; } = "3.0";
        public List<FillPatternData> AllFillPatterns { get; set; } = new List<FillPatternData>();
        public List<FilledRegionData> FilledRegions { get; set; } = new List<FilledRegionData>();
        public List<TextNoteData> Texts { get; set; } = new List<TextNoteData>();
        public List<CurveElementData> Lines { get; set; } = new List<CurveElementData>();
    }

    public class FillPatternData
    {
        public string Name { get; set; }
        public string Target { get; set; } // "Drafting" or "Model"
        public List<FillGridData> Grids { get; set; } = new List<FillGridData>();
    }

    public class FillGridData
    {
        public double Angle { get; set; }
        public double OriginU { get; set; }
        public double OriginV { get; set; }
        public double Offset { get; set; }
        public double Shift { get; set; }
        public List<double> Segments { get; set; } = new List<double>();
    }

    public class FilledRegionData
    {
        public FilledRegionTypeData TypeData { get; set; }
        public List<List<CurveData>> Boundaries { get; set; } = new List<List<CurveData>>();
        
        public bool HasOverride { get; set; }
        public string OverrideForegroundPatternName { get; set; }
        public int OverrideColorRed { get; set; }
        public int OverrideColorGreen { get; set; }
        public int OverrideColorBlue { get; set; }
        
        public string OverrideBackgroundPatternName { get; set; }
        public int OverrideBackgroundColorRed { get; set; }
        public int OverrideBackgroundColorGreen { get; set; }
        public int OverrideBackgroundColorBlue { get; set; }
    }

    public class FilledRegionTypeData
    {
        public string Name { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
        
        public int BackgroundColorRed { get; set; }
        public int BackgroundColorGreen { get; set; }
        public int BackgroundColorBlue { get; set; }

        public string ForegroundPatternName { get; set; }
        public string BackgroundPatternName { get; set; }
        public int LineWeight { get; set; }
        public bool IsMasking { get; set; }

        // Identity Data
        public string Description { get; set; }
        public string Model { get; set; }
        public string Manufacturer { get; set; }
        public string TypeComments { get; set; }
        public string Url { get; set; }
        public string Keynote { get; set; }
    }

    public class CurveData
    {
        public string CurveType { get; set; } // "Line", "Arc", "Ellipse", "NurbSpline", "HermiteSpline"
        
        public PointData StartPoint { get; set; }
        public PointData EndPoint { get; set; }
        
        public PointData Center { get; set; }
        public double Radius { get; set; }
        public double RadiusX { get; set; }
        public double RadiusY { get; set; }
        public PointData XDirection { get; set; }
        public PointData YDirection { get; set; }
        public PointData Normal { get; set; }
        
        public double StartParameter { get; set; }
        public double EndParameter { get; set; }

        public List<PointData> ControlPoints { get; set; }
        public List<double> Knots { get; set; }
        public List<double> Weights { get; set; }
        public int Degree { get; set; }
        public bool IsClosed { get; set; }
        public bool IsRational { get; set; }
    }

    public class PointData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public PointData() { }
        public PointData(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class TextNoteData
    {
        public string Text { get; set; }
        public PointData Location { get; set; }
        public PointData BaseDirection { get; set; }
        public PointData UpDirection { get; set; }
        public double Width { get; set; }
        public TextNoteTypeData TypeData { get; set; }
        public string HorizontalAlignment { get; set; }
        public string VerticalAlignment { get; set; }
    }

    public class TextNoteTypeData
    {
        public string Name { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
        public double TextSize { get; set; }
        public string FontName { get; set; }
        public int Bold { get; set; }
        public int Italic { get; set; }
        public int Underline { get; set; }
        public double WidthScale { get; set; }
    }

    public class CurveElementData
    {
        public CurveData Curve { get; set; }
        public string LineStyleName { get; set; }
        public bool IsModelCurve { get; set; }
    }
}
