using System.Windows;
using RincoNhan.Tools.AddSharedParameters.ViewModels;

namespace RincoNhan.Tools.AddSharedParameters.UI
{
    public partial class AddSharedParamWindow : Window
    {
        public AddSharedParamWindow(AddSharedParamViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
