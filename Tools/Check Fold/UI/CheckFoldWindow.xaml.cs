using System.Windows;
using System.Windows.Controls;
using RincoModeling.Tools.CheckFold.Models;
using RincoModeling.Tools.CheckFold.ViewModels;

namespace RincoModeling.Tools.CheckFold.UI
{
    public partial class CheckFoldWindow : Window
    {
        public CheckFoldWindow(CheckFoldHandler handler)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            InitializeComponent();
            this.DataContext = new CheckFoldViewModel(handler);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var vm = this.DataContext as CheckFoldViewModel;
            if (vm == null) return;

            var grid = sender as DataGrid;
            if (grid?.SelectedItem == null) return;

            if (grid.SelectedItem is StepCheckItem step)
            {
                vm.SelectElementInRevit(step.StepFamilyId);
            }
            else if (grid.SelectedItem is FoldCheckItem fold)
            {
                vm.SelectElementInRevit(fold.FoldFloorId);
            }
        }
    }
}

