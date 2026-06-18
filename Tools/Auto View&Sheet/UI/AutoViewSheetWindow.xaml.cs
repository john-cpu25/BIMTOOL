using System.Windows;
using RincoNhan.Tools.AutoViewSheet.ViewModels;

namespace RincoNhan.Tools.AutoViewSheet.UI
{
    public partial class AutoViewSheetWindow : Window
    {
        public AutoViewSheetWindow(AutoViewSheetViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
