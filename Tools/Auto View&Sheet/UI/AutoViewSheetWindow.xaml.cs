using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RincoNhan.Tools.AutoViewSheet.ViewModels;

namespace RincoNhan.Tools.AutoViewSheet.UI
{
    public partial class AutoViewSheetWindow : Window
    {
        public AutoViewSheetWindow(AutoViewSheetViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetExpandersState(DependencyObject parent, bool isExpanded)
        {
            if (parent == null) return;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Expander expander)
                {
                    expander.IsExpanded = isExpanded;
                }
                
                SetExpandersState(child, isExpanded);
            }
        }

        private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersState(MainDataGrid, true);
        }

        private void BtnCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersState(MainDataGrid, false);
        }

        private void BtnPartExpandAll_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersState(PartDataGrid, true);
        }

        private void BtnPartCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            SetExpandersState(PartDataGrid, false);
        }
    }
}
