using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoNhan.Tools.ReloadCADLinks
{
    /// <summary>
    /// Model class representing a linked CAD file in the Revit document.
    /// Implements ObservableObject for WPF data binding using Source Generators.
    /// </summary>
    public partial class CADLinkInfo : ObservableObject
    {
        /// <summary>
        /// Whether this link is selected for reload
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private string _fileName;

        [ObservableProperty]
        private string _filePath;

        /// <summary>
        /// Current link status (Loaded, Not Found, Unloaded, etc.)
        /// </summary>
        [ObservableProperty]
        private string _status;

        /// <summary>
        /// Status icon for UI display
        /// </summary>
        [ObservableProperty]
        private string _statusIcon;

        /// <summary>
        /// True nếu đây là Link (có thể reload), False nếu là Import (không thể reload)
        /// </summary>
        [ObservableProperty]
        private bool _isReloadable = true;

        /// <summary>
        /// Tooltip explaining the status (e.g. why it's not reloadable)
        /// </summary>
        public string StatusToolTip => IsReloadable 
            ? "Link - Selection allowed for reload" 
            : "Imported CAD - Cannot be reloaded from an external file";

        /// <summary>
        /// True nếu đây là Cloud link (BIM 360 / ACC)
        /// </summary>
        [ObservableProperty]
        private bool _isCloudPath;

        /// <summary>
        /// The ElementId of the CADLinkType in Revit
        /// </summary>
        public ElementId TypeId { get; set; }

        /// <summary>
        /// The linked file status from Revit API
        /// </summary>
        public LinkedFileStatus LinkedStatus { get; set; }

        /// <summary>
        /// Override path: nếu user chọn file CAD khác tên cho link này
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OverrideFileName))]
        [NotifyPropertyChangedFor(nameof(HasOverride))]
        private string _overridePath;

        /// <summary>
        /// Tên file của OverridePath (chỉ tên, không có đường dẫn)
        /// </summary>
        public string OverrideFileName => string.IsNullOrEmpty(OverridePath) 
            ? null 
            : System.IO.Path.GetFileName(OverridePath);

        /// <summary>
        /// True nếu user đã chọn file override cho link này
        /// </summary>
        public bool HasOverride => !string.IsNullOrEmpty(OverridePath);

        /// <summary>
        /// Get a user-friendly status string from LinkedFileStatus
        /// </summary>
        public static string GetStatusText(LinkedFileStatus status)
        {
            switch (status)
            {
                case LinkedFileStatus.Loaded:
                    return "Loaded";
                case LinkedFileStatus.NotFound:
                    return "Not Found";
                case LinkedFileStatus.Unloaded:
                    return "Unloaded";
                case LinkedFileStatus.LocallyUnloaded:
                    return "Locally Unloaded";
                case LinkedFileStatus.InClosedWorkset:
                    return "In Closed Workset";
                default:
                    return status.ToString();
            }
        }

        /// <summary>
        /// Get a status icon based on LinkedFileStatus
        /// </summary>
        public static string GetStatusIcon(LinkedFileStatus status)
        {
            switch (status)
            {
                case LinkedFileStatus.Loaded:
                    return "✅";
                case LinkedFileStatus.NotFound:
                    return "❌";
                case LinkedFileStatus.Unloaded:
                    return "⏳";
                case LinkedFileStatus.LocallyUnloaded:
                    return "⏳";
                default:
                    return "⚠️";
            }
        }
    }
}
