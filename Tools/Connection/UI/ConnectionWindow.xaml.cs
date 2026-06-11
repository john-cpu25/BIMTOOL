using System.Windows;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Connection.ViewModels;

namespace RincoNhan.Tools.Connection.UI
{
    public partial class ConnectionWindow : Window
    {
        public ConnectionWindow(UIDocument uidoc, ExternalEvent externalEvent, ConnectionEventHandler handler)
        {
            InitializeComponent();
            DataContext = new ConnectionViewModel(uidoc, externalEvent, handler);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
