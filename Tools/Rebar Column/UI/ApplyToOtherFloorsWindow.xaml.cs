using System.Windows;
using RincoNhan.Tools.RebarColumn.ViewModels;

namespace RincoNhan.Tools.RebarColumn.UI
{
    public partial class ApplyToOtherFloorsWindow : Window
    {
        public ApplyToOtherFloorsWindow()
        {
            InitializeComponent();
            DataContext = new ApplyToOtherFloorsViewModel();
        }

        private void OnXongClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
