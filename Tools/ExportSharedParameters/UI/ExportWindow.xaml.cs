using System.Windows;
using RincoNhan.Tools.ExportSharedParameters.ViewModels;

namespace RincoNhan.Tools.ExportSharedParameters.UI
{
    public partial class ExportWindow : Window
    {
        public ExportWindow(ExportViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
