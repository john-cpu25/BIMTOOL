using System.Windows;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingHatch.ViewModels;

namespace RincoNhan.Tools.LoadingHatch.UI
{
    public partial class LoadingHatchWindow : Window
    {
        public LoadingHatchWindow(Document doc, View activeView)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            this.DataContext = new LoadingHatchViewModel(doc, activeView);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
