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
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


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
