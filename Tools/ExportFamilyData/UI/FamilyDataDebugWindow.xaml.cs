using System.Windows;
using RincoNhan.Tools.ExportFamilyData.ViewModels;

namespace RincoNhan.Tools.ExportFamilyData.UI
{
    public partial class FamilyDataDebugWindow : Window
    {
        public FamilyDataDebugWindow(FamilyDataDebugViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            this.DataContext = viewModel;
            viewModel.CloseAction = () => this.Close();
        }
    }
}
