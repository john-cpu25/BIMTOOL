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
            InitializeComponent();

            var handler = new WallDivideExternalEventHandler();
            DataContext = new WallDivideViewModel(handler, uidoc.Document);
        }
    }
}
