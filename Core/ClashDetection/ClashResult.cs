using Autodesk.Revit.DB;

namespace RincoNhan.Core.ClashDetection
{
    public class ClashResult
    {
        public ElementId HostElementId { get; set; }
        public string HostCategory { get; set; }
        public string HostName { get; set; }

        public ElementId LinkElementId { get; set; }
        public string LinkCategory { get; set; }
        public string LinkName { get; set; }
        
        public string LinkDocumentName { get; set; }
        
        public bool IsLinkClash => !string.IsNullOrEmpty(LinkDocumentName);

        public string DisplayText => IsLinkClash 
            ? $"Host: {HostName} ({HostElementId}) <-> Link [{LinkDocumentName}]: {LinkName} ({LinkElementId})"
            : $"Host: {HostName} ({HostElementId}) <-> Host: {LinkName} ({LinkElementId})";
    }
}
