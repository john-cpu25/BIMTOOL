using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.FillRegionManager
{
    public class FillRegionModel : INotifyPropertyChanged
    {
        private ElementId _id;
        public ElementId Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _typeName;
        public string TypeName
        {
            get => _typeName;
            set { _typeName = value; OnPropertyChanged(); }
        }

        private string _hatchName;
        public string HatchName
        {
            get => _hatchName;
            set { _hatchName = value; OnPropertyChanged(); }
        }

        private string _typeMark;
        public string TypeMark
        {
            get => _typeMark;
            set { _typeMark = value; OnPropertyChanged(); }
        }

        private ImageSource _hatchPreview;
        public ImageSource HatchPreview
        {
            get => _hatchPreview;
            set { _hatchPreview = value; OnPropertyChanged(); }
        }

        private string _group;
        public string Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
