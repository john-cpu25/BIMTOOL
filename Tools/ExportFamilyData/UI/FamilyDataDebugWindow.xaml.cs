using System.Windows;
using RincoNhan.Tools.ExportFamilyData.ViewModels;

namespace RincoNhan.Tools.ExportFamilyData.UI
{
    public partial class FamilyDataDebugWindow : Window
    {
        public FamilyDataDebugWindow(FamilyDataDebugViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            viewModel.CloseAction = () => this.Close();
        }
    }
}
