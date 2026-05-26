using System.Windows;
using RincoNhan.Tools.ElevationView.ViewModels;

namespace RincoNhan.Tools.ElevationView.UI
{
    public partial class ElevationViewWindow : Window
    {
        public ElevationViewWindow(ElevationViewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestHideWindow = () => Dispatcher.Invoke(() => this.Hide());
            viewModel.RequestShowWindow = () => Dispatcher.Invoke(() => 
            {
                this.Show();
                this.Activate();
            });
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
