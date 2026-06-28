using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;
using RincoNhan.Tools.StairDetail.ViewModels;

namespace RincoNhan.Tools.StairDetail.UI
{
    public partial class StairDetailWindow : Window
    {
        public StairDetailWindow(List<Element> rebarTypes)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            InitializeComponent();
            this.DataContext = new StairDetailViewModel(rebarTypes);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as StairDetailViewModel;
            vm?.SaveSettings();
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
