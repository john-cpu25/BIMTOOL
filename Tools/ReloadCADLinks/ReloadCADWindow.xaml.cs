using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ReloadCADLinks
{
    /// <summary>
    /// WPF Window for selecting and reloading multiple CAD links.
    /// </summary>
    public partial class ReloadCADWindow : Window
    {
        private readonly ObservableCollection<CADLinkInfo> _cadLinks;
        private readonly Document _doc;
        private string _selectedFolderPath = null;

        public ReloadCADWindow(List<CADLinkInfo> cadLinks, Document doc)
        {
            InitializeComponent();

            _doc = doc;
            _cadLinks = new ObservableCollection<CADLinkInfo>(cadLinks);
            lvLinks.ItemsSource = _cadLinks;

            UpdateSummary();
        }

        /// <summary>
        /// Updates the summary text showing total and selected count
        /// </summary>
        private void UpdateSummary()
        {
            int total = _cadLinks.Count;
            int selected = _cadLinks.Count(l => l.IsSelected);
            int linksCount = _cadLinks.Count(l => l.IsReloadable);
            int importsCount = total - linksCount;
            int loaded = _cadLinks.Count(l => l.LinkedStatus == LinkedFileStatus.Loaded);
            int notFound = _cadLinks.Count(l => l.LinkedStatus == LinkedFileStatus.NotFound && l.IsReloadable);

            txtSummary.Text = $"Total: {total} CAD (Links: {linksCount}, Imports: {importsCount})  |  " +
                              $"Selected: {selected}  |  " +
                              $"Loaded: {loaded}  |  Not Found: {notFound}";
        }

        /// <summary>
        /// Select all CAD links
        /// </summary>
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var link in _cadLinks)
            {
                link.IsSelected = true;
            }
            UpdateSummary();
        }

        /// <summary>
        /// Deselect all CAD links
        /// </summary>
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var link in _cadLinks)
            {
                link.IsSelected = false;
            }
            UpdateSummary();
        }

        /// <summary>
        /// Handle individual checkbox click to update summary
        /// </summary>
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSummary();
        }

        /// <summary>
        /// Allow user to select a different CAD file to reload for a specific link
        /// </summary>
        private void BtnReloadFrom_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as System.Windows.Controls.Button)?.Tag is CADLinkInfo link))
                return;

            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Title = $"Select CAD file to reload: {link.FileName}";
                dlg.Filter = "CAD Files (*.dwg;*.dxf;*.dgn)|*.dwg;*.dxf;*.dgn|All Files (*.*)|*.*";
                dlg.FileName = link.FileName;

                // Start at the selected folder (if any), or the original folder of the link
                string initialDir = _selectedFolderPath;
                if (string.IsNullOrEmpty(initialDir))
                    initialDir = Path.GetDirectoryName(link.FilePath);
                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                    dlg.InitialDirectory = initialDir;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    link.OverridePath = dlg.FileName;
                    UpdateSummary();
                }
            }
        }

        /// <summary>
        /// Browse for a folder containing CAD files
        /// </summary>
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing CAD files to reload";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _selectedFolderPath = dialog.SelectedPath;
                    txtFolderPath.Text = _selectedFolderPath;
                    txtFolderPath.Foreground = new SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7C8FFF"));
                    txtFolderPath.ToolTip = _selectedFolderPath;
                }
            }
        }

        /// <summary>
        /// Reload selected CAD links
        /// </summary>
        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            var selectedLinks = _cadLinks.Where(l => l.IsSelected).ToList();

            if (selectedLinks.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least 1 CAD link to reload.",
                    "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Confirm reload
            var result = MessageBox.Show(
                $"Are you sure you want to reload {selectedLinks.Count} CAD link(s)?",
                "Confirm Reload",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // Disable UI during reload
            btnReload.IsEnabled = false;
            btnSelectAll.IsEnabled = false;
            btnDeselectAll.IsEnabled = false;
            progressPanel.Visibility = System.Windows.Visibility.Visible;
            progressBar.Value = 0;
            progressBar.Maximum = selectedLinks.Count;

            int successCount = 0;
            int failCount = 0;
            int index = 0;

            using (Transaction trans = new Transaction(_doc, "Reload CAD Links"))
            {
                trans.Start();

                foreach (var link in selectedLinks)
                {
                    index++;
                    txtStatus.Text = $"Reloading ({index}/{selectedLinks.Count}): {link.FileName}...";
                    progressBar.Value = index;

                    // Force UI update
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(delegate { }));

                    try
                    {
                        // Determine the file path to use — priority:
                        // 1. Per-link OverridePath (user chose a specific file for this link)
                        // 2. Folder-level override (match file name in selected folder)
                        // 3. Original FilePath (fallback)
                        string reloadPath = link.FilePath;

                        if (!string.IsNullOrEmpty(link.OverridePath) && File.Exists(link.OverridePath))
                        {
                            reloadPath = link.OverridePath;
                        }
                        else if (!string.IsNullOrEmpty(_selectedFolderPath))
                        {
                            string altPath = Path.Combine(_selectedFolderPath, link.FileName);
                            if (File.Exists(altPath))
                                reloadPath = altPath;
                        }

                        // Check if file exists on disk (Only if not a Cloud path OR if we have an override/alt path)
                        // If it's a Cloud path and no override was provided, we skip the File.Exists check
                        bool isCloudOriginal = link.IsCloudPath && string.Equals(reloadPath, link.FilePath);
                        
                        if (!isCloudOriginal && !File.Exists(reloadPath))
                        {
                            link.Status = "File not found";
                            link.StatusIcon = "";
                            failCount++;
                            continue;
                        }

                        // Get the CADLinkType element
                        CADLinkType cadLinkType = _doc.GetElement(link.TypeId) as CADLinkType;
                        if (cadLinkType == null)
                        {
                            link.Status = "Link type not found";
                            link.StatusIcon = "";
                            failCount++;
                            continue;
                        }

                        // Verify if the link needs to change path
                        bool isNewPath = !string.Equals(reloadPath, link.FilePath, StringComparison.OrdinalIgnoreCase);

                        // Reload the CAD link
                        if (isCloudOriginal && !isNewPath)
                        {
                            // If it's a cloud link and path hasn't changed, just reload
                            cadLinkType.Reload();
                        }
                        else
                        {
                            // Reload from the resolved path (local or cloud)
                            cadLinkType.LoadFrom(reloadPath);
                        }
                        
                        // Update the CAD link Name in Revit if file name changed
                        string newFileName = Path.GetFileName(reloadPath);
                        if (cadLinkType.Name != newFileName)
                        {
                            try { cadLinkType.Name = newFileName; } catch { } // Ignore if name collision
                        }

                        // Update the displayed path if it changed
                        if (isNewPath)
                        {
                            link.FilePath = reloadPath;
                            link.FileName = newFileName;
                        }

                        // Update status
                        link.Status = "Reload successful";
                        link.StatusIcon = "";
                        link.LinkedStatus = LinkedFileStatus.Loaded;
                        link.OverridePath = null; // Clear chosen override path since it's applied
                        successCount++;
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException revitEx)
                    {
                        link.Status = $"Revit Error: {revitEx.Message}";
                        link.StatusIcon = "";
                        failCount++;
                    }
                    catch (Exception ex)
                    {
                        link.Status = $"Error: {ex.Message}";
                        link.StatusIcon = "";
                        failCount++;
                    }
                }

                trans.Commit();
            }

            // Re-enable UI
            btnReload.IsEnabled = true;
            btnSelectAll.IsEnabled = true;
            btnDeselectAll.IsEnabled = true;

            // Update status with results
            string statusMsg = $"✅ Reload complete: {successCount} successful";
            if (failCount > 0)
            {
                statusMsg += $",  ❌ {failCount} failed";
                MessageBox.Show($"{failCount} links failed to reload.\nPlease check the messages in the 'Status' column.\nNote: Revit does not allow reloading to a file that is already linked in the project.", "Reload Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            txtStatus.Text = statusMsg;
            txtStatus.Foreground = failCount > 0
                ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB347"))
                : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6BCB77"));

            UpdateSummary();
        }

        /// <summary>
        /// Close the window
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
