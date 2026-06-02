using System;
using System.Collections.Generic;

namespace RincoNhan.Tools.ExportFamilyData
{
    public class XYZModel
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class LineModel
    {
        public int Id { get; set; }
        public string LineStyle { get; set; }
        public string Type { get; set; } // ModelCurve, SymbolicCurve, etc.
        public string CurveShape { get; set; } // Line, Arc
        public XYZModel StartPoint { get; set; }
        public XYZModel EndPoint { get; set; }
        public XYZModel MidPoint { get; set; } // For arcs
        
        // Precise Arc parameters
        public XYZModel Center { get; set; }
        public XYZModel Normal { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        
        public bool IsReferenceLine { get; set; }
    }

    public class ReferencePlaneModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public XYZModel Direction { get; set; }
        public XYZModel Normal { get; set; }
        public XYZModel BubbleEnd { get; set; }
        public XYZModel FreeEnd { get; set; }
        
        public int IsReference { get; set; }
        public bool DefinesOrigin { get; set; }
    }

    public class DimensionModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string ValueString { get; set; }
        public bool IsLocked { get; set; }
        
        // Tên của Parameter được gán cho Dimension này (nếu có)
        public string Label { get; set; }
        
        // Chứa tên của các Reference Plane / Line mà Dimension này đang bắt điểm tới
        public List<string> ReferenceNames { get; set; } = new List<string>();
        public List<int> ReferenceIds { get; set; } = new List<int>();
        public List<string> StableRepresentations { get; set; } = new List<string>(); // Backup for complex mapping
        
        // Vị trí của đường ghi kích thước
        public XYZModel DimLineStart { get; set; }
        public XYZModel DimLineEnd { get; set; }
    }

    public class ParameterModel
    {
        public string Name { get; set; }
        public string Type { get; set; } // Length, Text, Material, etc.
        public string Group { get; set; }
        public string ValueString { get; set; }
        public string Formula { get; set; }
        public bool IsInstance { get; set; }
        public bool IsReporting { get; set; }
        public bool IsShared { get; set; }
    }

    public class FamilyDataModel
    {
        public string FamilyName { get; set; }
        public List<ParameterModel> Parameters { get; set; } = new List<ParameterModel>();
        public List<LineModel> Lines { get; set; } = new List<LineModel>();
        public List<ReferencePlaneModel> ReferencePlanes { get; set; } = new List<ReferencePlaneModel>();
        public List<DimensionModel> Dimensions { get; set; } = new List<DimensionModel>();
    }
}
