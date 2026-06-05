using System.Windows;
using System.Windows.Controls;
using RincoNhan.Tools.ImportEXtoLegend.Models;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    public partial class ImportEXtoLegendWindow : Window
    {
        private ImportEXtoLegendViewModel _viewModel;

        public ImportEXtoLegendWindow(ImportEXtoLegendViewModel viewModel)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e) => SetSelectedHorizontalAlignment(HorizontalAlignmentType.Left);
        private void AlignCenter_Click(object sender, RoutedEventArgs e) => SetSelectedHorizontalAlignment(HorizontalAlignmentType.Center);
        private void AlignRight_Click(object sender, RoutedEventArgs e) => SetSelectedHorizontalAlignment(HorizontalAlignmentType.Right);

        private void AlignTop_Click(object sender, RoutedEventArgs e) => SetSelectedVerticalAlignment(VerticalAlignmentType.Top);
        private void AlignMiddle_Click(object sender, RoutedEventArgs e) => SetSelectedVerticalAlignment(VerticalAlignmentType.Center);
        private void AlignBottom_Click(object sender, RoutedEventArgs e) => SetSelectedVerticalAlignment(VerticalAlignmentType.Bottom);

        private void SetSelectedHorizontalAlignment(HorizontalAlignmentType alignment)
        {
            foreach (var cellInfo in PreviewDataGrid.SelectedCells)
            {
                var row = PreviewDataGrid.Items.IndexOf(cellInfo.Item) + 1;
                var col = cellInfo.Column.DisplayIndex + 1;
                _viewModel.UpdateCellAlignment(row, col, alignment);
            }
            _viewModel.StatusMessage = $"Horizontal alignment: {alignment}";
        }

        private void SetSelectedVerticalAlignment(VerticalAlignmentType alignment)
        {
            foreach (var cellInfo in PreviewDataGrid.SelectedCells)
            {
                var row = PreviewDataGrid.Items.IndexOf(cellInfo.Item) + 1;
                var col = cellInfo.Column.DisplayIndex + 1;
                _viewModel.UpdateCellVerticalAlignment(row, col, alignment);
            }
            _viewModel.StatusMessage = $"Vertical alignment: {alignment}";
        }

        private bool _isDataLoaded = false;
        private void PreviewDataGrid_LayoutUpdated(object sender, System.EventArgs e)
        {
            if (_viewModel == null || PreviewDataGrid.Columns.Count == 0) return;

            // If we just loaded new data, try to apply the widths from the model first
            if (!_isDataLoaded && _viewModel.WorkingData != null)
            {
                var workData = _viewModel.WorkingData;
                for (int i = 0; i < PreviewDataGrid.Columns.Count; i++)
                {
                    if (workData.ColumnWidths.TryGetValue(i + 1, out double widthPt))
                    {
                        // Points to Pixels (approx 1.33)
                        PreviewDataGrid.Columns[i].Width = widthPt * 1.33;
                    }
                }
                _isDataLoaded = true;
                return;
            }

            // Otherwise, sync from UI to Model (user resizing)
            for (int i = 0; i < PreviewDataGrid.Columns.Count; i++)
            {
                var col = PreviewDataGrid.Columns[i];
                double widthPx = col.ActualWidth;
                if (widthPx > 0)
                {
                    _viewModel.UpdateColumnWidth(i + 1, widthPx);
                }
            }
        }
    }
}
