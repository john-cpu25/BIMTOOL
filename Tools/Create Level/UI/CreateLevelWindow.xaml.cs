using System.Windows;
using RincoNhan.Tools.CreateLevel.ViewModels;

namespace RincoNhan.Tools.CreateLevel.UI
{
    public partial class CreateLevelWindow : Window
    {
        public CreateLevelWindow(CreateLevelViewModel viewModel)
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
