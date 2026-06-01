using System.Windows;
using RincoNhan.Tools.ExportExcel.ViewModels;

namespace RincoNhan.Tools.ExportExcel.UI
{
    public partial class ExportExcelWindow : Window
    {
        public ExportExcelWindow(ExportExcelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
