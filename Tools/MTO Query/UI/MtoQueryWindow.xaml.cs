using System.Windows;
using Autodesk.Revit.DB;
using RincoNhan.Tools.MTOQuery.ViewModels;

namespace RincoNhan.Tools.MTOQuery.UI
{
    public partial class MtoQueryWindow : Window
    {
        private MtoQueryViewModel _viewModel;

        public MtoQueryWindow(Document doc, View activeView)
        {
            InitializeComponent();
            _viewModel = new MtoQueryViewModel(doc, activeView);
            DataContext = _viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
