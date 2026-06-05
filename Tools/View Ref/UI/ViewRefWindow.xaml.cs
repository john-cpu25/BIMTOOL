using System.Windows;
using RincoNhan.Tools.ViewRef.ViewModels;

namespace RincoNhan.Tools.ViewRef.UI
{
    public partial class ViewRefWindow : Window
    {
        public ViewRefWindow(ViewRefViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestHideWindow = Hide;
            viewModel.RequestShowWindow = Show;
        }
    }
}
