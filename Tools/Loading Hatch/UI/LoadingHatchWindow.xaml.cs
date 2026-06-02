using System.Windows;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingHatch.ViewModels;

namespace RincoNhan.Tools.LoadingHatch.UI
{
    public partial class LoadingHatchWindow : Window
    {
        public LoadingHatchWindow(Document doc, View activeView)
        {
            InitializeComponent();
            this.DataContext = new LoadingHatchViewModel(doc, activeView);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
