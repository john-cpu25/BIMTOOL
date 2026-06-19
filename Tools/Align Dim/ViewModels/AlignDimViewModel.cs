using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.Align_Dim.Models;

namespace RincoNhan.Tools.Align_Dim.ViewModels
{
    public partial class AlignDimViewModel : ObservableObject
    {
        private readonly AlignDimSettings _settings;
        private Document _doc;

        [ObservableProperty]
        private double _distance;

        [ObservableProperty]
        private bool _saveTemplate;

        // 0 = By Distance, 1 = Match Views
        [ObservableProperty]
        private int _selectedTabIndex;

        [ObservableProperty]
        private string _sourceViewName;

        public ObservableCollection<ViewItem> TargetViews { get; set; } = new ObservableCollection<ViewItem>();

        // Will be true if user clicked Apply, false if they closed the window
        public bool DialogResult { get; private set; } = false;

        public AlignDimViewModel(Document doc)
        {
            _doc = doc;
            _settings = AlignDimSettings.Load();
            Distance = _settings.DistanceMm;
            SaveTemplate = true; // Default to saving to encourage reuse
            SelectedTabIndex = 0;

            if (doc != null)
            {
                SourceViewName = doc.ActiveView.Name;
                LoadTargetViews();
            }
        }

        private void LoadTargetViews()
        {
            TargetViews.Clear();
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan || v.ViewType == ViewType.CeilingPlan || v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation))
                .Where(v => v.Id != _doc.ActiveView.Id)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                TargetViews.Add(new ViewItem(view));
            }
        }

        [RelayCommand]
        private void SelectAllViews()
        {
            foreach (var v in TargetViews) v.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNoneViews()
        {
            foreach (var v in TargetViews) v.IsSelected = false;
        }

        [RelayCommand]
        private void Apply(Window window)
        {
            if (SelectedTabIndex == 0 && SaveTemplate)
            {
                _settings.DistanceMm = Distance;
                _settings.Save();
            }

            if (SelectedTabIndex == 1 && !TargetViews.Any(v => v.IsSelected))
            {
                MessageBox.Show("Please select at least one Target View.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            DialogResult = false;
            window.Close();
        }
    }
}
