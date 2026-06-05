using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;

namespace RincoNhan.Tools.SmartLinkCad
{
    public partial class SmartLinkCadWindow : Window
    {
        private Document _doc;
        private SmartLinkCadEventHandler _handler;
        private List<CADCategoryInfo> _cadList;
        private List<View> _viewTemplates;
        private Autodesk.Revit.DB.Color _currentColor = new Autodesk.Revit.DB.Color(80, 200, 150);

        // Tab 2
        private List<CADLayerInfo> _allLayers;
        private ObservableCollection<CADLayerInfo> _filteredLayers;
        private Autodesk.Revit.DB.Color _layerColor = new Autodesk.Revit.DB.Color(80, 200, 150);

        // Presets
        private LayerPresetManager _presetManager;

        public SmartLinkCadWindow(Document doc, SmartLinkCadEventHandler handler)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            InitializeComponent();
            _doc = doc;
            _handler = handler;
            _handler.Doc = doc;
            _handler.StatusCallback = UpdateStatus;

            LoadData();
            LoadLayerData();

            // Initialize presets
            try
            {
                _presetManager = new LayerPresetManager(doc.PathName);
                RefreshPresetList();
            }
            catch { }

            // Set initial color display
            brdColor.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(_currentColor.Red, _currentColor.Green, _currentColor.Blue));
            brdLayerColor.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(_layerColor.Red, _layerColor.Green, _layerColor.Blue));
        }

        private void UpdateStatus(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                if (txtStatusBar != null) txtStatusBar.Text = msg;
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SHARED DATA
        // ═══════════════════════════════════════════════════════════════════════

        private void LoadData()
        {
            // 1. Load View Templates only
            _viewTemplates = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

            cboViewTemplates.ItemsSource = _viewTemplates;

            if (_doc.ActiveView.ViewTemplateId != ElementId.InvalidElementId)
            {
                cboViewTemplates.SelectedValue = _doc.ActiveView.ViewTemplateId;
            }

            // 2. Load Line Weights
            var weights = new List<string> { "<No Override>" };
            for (int i = 1; i <= 16; i++) weights.Add(i.ToString());
            cboWeight.ItemsSource = weights;
            cboWeight.SelectedIndex = 0;
            cboLayerWeight.ItemsSource = weights.ToList();
            cboLayerWeight.SelectedIndex = 0;

            // 3. Load Line Patterns
            var patterns = new FilteredElementCollector(_doc)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .OrderBy(p => p.Name)
                .Select(p => new { Name = p.Name, Id = p.Id })
                .ToList();

            var patternOptions = new List<dynamic>();
            patternOptions.Add(new { Name = "<No Override>", Id = ElementId.InvalidElementId });
            patternOptions.AddRange(patterns);

            cboPattern.ItemsSource = patternOptions;
            cboPattern.SelectedIndex = 0;

            var patternOptions2 = new List<dynamic>();
            patternOptions2.Add(new { Name = "<No Override>", Id = ElementId.InvalidElementId });
            patternOptions2.AddRange(patterns);
            cboLayerPattern.ItemsSource = patternOptions2;
            cboLayerPattern.SelectedIndex = 0;

            // 4. Load CAD categories
            _cadList = new List<CADCategoryInfo>();

            var imports = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>();

            var cadCategories = new Dictionary<ElementId, Category>();
            foreach (var imp in imports)
            {
                if (imp.Category != null && !cadCategories.ContainsKey(imp.Category.Id))
                {
                    cadCategories.Add(imp.Category.Id, imp.Category);
                }
            }

            foreach (var kvp in cadCategories)
            {
                _cadList.Add(new CADCategoryInfo
                {
                    Category = kvp.Value,
                    Name = kvp.Value.Name,
                    IsSelected = false
                });
            }

            _cadList = _cadList.OrderBy(c => c.Name).ToList();
            lstCadFiles.ItemsSource = _cadList;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TAB 2: LAYER DATA
        // ═══════════════════════════════════════════════════════════════════════

        private void LoadLayerData()
        {
            _allLayers = new List<CADLayerInfo>();

            foreach (var cad in _cadList)
            {
                // Add each sub-category (layer) of the CAD file
                foreach (Category subCat in cad.Category.SubCategories)
                {
                    _allLayers.Add(new CADLayerInfo
                    {
                        LayerName = subCat.Name,
                        CadFileName = cad.Name,
                        SubCategory = subCat,
                        ParentCategory = cad.Category,
                        IsVisible = true
                    });
                }
            }

            _allLayers = _allLayers.OrderBy(l => l.CadFileName).ThenBy(l => l.LayerName).ToList();

            // Read current visibility state from selected View Template
            var selectedView = cboViewTemplates.SelectedItem as View;
            if (selectedView != null)
            {
                foreach (var layer in _allLayers)
                {
                    if (layer.SubCategory == null) continue;
                    try
                    {
                        bool isHidden = selectedView.GetCategoryHidden(layer.SubCategory.Id);
                        layer.IsVisible = !isHidden;
                    }
                    catch { }
                }
            }

            // Populate CAD filter list (multi-select) — select all by default
            foreach (var cad in _cadList)
            {
                cad.IsSelected = true;
            }
            lstCadFilter.ItemsSource = _cadList;

            _filteredLayers = new ObservableCollection<CADLayerInfo>(_allLayers);
            lstLayers.ItemsSource = _filteredLayers;
        }

        private void ChkCadFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyLayerFilters();
        }

        private void BtnCadFilterAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cad in _cadList)
                cad.IsSelected = true;
            ApplyLayerFilters();
        }

        private void BtnCadFilterNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cad in _cadList)
                cad.IsSelected = false;
            ApplyLayerFilters();
        }

        private void TxtLayerFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyLayerFilters();
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            txtLayerFilter.Text = "";
        }

        /// <summary>
        /// Central filter method: combines multi-select CAD file filter + layer name search.
        /// </summary>
        private void ApplyLayerFilters()
        {
            if (_allLayers == null || _filteredLayers == null) return;

            // 1. Filter by selected CAD files (multi-select)
            var selectedCadNames = _cadList.Where(c => c.IsSelected).Select(c => c.Name).ToHashSet();
            IEnumerable<CADLayerInfo> source;

            if (selectedCadNames.Count == 0 || selectedCadNames.Count == _cadList.Count)
            {
                source = _allLayers;
            }
            else
            {
                source = _allLayers.Where(l => selectedCadNames.Contains(l.CadFileName));
            }

            int totalForCad = source.Count();

            // 2. Filter by layer name (search text)
            string searchText = txtLayerFilter?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(searchText))
            {
                source = source.Where(l =>
                    l.LayerName != null &&
                    l.LayerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 3. Update collection
            _filteredLayers.Clear();
            foreach (var layer in source)
            {
                _filteredLayers.Add(layer);
            }

            // 4. Update count label
            if (txtLayerCount != null)
            {
                if (!string.IsNullOrEmpty(searchText))
                    txtLayerCount.Text = $"Showing: {_filteredLayers.Count} / {totalForCad} layers";
                else
                    txtLayerCount.Text = $"{totalForCad} layers";
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TAB 1: EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        private void BtnPickColor_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog())
            {
                dialog.Color = System.Drawing.Color.FromArgb(_currentColor.Red, _currentColor.Green, _currentColor.Blue);
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _currentColor = new Autodesk.Revit.DB.Color(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    brdColor.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
                }
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = _cadList.All(c => c.IsSelected);
            foreach (var item in _cadList)
            {
                item.IsSelected = !allSelected;
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selectedView = cboViewTemplates.SelectedItem as View;
            if (selectedView == null)
            {
                MessageBox.Show("Please select a View Template.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCads = _cadList.Where(c => c.IsSelected).ToList();
            if (selectedCads.Count == 0)
            {
                MessageBox.Show("Please select at least one CAD file to override.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set handler data and raise event
            _handler.RequestAction = "BATCH_OVERRIDE";
            _handler.TargetView = selectedView;
            _handler.SelectedCads = selectedCads;
            _handler.OverrideColor = _currentColor;
            _handler.Halftone = chkHalftone.IsChecked == true;
            _handler.WeightIndex = cboWeight.SelectedIndex;
            _handler.PatternId = cboPattern.SelectedIndex > 0 && cboPattern.SelectedValue is ElementId pid
                ? pid : ElementId.InvalidElementId;
            _handler.Raise();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  TAB 2: EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        private void BtnPickLayerColor_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.ColorDialog())
            {
                dialog.Color = System.Drawing.Color.FromArgb(_layerColor.Red, _layerColor.Green, _layerColor.Blue);
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _layerColor = new Autodesk.Revit.DB.Color(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    brdLayerColor.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
                }
            }
        }

        private void ChkSelectAllLayers_Click(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            bool isChecked = chk?.IsChecked == true;
            foreach (var layer in _filteredLayers)
            {
                layer.IsVisible = isChecked;
            }
        }

        private void BtnApplyVisibility_Click(object sender, RoutedEventArgs e)
        {
            var selectedView = cboViewTemplates.SelectedItem as View;
            if (selectedView == null)
            {
                MessageBox.Show("Please select a View Template.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _handler.RequestAction = "APPLY_VISIBILITY";
            _handler.TargetView = selectedView;
            _handler.AllLayers = _allLayers;
            _handler.Raise();
        }

        private void BtnApplyLayerOverride_Click(object sender, RoutedEventArgs e)
        {
            var selectedView = cboViewTemplates.SelectedItem as View;
            if (selectedView == null)
            {
                MessageBox.Show("Please select a View Template.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetLayers = _filteredLayers.Where(l => l.IsVisible).ToList();
            if (targetLayers.Count == 0)
            {
                MessageBox.Show("No visible layers selected. Check layers you want to override.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _handler.RequestAction = "LAYER_OVERRIDE";
            _handler.TargetView = selectedView;
            _handler.TargetLayers = targetLayers;
            _handler.OverrideColor = _layerColor;
            _handler.Halftone = chkLayerHalftone.IsChecked == true;
            _handler.WeightIndex = cboLayerWeight.SelectedIndex;
            _handler.PatternId = cboLayerPattern.SelectedIndex > 0 && cboLayerPattern.SelectedValue is ElementId pid
                ? pid : ElementId.InvalidElementId;
            _handler.Raise();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PRESETS
        // ═══════════════════════════════════════════════════════════════════════

        private void RefreshPresetList()
        {
            if (_presetManager == null) return;
            var presets = _presetManager.GetPresets();
            cboPresets.ItemsSource = presets;
            if (presets.Count > 0)
                cboPresets.SelectedIndex = 0;
        }

        private void CboPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var preset = cboPresets.SelectedItem as LayerPreset;
            if (preset != null)
            {
                txtPresetName.Text = preset.Name;
                txtPresetInfo.Text = $"Created: {preset.CreatedAt:dd/MM/yyyy HH:mm} | {preset.HiddenLayers.Count} hidden layers";
            }
            else
            {
                txtPresetInfo.Text = "";
            }
        }

        private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            string name = txtPresetName?.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a preset name.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_presetManager == null || _allLayers == null) return;

            _presetManager.SavePreset(name, _allLayers);
            RefreshPresetList();

            int hiddenCount = _allLayers.Count(l => !l.IsVisible);
            UpdateStatus($"💾 Preset '{name}' saved ({hiddenCount} hidden layers).");
        }

        private void BtnLoadPreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = cboPresets.SelectedItem as LayerPreset;
            if (preset == null)
            {
                MessageBox.Show("Please select a preset to load.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_presetManager == null || _allLayers == null) return;

            _presetManager.ApplyPreset(preset, _allLayers);
            ApplyLayerFilters(); // Refresh UI

            int hiddenCount = _allLayers.Count(l => !l.IsVisible);
            UpdateStatus($"📂 Preset '{preset.Name}' loaded ({hiddenCount} hidden layers).");
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = cboPresets.SelectedItem as LayerPreset;
            if (preset == null)
            {
                MessageBox.Show("Please select a preset to delete.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_presetManager == null) return;

            var result = MessageBox.Show($"Delete preset '{preset.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _presetManager.DeletePreset(preset.Name);
                RefreshPresetList();
                txtPresetName.Text = "";
                txtPresetInfo.Text = "";
                UpdateStatus($"🗑 Preset '{preset.Name}' deleted.");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SHARED
        // ═══════════════════════════════════════════════════════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
