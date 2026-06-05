using System.Windows;
using RincoNhan.Tools.RebarColumn.ViewModels;

namespace RincoNhan.Tools.RebarColumn.UI
{
    public partial class ApplyToOtherFloorsWindow : Window
    {
        public ApplyToOtherFloorsWindow()
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


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
