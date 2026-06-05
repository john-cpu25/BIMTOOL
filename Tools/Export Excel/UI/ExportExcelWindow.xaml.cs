using System.Windows;
using RincoNhan.Tools.ExportExcel.ViewModels;

namespace RincoNhan.Tools.ExportExcel.UI
{
    public partial class ExportExcelWindow : Window
    {
        public ExportExcelWindow(ExportExcelViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
