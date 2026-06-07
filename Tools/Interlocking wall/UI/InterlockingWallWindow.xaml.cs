using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using RincoNhan.Tools.InterlockingWall.ViewModels;

namespace RincoNhan.Tools.InterlockingWall.UI
{
    public partial class InterlockingWallWindow : Window
    {
        public InterlockingWallWindow(UIDocument uidoc)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            InitializeComponent();

            var handler = new InterlockingWallExternalEventHandler();
            var viewModel = new InterlockingWallViewModel(handler);
            DataContext = viewModel;

            // Handle Custom radio button click (since we can't use a negation converter easily)
            CustomRadio.Checked += (s, e) =>
            {
                viewModel.IsEqualSplit = false;
            };
        }
    }
}
