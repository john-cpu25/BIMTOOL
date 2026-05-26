using System.Windows;
using RincoNhan.Tools.ClashDetection.ViewModels;

namespace RincoNhan.Tools.ClashDetection.UI
{
    public partial class ClashDetectionWindow : Window
    {
        public ClashDetectionWindow(ClashDetectionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
