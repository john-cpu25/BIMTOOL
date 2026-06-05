using System;
using System.Windows;
using Autodesk.Revit.UI;
using RincoNhan.Tools.WallDivide.ViewModels;

namespace RincoNhan.Tools.WallDivide.UI
{
    public partial class WallDivideWindow : Window
    {
        public WallDivideWindow(UIDocument uidoc)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();

            var handler = new WallDivideExternalEventHandler();
            DataContext = new WallDivideViewModel(handler, uidoc.Document);
        }
    }
}
