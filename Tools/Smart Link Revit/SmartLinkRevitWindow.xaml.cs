using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;

namespace RincoNhan.Tools.SmartLinkRevit
{
    public partial class SmartLinkRevitWindow : Window
    {
        private Document _doc;
        private List<HostViewInfo> _allHostViews;
        private ObservableCollection<HostViewInfo> _filteredHostViews;
        
        public SmartLinkRevitWindow(Document doc)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            _doc = doc;
            LoadData();
        }

        private void LoadData()
        {
            // 1. Load View Types
            var viewTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .OrderBy(vt => vt.Name)
                .ToList();

            var vtOptions = new List<dynamic> { new { Name = "<All View Types>", Id = ElementId.InvalidElementId } };
            vtOptions.AddRange(viewTypes.Select(vt => new { Name = vt.Name, Id = vt.Id }));
            
            cboViewType.ItemsSource = vtOptions;
            cboViewType.SelectedIndex = 0;

            // 2. Load Host Views (Non-templates)
            _allHostViews = new List<HostViewInfo>();
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted) // typically graphical views
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var v in views)
            {
                _allHostViews.Add(new HostViewInfo
                {
                    Name = v.Name,
                    View = v,
                    IsSelected = false
                });
            }

            _filteredHostViews = new ObservableCollection<HostViewInfo>(_allHostViews);
            lstHostViews.ItemsSource = _filteredHostViews;

            // 3. Load Revit Links
            var linkInstances = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .OrderBy(l => l.Name)
                .ToList();

            var linkOptions = linkInstances.Select(l => new LinkInstanceInfo
            {
                Name = l.Name,
                Instance = l
            }).ToList();

            cboLinks.ItemsSource = linkOptions;
            if (linkOptions.Count > 0) cboLinks.SelectedIndex = 0;
        }

        private void CboViewType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allHostViews == null) return;
            
            dynamic selectedVt = cboViewType.SelectedItem;
            if (selectedVt == null) return;

            _filteredHostViews.Clear();

            ElementId vtId = selectedVt.Id;
            IEnumerable<HostViewInfo> filtered;

            if (vtId == ElementId.InvalidElementId)
            {
                filtered = _allHostViews;
            }
            else
            {
                filtered = _allHostViews.Where(v => v.View.GetTypeId() == vtId);
            }

            foreach (var item in filtered)
            {
                _filteredHostViews.Add(item);
            }
        }

        private void ChkSelectAllViews_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkSelectAllViews.IsChecked == true;
            foreach (var item in _filteredHostViews)
            {
                item.IsSelected = isChecked;
            }
        }

        private void CboLinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateLinkedViews();
        }

        private void DisplayType_Changed(object sender, RoutedEventArgs e)
        {
            if (cboLinkedViews != null)
            {
                cboLinkedViews.IsEnabled = optLinkedView.IsChecked == true || optCustom.IsChecked == true;
            }
        }

        private void UpdateLinkedViews()
        {
            if (cboLinkedViews == null) return;

            cboLinkedViews.ItemsSource = null;

            var selectedLinkInfo = cboLinks.SelectedItem as LinkInstanceInfo;
            if (selectedLinkInfo == null) return;

            Document linkDoc = selectedLinkInfo.Instance.GetLinkDocument();
            if (linkDoc == null)
            {
                // Link might be unloaded
                return;
            }

            // Get views from link doc
            var linkedViews = new FilteredElementCollector(linkDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .OrderBy(v => v.Name)
                .Select(v => new LinkedViewInfo { Name = v.Name, ViewId = v.Id })
                .ToList();

            var options = new List<LinkedViewInfo>
            {
                new LinkedViewInfo { Name = "<None>", ViewId = ElementId.InvalidElementId }
            };
            options.AddRange(linkedViews);

            cboLinkedViews.ItemsSource = options;
            cboLinkedViews.SelectedIndex = 0;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var selectedLinkInfo = cboLinks.SelectedItem as LinkInstanceInfo;
            if (selectedLinkInfo == null)
            {
                MessageBox.Show("Please select a Revit Link.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetViews = _filteredHostViews.Where(v => v.IsSelected).ToList();
            if (targetViews.Count == 0)
            {
                MessageBox.Show("Please select at least one Host View.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

#if NET8_0_OR_GREATER
            LinkVisibility linkVisType = LinkVisibility.ByHostView;
            if (optLinkedView.IsChecked == true) linkVisType = LinkVisibility.ByLinkView;
            if (optCustom.IsChecked == true) linkVisType = LinkVisibility.Custom;

            var selectedLinkedViewInfo = cboLinkedViews.SelectedItem as LinkedViewInfo;
            ElementId linkedViewId = selectedLinkedViewInfo?.ViewId ?? ElementId.InvalidElementId;

            if ((linkVisType == LinkVisibility.ByLinkView || linkVisType == LinkVisibility.Custom) && linkedViewId == ElementId.InvalidElementId)
            {
                MessageBox.Show("Please select a Linked View to apply.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
#else
            MessageBox.Show("This feature requires Revit 2024 or newer.", "Unsupported Version", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
#endif

#if NET8_0_OR_GREATER
            try
            {
                using (Transaction t = new Transaction(_doc, "Apply RVT Link Display Settings"))
                {
                    t.Start();

                    foreach (var hostViewInfo in targetViews)
                    {
                        View hostView = hostViewInfo.View;

                        // Create settings based on host view
                        RevitLinkGraphicsSettings settings;
                        try 
                        {
                            settings = hostView.GetLinkOverrides(selectedLinkInfo.Instance.Id);
                        }
                        catch
                        {
                            settings = new RevitLinkGraphicsSettings();
                        }
                        
                        settings.LinkVisibilityType = linkVisType;
                        if (linkVisType == LinkVisibility.ByLinkView || linkVisType == LinkVisibility.Custom)
                        {
                            try 
                            {
                                settings.LinkedViewId = linkedViewId;
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException)
                            {
                                // Handle case where the linked view is incompatible with the host view type (e.g. Plan vs 3D)
                                continue;
                            }
                        }

                        try 
                        {
                            hostView.SetLinkOverrides(selectedLinkInfo.Instance.Id, settings);
                        }
                        catch (Exception ex)
                        {
                            // In case it's not possible to override this particular link in this view
                            Console.WriteLine(ex.Message);
                        }
                    }

                    t.Commit();
                }

                MessageBox.Show("Display settings applied successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
#endif
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
