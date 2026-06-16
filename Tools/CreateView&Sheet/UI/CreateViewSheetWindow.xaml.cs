using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using RincoNhan.Tools.CreateViewSheet.ViewModels;

namespace RincoNhan.Tools.CreateViewSheet.UI
{
    public partial class CreateViewSheetWindow : Window
    {
        // Track last clicked row index per DataGrid
        private Dictionary<DataGrid, int> _lastClickedIndex = new Dictionary<DataGrid, int>();

        public CreateViewSheetWindow(CreateViewSheetViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// Shift+Click handler for CheckBox inside DataGrid rows.
        /// Toggles IsSelected for all rows between last click and current click.
        /// Attach to CheckBox.Click in all DataGrid templates.
        /// </summary>
        public void CheckBox_ShiftClick(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            // Find the parent DataGrid
            var dataGrid = FindParentDataGrid(checkBox);
            if (dataGrid == null) return;

            // Get current row index
            var row = FindParentDataGridRow(checkBox);
            if (row == null) return;

            int currentIndex = row.GetIndex();
            bool isChecked = checkBox.IsChecked == true;

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                if (_lastClickedIndex.ContainsKey(dataGrid))
                {
                    int lastIndex = _lastClickedIndex[dataGrid];
                    int start = System.Math.Min(lastIndex, currentIndex);
                    int end = System.Math.Max(lastIndex, currentIndex);

                    // Get items from the DataGrid's ItemsSource
                    var items = dataGrid.Items;
                    for (int i = start; i <= end; i++)
                    {
                        if (i >= 0 && i < items.Count)
                        {
                            var item = items[i];
                            // Use reflection to set IsSelected
                            var prop = item.GetType().GetProperty("IsSelected");
                            if (prop != null && prop.CanWrite)
                            {
                                prop.SetValue(item, isChecked);
                            }
                        }
                    }
                }
            }

            _lastClickedIndex[dataGrid] = currentIndex;
        }

        private DataGrid FindParentDataGrid(DependencyObject child)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is DataGrid dg) return dg;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private DataGridRow FindParentDataGridRow(DependencyObject child)
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is DataGridRow row) return row;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }

    /// <summary>
    /// Converter for binding RadioButton.IsChecked to a string property.
    /// Returns true when the bound value equals the ConverterParameter.
    /// </summary>
    public class StringMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return parameter?.ToString();
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
