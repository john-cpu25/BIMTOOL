using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.SmartLinkCad
{
    /// <summary>
    /// Represents a single layer (SubCategory) inside a CAD file for Tab 2 layer management.
    /// </summary>
    public class CADLayerInfo : INotifyPropertyChanged
    {
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Name of the layer (SubCategory name)</summary>
        public string LayerName { get; set; }

        /// <summary>Name of the parent CAD file category</summary>
        public string CadFileName { get; set; }

        /// <summary>The SubCategory object from Revit API</summary>
        public Category SubCategory { get; set; }

        /// <summary>The parent root Category of the CAD file</summary>
        public Category ParentCategory { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
