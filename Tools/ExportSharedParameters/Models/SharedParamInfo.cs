using System;

namespace RincoNhan.Tools.ExportSharedParameters.Models
{
    public class SharedParamInfo
    {
        public string Name { get; set; }
        public Guid Guid { get; set; }
        public string DataType { get; set; }
        public string DataCategory { get; set; }
        public string Group { get; set; }
        public bool IsVisible { get; set; }
        public string Description { get; set; }
        public bool UserModifiable { get; set; }
    }
}
