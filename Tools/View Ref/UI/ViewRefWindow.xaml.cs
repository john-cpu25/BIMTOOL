using System.Windows;
using RincoNhan.Tools.ViewRef.ViewModels;

namespace RincoNhan.Tools.ViewRef.UI
{
    public partial class ViewRefWindow : Window
    {
        public ViewRefWindow(ViewRefViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestHideWindow = Hide;
            viewModel.RequestShowWindow = Show;
        }
    }
}
