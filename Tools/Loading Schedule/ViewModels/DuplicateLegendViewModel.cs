using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.LoadingSchedule.ViewModels
{
    public class DuplicateLegendViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;

        public ObservableCollection<ViewWrapper> AvailableLegends { get; set; }
        public ObservableCollection<SheetItemViewModel> Sheets { get; set; }

        private ViewWrapper _selectedLegend;
        public ViewWrapper SelectedLegend
        {
            get => _selectedLegend;
            set { _selectedLegend = value; OnPropertyChanged(); }
        }

        public DuplicateLegendViewModel(Document doc)
        {
            _doc = doc;
            AvailableLegends = new ObservableCollection<ViewWrapper>();
            Sheets = new ObservableCollection<SheetItemViewModel>();
        }

        public void LoadData()
        {
            // Load Legends
            var legends = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var leg in legends)
            {
                AvailableLegends.Add(new ViewWrapper(leg));
            }

            if (AvailableLegends.Any())
            {
                var target = AvailableLegends.FirstOrDefault(l => l.Name.Equals("LOADING PLAN", System.StringComparison.OrdinalIgnoreCase))
                          ?? AvailableLegends.FirstOrDefault(l => l.Name.IndexOf("LOADING PLAN", System.StringComparison.OrdinalIgnoreCase) >= 0);
                SelectedLegend = target ?? AvailableLegends.First();
            }

            // Load Sheets
            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate && s.LookupParameter("RINCO_TB_SHEET SERIES")?.AsString() == "S0100 SERIES - LOADING PLANS")
                .OrderBy(s => s.SheetNumber)
                .ToList();

            foreach (var s in sheets)
            {
                Sheets.Add(new SheetItemViewModel
                {
                    SheetId = s.Id,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.Name,
                    IsSelected = false
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
