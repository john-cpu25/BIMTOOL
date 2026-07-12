using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.TwoDFamilyManager.Models
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FamilyCategoryItem : ObservableObject
    {
        public string Name { get; set; }
        public ObservableCollection<FamilyModelItem> Families { get; set; } = new ObservableCollection<FamilyModelItem>();
        
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
    }

    public class FamilyModelItem : ObservableObject
    {
        public string Name { get; set; }
        public Family Family { get; set; }
        public ObservableCollection<FamilySymbolItem> Symbols { get; set; } = new ObservableCollection<FamilySymbolItem>();
        
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }
    }

    public class FamilySymbolItem : ObservableObject
    {
        public string Name { get; set; }
        public FamilySymbol Symbol { get; set; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private ImageSource _image;
        public ImageSource Image
        {
            get => _image;
            set { _image = value; OnPropertyChanged(); }
        }
    }
}
