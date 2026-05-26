using System.Windows;
using RincoNhan.Tools.CreateSectionWall.ViewModels;

namespace RincoNhan.Tools.CreateSectionWall.UI
{
    public partial class CreateSectionWallWindow : Window
    {
        public CreateSectionWallWindow(CreateSectionWallViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestHideWindow = Hide;
            viewModel.RequestShowWindow = Show;
        }
    }
}
