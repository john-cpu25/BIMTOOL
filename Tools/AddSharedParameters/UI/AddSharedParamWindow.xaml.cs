using System.Windows;
using RincoNhan.Tools.AddSharedParameters.ViewModels;

namespace RincoNhan.Tools.AddSharedParameters.UI
{
    public partial class AddSharedParamWindow : Window
    {
        public AddSharedParamWindow(AddSharedParamViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
