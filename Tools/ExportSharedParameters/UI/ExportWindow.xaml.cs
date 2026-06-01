using System.Windows;
using RincoNhan.Tools.ExportSharedParameters.ViewModels;

namespace RincoNhan.Tools.ExportSharedParameters.UI
{
    public partial class ExportWindow : Window
    {
        public ExportWindow(ExportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
