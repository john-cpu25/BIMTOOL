using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Filter.ViewModels;

namespace RincoNhan.Tools.Filter.UI
{
    public partial class FilterWindow : Window
    {
        private UIDocument _uidoc;
        private RevitDataCollector _collector;
        private FilterExternalEventHandler _handler;
        private ExternalEvent _externalEvent;
        public MainViewModel ViewModel { get; }

        public FilterWindow(UIDocument uidoc)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            _uidoc = uidoc;
            _collector = new RevitDataCollector(uidoc.Document, uidoc.ActiveView);
            ViewModel = new MainViewModel(_collector);
            DataContext = ViewModel;

            _handler = new FilterExternalEventHandler { ViewModel = ViewModel };
            _externalEvent = ExternalEvent.Create(_handler);

            ViewModel.OnResetViewRequested += (s, e) => RequestAction("RESET");
            ViewModel.OnVisibilityActionRequested += (s, action) => RequestAction(action);
            
            ViewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
                {
                    ApplyTheme(ViewModel.IsDarkMode);
                }
            };

            // Apply initial theme
            ApplyTheme(ViewModel.IsDarkMode);
        }

        private void ApplyTheme(bool isDark)
        {
            try
            {
                string themePath = isDark
                    ? "pack://application:,,,/RincoNhan;component/Tools/Filter/UI/DarkTheme.xaml"
                    : "pack://application:,,,/RincoNhan;component/Tools/Filter/UI/LightTheme.xaml";

                var dict = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };

                // Find the existing theme dictionary by checking for source names containing "Theme.xaml"
                ResourceDictionary existingTheme = null;
                foreach (var d in this.Resources.MergedDictionaries)
                {
                    if (d.Source != null && d.Source.ToString().EndsWith("Theme.xaml"))
                    {
                        existingTheme = d;
                        break;
                    }
                }

                if (existingTheme != null)
                {
                    int index = this.Resources.MergedDictionaries.IndexOf(existingTheme);
                    this.Resources.MergedDictionaries[index] = dict;
                }
                else
                {
                    // Fallback: If not found, insert at the beginning
                    this.Resources.MergedDictionaries.Insert(0, dict);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error applying theme: " + ex.Message);
            }
        }

        private void RequestAction(string action)
        {
            _handler.RequestAction = action;
            _externalEvent.Raise();
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            RequestAction("APPLY");
        }

        private string GetCombinedFamilyName(Element elem, Document doc)
        {
            var typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var elemType = doc.GetElement(typeId) as ElementType;
                if (elemType != null)
                {
                    string fName = elemType.FamilyName ?? "";
                    string tName = elemType.Name ?? "";
                    return !string.IsNullOrEmpty(fName) && !string.IsNullOrEmpty(tName) ? $"{fName} - {tName}" : fName + tName;
                }
            }
            return "";
        }

        private string GetParamValueString(Parameter param)
        {
            string val = param.AsValueString();
            if (string.IsNullOrEmpty(val)) val = param.AsString();
            if (string.IsNullOrEmpty(val)) val = param.AsInteger().ToString();
            if (string.IsNullOrEmpty(val)) val = "<Empty/No Value>";
            return val;
        }
    }
}
