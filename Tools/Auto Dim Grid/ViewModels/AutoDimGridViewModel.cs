using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.Auto_Dim_Grid.ViewModels
{
    public class AutoDimGridViewModel : INotifyPropertyChanged
    {
        private double _distanceToNearestDim;
        private double _distanceBetweenDims;
        private DimensionType _selectedDimensionType;

        public double DistanceToNearestDim
        {
            get => _distanceToNearestDim;
            set { _distanceToNearestDim = value; OnPropertyChanged(); }
        }

        public double DistanceBetweenDims
        {
            get => _distanceBetweenDims;
            set { _distanceBetweenDims = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DimensionType> DimensionTypes { get; set; }

        public DimensionType SelectedDimensionType
        {
            get => _selectedDimensionType;
            set { _selectedDimensionType = value; OnPropertyChanged(); }
        }

        private bool _dimOpenings;
        public bool DimOpenings
        {
            get => _dimOpenings;
            set { _dimOpenings = value; OnPropertyChanged(); OnPropertyChanged(nameof(DimCenters)); }
        }

        public bool DimCenters
        {
            get => !_dimOpenings;
            set { _dimOpenings = !value; OnPropertyChanged(); OnPropertyChanged(nameof(DimOpenings)); }
        }

        private bool _dimOverall;
        public bool DimOverall
        {
            get => _dimOverall;
            set { _dimOverall = value; OnPropertyChanged(); }
        }

        public AutoDimGridViewModel(Document doc)
        {
            // Default values
            DistanceToNearestDim = 0.0;
            DistanceBetweenDims = 8.0;
            DimOpenings = true;
            DimOverall = true;

            DimensionTypes = new ObservableCollection<DimensionType>();
            var dimTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(dt => dt.StyleType == DimensionStyleType.Linear)
                .OrderBy(dt => dt.Name)
                .ToList();

            foreach (var dt in dimTypes)
            {
                DimensionTypes.Add(dt);
            }

            if (DimensionTypes.Count > 0)
            {
                SelectedDimensionType = DimensionTypes[0];
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
