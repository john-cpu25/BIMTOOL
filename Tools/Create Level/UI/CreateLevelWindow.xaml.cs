using System.Windows;
using RincoNhan.Tools.CreateLevel.ViewModels;

namespace RincoNhan.Tools.CreateLevel.UI
{
    public partial class CreateLevelWindow : Window
    {
        public CreateLevelWindow(CreateLevelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
