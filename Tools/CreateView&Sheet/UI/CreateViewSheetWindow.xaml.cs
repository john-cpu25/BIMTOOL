using System.Windows;
using Autodesk.Revit.DB;
using RincoNhan.Tools.CreateViewSheet.ViewModels;

namespace RincoNhan.Tools.CreateViewSheet.UI
{
    public partial class CreateViewSheetWindow : Window
    {
        public CreateViewSheetWindow(CreateViewSheetViewModel viewModel)
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
