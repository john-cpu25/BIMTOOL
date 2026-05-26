using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ElementsTags.ViewModels;

namespace RincoNhan.Tools.ElementsTags.UI
{
    public class RevitColorToWpfBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Autodesk.Revit.DB.Color rvtColor)
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(rvtColor.Red, rvtColor.Green, rvtColor.Blue));
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public partial class ElementsTagsWindow : Window
    {
        public ElementsTagsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            var collector = new RevitDataCollector(uidoc.Document, uidoc.Document.ActiveView);
            var handler = new ElementsTagsExternalEventHandler();
            
            DataContext = new MainViewModel(collector, handler);
        }
    }
}
