using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoNhan.Tools.AddSharedParameters.Models
{
    public partial class SPDefinitionItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public ExternalDefinition Definition { get; set; }
        public string Name { get; set; }
        public string GroupName { get; set; }
        public string DataType { get; set; }
    }
}
