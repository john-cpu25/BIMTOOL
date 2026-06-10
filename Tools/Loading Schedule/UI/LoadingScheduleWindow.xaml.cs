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
        
        private readonly ExternalEvent _dupExternalEvent;
        private readonly DuplicateLegendHandler _dupHandler;

        private readonly ExternalEvent _updExternalEvent;
        private readonly UpdateLegendHandler _updHandler;

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

            _dupHandler = new DuplicateLegendHandler();
            _dupExternalEvent = ExternalEvent.Create(_dupHandler);

            _updHandler = new UpdateLegendHandler();
            _updExternalEvent = ExternalEvent.Create(_updHandler);

            _viewModel = new LoadingScheduleViewModel(doc, activeView);
            this.DataContext = _viewModel;

            this.Loaded += Window_Loaded;

            // ─── Loading Schedule Callbacks ───
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

            _handler.LogMessage = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    txtLog.AppendText($"[{timestamp}] {msg}\n");
                    txtLog.ScrollToEnd();
                });
            };

            // ─── Duplicate Legend Callbacks ───
            _dupHandler.Completed = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    dupTxtStatus.Text = msg;
                    dupProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                    dupTxtPercentage.Visibility = System.Windows.Visibility.Collapsed;
                    dupBtnRun.IsEnabled = true;
                    dupBtnRun.Content = "Run Duplicate";

                    if (msg.StartsWith("Success"))
                    {
                        dupTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(46, 125, 50));
                        MessageBox.Show(msg, "Duplicate Legend", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (msg.StartsWith("Error"))
                    {
                        dupTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(198, 40, 40));
                        MessageBox.Show(msg, "Duplicate Legend", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            };

            _dupHandler.LogMessage = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    dupTxtLog.AppendText($"[{timestamp}] {msg}\n");
                    dupTxtLog.ScrollToEnd();
                });
            };

            _dupHandler.ProgressChanged = (percentage) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    dupProgressBar.Value = percentage;
                    dupTxtPercentage.Text = $"{percentage}%";
                });
            };
            _updHandler.ProgressChanged = (percentage) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    updProgressBar.Value = percentage;
                    updTxtPercentage.Text = $"{percentage}%";
                });
            };

            _updHandler.Completed = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    updTxtStatus.Text = msg;
                    updProgressBar.Visibility = System.Windows.Visibility.Collapsed;
                    updTxtPercentage.Visibility = System.Windows.Visibility.Collapsed;
                    updBtnRun.IsEnabled = true;
                    updBtnRun.Content = "Run Update";

                    if (msg.StartsWith("Success"))
                    {
                        updTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(46, 125, 50));
                        MessageBox.Show(msg, "Update Legend", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (msg.StartsWith("Error"))
                    {
                        updTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(198, 40, 40));
                        MessageBox.Show(msg, "Update Legend", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            };

            _updHandler.LogMessage = (msg) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    updTxtLog.AppendText($"[{timestamp}] {msg}\n");
                    updTxtLog.ScrollToEnd();
                });
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _viewModel.InitializeData();
                }
                finally
                {
                    System.Windows.Input.Mouse.OverrideCursor = null;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Loading Schedule Tab Handlers ───
        private void LegendMode_Changed(object sender, RoutedEventArgs e)
        {
            if (txtLegendName == null || cmbTargetLegend == null) return;
            bool createNew = rbCreateNew.IsChecked == true;
            txtLegendName.IsEnabled = createNew;
            cmbTargetLegend.IsEnabled = !createNew;
        }

        private void CreateLegend_Click(object sender, RoutedEventArgs e)
        {
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

            txtLog.Clear();
            txtStatus.Text = "Creating legend...";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(117, 117, 117));
            progressBar.Visibility = System.Windows.Visibility.Visible;
            btnCreate.IsEnabled = false;
            btnCreate.Content = "Creating...";

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

        // ─── Duplicate Legend Tab Handlers ───
        private void SelectAllSheets_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in _viewModel.DuplicateLegendViewModel.Sheets)
                sheet.IsSelected = true;
            dgSheets.Items.Refresh();
        }

        private void DeselectAllSheets_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in _viewModel.DuplicateLegendViewModel.Sheets)
                sheet.IsSelected = false;
            dgSheets.Items.Refresh();
        }

        private void DupToggleLog_Click(object sender, RoutedEventArgs e)
        {
            if (dupLogPanel.Visibility == System.Windows.Visibility.Visible)
            {
                dupLogPanel.Visibility = System.Windows.Visibility.Collapsed;
                dupBtnToggleLog.Content = "▶ Show Log";
            }
            else
            {
                dupLogPanel.Visibility = System.Windows.Visibility.Visible;
                dupBtnToggleLog.Content = "▼ Hide Log";
            }
        }

        private void DupClearLog_Click(object sender, RoutedEventArgs e)
        {
            dupTxtLog.Clear();
        }

        private void RunDuplicate_Click(object sender, RoutedEventArgs e)
        {
            var dupVm = _viewModel.DuplicateLegendViewModel;
            if (dupVm.SelectedLegend == null)
            {
                MessageBox.Show("Please select a Source Legend.", "Duplicate Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedSheets = dupVm.Sheets.Where(s => s.IsSelected).Select(s => s.SheetId).ToList();
            if (!selectedSheets.Any())
            {
                MessageBox.Show("Please select at least one Target Sheet.", "Duplicate Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _dupHandler.SourceLegendId = dupVm.SelectedLegend.Id;
            _dupHandler.TargetSheetIds = selectedSheets;

            dupTxtLog.Clear();
            dupTxtStatus.Text = "Duplicating legend...";
            dupTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(117, 117, 117));
            dupProgressBar.Value = 0;
            dupProgressBar.Visibility = System.Windows.Visibility.Visible;
            dupTxtPercentage.Text = "0%";
            dupTxtPercentage.Visibility = System.Windows.Visibility.Visible;
            dupBtnRun.IsEnabled = false;
            dupBtnRun.Content = "Duplicating...";

            dupLogPanel.Visibility = System.Windows.Visibility.Visible;
            dupBtnToggleLog.Content = "▼ Hide Log";

            _dupExternalEvent.Raise();
        }

        // ─── Update Legend Tab Handlers ───
        private void SelectAllUpdateSheets_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in _viewModel.UpdateLegendViewModel.Sheets)
                sheet.IsSelected = true;
            dgUpdateSheets.Items.Refresh();
        }

        private void DeselectAllUpdateSheets_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sheet in _viewModel.UpdateLegendViewModel.Sheets)
                sheet.IsSelected = false;
            dgUpdateSheets.Items.Refresh();
        }

        private void UpdToggleLog_Click(object sender, RoutedEventArgs e)
        {
            if (updLogPanel.Visibility == System.Windows.Visibility.Visible)
            {
                updLogPanel.Visibility = System.Windows.Visibility.Collapsed;
                updBtnToggleLog.Content = "▶ Show Log";
            }
            else
            {
                updLogPanel.Visibility = System.Windows.Visibility.Visible;
                updBtnToggleLog.Content = "▼ Hide Log";
            }
        }

        private void UpdClearLog_Click(object sender, RoutedEventArgs e)
        {
            updTxtLog.Clear();
        }

        private void RunUpdate_Click(object sender, RoutedEventArgs e)
        {
            var updVm = _viewModel.UpdateLegendViewModel;
            if (updVm.SelectedTemplateLegend == null)
            {
                MessageBox.Show("Please select a Template Legend.", "Update Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedSheets = updVm.Sheets.Where(s => s.IsSelected).ToList();
            if (!selectedSheets.Any())
            {
                MessageBox.Show("Please select at least one Target Sheet.", "Update Legend", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _updHandler.TemplateLegendId = updVm.SelectedTemplateLegend.Id;
            _updHandler.Jobs.Clear();

            foreach (var sheet in selectedSheets)
            {
                var job = new UpdateLegendHandler.UpdateJob
                {
                    TargetLegendId = sheet.TargetLegendId,
                    SourceViewId = sheet.SelectedView?.Id ?? ElementId.InvalidElementId
                };
                _updHandler.Jobs.Add(job);
            }

            updTxtLog.Clear();
            updTxtStatus.Text = "Updating legends...";
            updTxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(117, 117, 117));
            updProgressBar.Value = 0;
            updProgressBar.Visibility = System.Windows.Visibility.Visible;
            updTxtPercentage.Text = "0%";
            updTxtPercentage.Visibility = System.Windows.Visibility.Visible;
            updBtnRun.IsEnabled = false;
            updBtnRun.Content = "Updating...";

            updLogPanel.Visibility = System.Windows.Visibility.Visible;
            updBtnToggleLog.Content = "▼ Hide Log";

            _updExternalEvent.Raise();
        }
    }
}
