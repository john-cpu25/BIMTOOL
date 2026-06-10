using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.LoadingSchedule.ViewModels
{
    public class UpdateLegendViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;

        public ObservableCollection<UpdateSheetItemViewModel> Sheets { get; set; }
        public ObservableCollection<ViewWrapper> TemplateLegends { get; set; }

        private ViewWrapper _selectedTemplateLegend;
        public ViewWrapper SelectedTemplateLegend
        {
            get => _selectedTemplateLegend;
            set { _selectedTemplateLegend = value; OnPropertyChanged(); }
        }

        public UpdateLegendViewModel(Document doc)
        {
            _doc = doc;
            Sheets = new ObservableCollection<UpdateSheetItemViewModel>();
            TemplateLegends = new ObservableCollection<ViewWrapper>();
        }

        public void LoadData()
        {
            // Load Template Legends
            var legends = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var leg in legends)
            {
                TemplateLegends.Add(new ViewWrapper(leg));
            }

            if (TemplateLegends.Any())
            {
                var target = TemplateLegends.FirstOrDefault(l => l.Name.Equals("LOADING PLAN", System.StringComparison.OrdinalIgnoreCase))
                          ?? TemplateLegends.FirstOrDefault(l => l.Name.IndexOf("LOADING PLAN", System.StringComparison.OrdinalIgnoreCase) >= 0);
                SelectedTemplateLegend = target ?? TemplateLegends.First();
            }

            // Load Sheets with "loading plan" in their name
            var allSheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate && s.LookupParameter("RINCO_TB_SHEET SERIES")?.AsString() == "S0100 SERIES - LOADING PLANS")
                .OrderBy(s => s.SheetNumber)
                .ToList();

            foreach (var sheet in allSheets)
            {
                var item = new UpdateSheetItemViewModel
                {
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    IsSelected = true
                };

                // Find viewports on this sheet
                var viewports = new FilteredElementCollector(_doc, sheet.Id)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                foreach (var vp in viewports)
                {
                    View v = _doc.GetElement(vp.ViewId) as View;
                    if (v == null) continue;

                    if (v.ViewType == ViewType.Legend)
                    {
                        item.TargetLegendId = v.Id;
                        item.TargetLegendName = v.Name;
                    }
                    else if (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan || v.ViewType == ViewType.EngineeringPlan || v.ViewType == ViewType.AreaPlan)
                    {
                        var vw = new ViewWrapper(v);
                        item.AvailableViews.Add(vw);
                    }
                }

                if (item.AvailableViews.Any())
                {
                    item.SelectedView = item.AvailableViews.First();
                }
                
                if (string.IsNullOrEmpty(item.TargetLegendName))
                {
                    item.TargetLegendName = "(No Legend Found)";
                }

                Sheets.Add(item);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
