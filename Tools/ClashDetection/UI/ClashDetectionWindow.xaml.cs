using System.Windows;
using RincoNhan.Tools.ClashDetection.ViewModels;

namespace RincoNhan.Tools.ClashDetection.UI
{
    public partial class ClashDetectionWindow : Window
    {
        public ClashDetectionWindow(ClashDetectionViewModel viewModel)
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
