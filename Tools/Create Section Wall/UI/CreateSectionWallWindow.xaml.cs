using System.Windows;
using RincoNhan.Tools.CreateSectionWall.ViewModels;

namespace RincoNhan.Tools.CreateSectionWall.UI
{
    public partial class CreateSectionWallWindow : Window
    {
        public CreateSectionWallWindow(CreateSectionWallViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestHideWindow = Hide;
            viewModel.RequestShowWindow = Show;
        }
    }
}
