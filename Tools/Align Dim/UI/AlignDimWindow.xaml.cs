using System.Windows;
using RincoNhan.Tools.Align_Dim.ViewModels;

namespace RincoNhan.Tools.Align_Dim.UI
{
    public partial class AlignDimWindow : Window
    {
        public AlignDimWindow(AlignDimViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
