using System;
using System.Windows;
using Autodesk.Revit.UI;
using RincoNhan.Tools.JoinElements.ViewModels;

namespace RincoNhan.Tools.JoinElements.UI
{
    public partial class JoinElementsWindow : Window
    {
        public JoinElementsWindow(UIDocument uidoc)
        {
            InitializeComponent();

            var collector = new RevitDataCollector(uidoc.Document, uidoc.Document.ActiveView);
            var handler = new JoinElementsExternalEventHandler();
            
            DataContext = new MainViewModel(collector, handler);
        }
    }
}
