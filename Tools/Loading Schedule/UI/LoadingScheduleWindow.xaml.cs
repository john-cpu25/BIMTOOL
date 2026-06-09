using System;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingSchedule.ViewModels;

namespace RincoNhan.Tools.LoadingSchedule.UI
{
    public partial class LoadingScheduleWindow : Window
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private readonly LoadingScheduleHandler _handler;
        private readonly LoadingScheduleViewModel _viewModel;

        public LoadingScheduleWindow(Document doc, View activeView)
        {
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            InitializeComponent();

            _doc = doc;
            _handler = new LoadingScheduleHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            _viewModel = new LoadingScheduleViewModel(doc, activeView);
            this.DataContext = _viewModel;

            // Status callback
            _handler.NotifyStatus = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    txtStatus.Text = msg;
                    progressBar.Visibility = System.Windows.Visibility.Collapsed;
                    btnCreate.IsEnabled = true;
                    btnCreate.Content = "Create Legend";

                    if (msg.StartsWith("Success"))
                    {
                        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(46, 125, 50));
                        MessageBox.Show(msg, "Loading Schedule", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (msg.StartsWith("Error"))
                    {
                        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(198, 40, 40));
                        MessageBox.Show(msg, "Loading Schedule", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            };

            // Debug log callback
            _handler.LogMessage = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    txtLog.AppendText($"[{timestamp}] {msg}\n");
                    txtLog.ScrollToEnd();
                });
            };
        }

        private void LegendMode_Changed(object sender, RoutedEventArgs e)
        {
            if (txtLegendName == null || cmbTargetLegend == null) return;
            bool createNew = rbCreateNew.IsChecked == true;
            txtLegendName.IsEnabled = createNew;
            cmbTargetLegend.IsEnabled = !createNew;
        }

        private void CreateLegend_Click(object sender, RoutedEventArgs e)
        {
            // Validate template selection
            if (_viewModel.SelectedTemplateLegend == null)
            {
                MessageBox.Show("Please select a Template Legend view.", "Loading Schedule",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool createNew = rbCreateNew.IsChecked == true;

            if (createNew)
            {
                string legendName = txtLegendName.Text?.Trim();
                if (string.IsNullOrEmpty(legendName))
                {
                    MessageBox.Show("Please enter a name for the new Legend view.", "Loading Schedule",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _handler.CreateNewLegend = true;
                _handler.NewLegendName = legendName;
                _handler.TargetLegendId = ElementId.InvalidElementId;
            }
            else
            {
                if (_viewModel.SelectedTargetLegend == null)
                {
                    MessageBox.Show("Please select a target Legend View.", "Loading Schedule",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Don't allow using template as target
                if (_viewModel.SelectedTargetLegend.Id == _viewModel.SelectedTemplateLegend.Id)
                {
                    MessageBox.Show("Target Legend cannot be the same as the Template Legend.\n" +
                                    "Please select a different target or create a new one.",
                        "Loading Schedule", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _handler.CreateNewLegend = false;
                _handler.NewLegendName = null;
                _handler.TargetLegendId = _viewModel.SelectedTargetLegend.Id;
            }

            var selectedItems = _viewModel.GetSelectedItems();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Please select at least one hatch type.", "Loading Schedule",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _handler.TemplateLegendId = _viewModel.SelectedTemplateLegend.Id;
            _handler.Items = selectedItems;

            // Clear log and start
            txtLog.Clear();
            txtStatus.Text = "Creating legend...";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(117, 117, 117));
            progressBar.Visibility = System.Windows.Visibility.Visible;
            btnCreate.IsEnabled = false;
            btnCreate.Content = "Creating...";

            // Auto-show log panel
            logPanel.Visibility = System.Windows.Visibility.Visible;
            btnToggleLog.Content = "▼ Hide Log";

            _externalEvent.Raise();
        }

        private void ToggleLog_Click(object sender, RoutedEventArgs e)
        {
            if (logPanel.Visibility == System.Windows.Visibility.Visible)
            {
                logPanel.Visibility = System.Windows.Visibility.Collapsed;
                btnToggleLog.Content = "▶ Show Log";
            }
            else
            {
                logPanel.Visibility = System.Windows.Visibility.Visible;
                btnToggleLog.Content = "▼ Hide Log";
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = dgItems.SelectedIndex;
            if (index > 0)
            {
                _viewModel.MoveUp(index);
                dgItems.SelectedIndex = index - 1;
            }
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = dgItems.SelectedIndex;
            if (index >= 0 && index < _viewModel.Items.Count - 1)
            {
                _viewModel.MoveDown(index);
                dgItems.SelectedIndex = index + 1;
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _viewModel.Items)
                item.IsSelected = true;
            dgItems.Items.Refresh();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _viewModel.Items)
                item.IsSelected = false;
            dgItems.Items.Refresh();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
