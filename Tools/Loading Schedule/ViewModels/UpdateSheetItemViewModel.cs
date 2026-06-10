using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.LoadingSchedule.ViewModels
{
    public class UpdateSheetItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ElementId SheetId { get; set; }
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        
        public ObservableCollection<ViewWrapper> AvailableViews { get; set; }

        private ViewWrapper _selectedView;
        public ViewWrapper SelectedView
        {
            get => _selectedView;
            set { _selectedView = value; OnPropertyChanged(); }
        }

        public ElementId TargetLegendId { get; set; }
        public string TargetLegendName { get; set; }

        public UpdateSheetItemViewModel()
        {
            AvailableViews = new ObservableCollection<ViewWrapper>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
