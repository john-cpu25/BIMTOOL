using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RincoNhan.Tools.TwoDFamilyManager.Models;
using RincoNhan.Tools.TwoDFamilyManager.ViewModels;

namespace RincoNhan.Tools.TwoDFamilyManager.UI
{
    public partial class FamilyManagerWindow : Window
    {
        public FamilyManagerWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FamilyTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.DataContext is FamilyManagerViewModel vm)
            {
                if (e.NewValue is FamilySymbolItem symbolItem)
                {
                    vm.SelectedSymbol = symbolItem;
                }
                else
                {
                    vm.SelectedSymbol = null;
                }
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is TreeViewItem tvi && tvi.Header is FamilySymbolItem)
                {
                    if (this.DataContext is FamilyManagerViewModel vm && vm.PlaceCommand.CanExecute(null))
                    {
                        vm.PlaceCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is FamilyManagerViewModel vm)
            {
                foreach (var cat in vm.Categories)
                {
                    cat.IsExpanded = true;
                    foreach (var fam in cat.Families)
                    {
                        fam.IsExpanded = true;
                    }
                }
            }
        }

        private void CollapseSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = FamilyTreeView.SelectedItem;
            if (selectedItem is FamilyCategoryItem cat)
            {
                cat.IsExpanded = false;
                foreach (var fam in cat.Families)
                {
                    fam.IsExpanded = false;
                }
            }
            else if (selectedItem is FamilyModelItem famItem)
            {
                famItem.IsExpanded = false;
            }
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is FamilyManagerViewModel vm)
            {
                foreach (var cat in vm.Categories)
                {
                    cat.IsExpanded = false;
                    foreach (var fam in cat.Families)
                    {
                        fam.IsExpanded = false;
                    }
                }
            }
        }
    }
}
