using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.QueryElement
{
    public partial class QueryElementWindow : Window
    {
        private Document _doc;
        private ExternalEvent _exEvent;
        private QueryElementEventHandler _handler;
        private List<ElementItem> _cadLinkItems;
        private List<CadLinkLocationItem> _allCadResults = new List<CadLinkLocationItem>();
        private GridViewColumnHeader _lastSortHeader;
        private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;

        public QueryElementWindow(Document doc, ExternalEvent exEvent, QueryElementEventHandler handler)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            _doc = doc;
            _exEvent = exEvent;
            _handler = handler;
            LoadData();
        }

        private void LoadData()
        {
            CmbViews.ItemsSource = QueryLogic.GetAllViews(_doc);
            CmbLegends.ItemsSource = QueryLogic.GetAllLegends(_doc);
            CmbGroups.ItemsSource = QueryLogic.GetAllGroups(_doc);

            // CAD tab
            var templates = QueryLogic.GetViewTemplates(_doc);
            templates.Insert(0, new ElementItem { Id = ElementId.InvalidElementId, Name = "(All - No Template Filter)" });
            CmbCadTemplate.ItemsSource = templates;
            CmbCadTemplate.SelectedIndex = 0;

            _cadLinkItems = QueryLogic.GetAllCadLinks(_doc);
            ListCadFiles.ItemsSource = _cadLinkItems;
        }

        private void CmbCadTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cadLinkItems == null) return;

            ElementId templateId = ElementId.InvalidElementId;
            if (CmbCadTemplate.SelectedItem is ElementItem selectedTemplate)
            {
                templateId = selectedTemplate.Id;
            }

            QueryLogic.UpdateCadVisibilityFromTemplate(_doc, templateId, _cadLinkItems);
            ListCadFiles.Items.Refresh();
        }

        private void CmbViews_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbViews.SelectedItem is ElementItem selectedItem)
            {
                ListViewLocations.ItemsSource = QueryLogic.GetViewLocation(_doc, selectedItem.Id);
            }
        }

        private void CmbLegends_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbLegends.SelectedItem is ElementItem selectedItem)
            {
                ListLegendLocations.ItemsSource = QueryLogic.GetLegendLocations(_doc, selectedItem.Id);
            }
        }

        private void CmbGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbGroups.SelectedItem is ElementItem selectedItem)
            {
                ListGroupLocations.ItemsSource = QueryLogic.GetGroupLocation(_doc, selectedItem.Id);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CAD LINK TAB
        // ═══════════════════════════════════════════════════════════════════

        private void ChkCadFile_Click(object sender, RoutedEventArgs e)
        {
            RefreshCadLocations();
        }

        private void BtnCadSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _cadLinkItems) item.IsSelected = true;
            ListCadFiles.Items.Refresh();
            RefreshCadLocations();
        }

        private void BtnCadSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _cadLinkItems) item.IsSelected = false;
            ListCadFiles.Items.Refresh();
            RefreshCadLocations();
        }

        private void RefreshCadLocations()
        {
            var selected = _cadLinkItems.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ListCadLocations.ItemsSource = null;
                TxtCadResultCount.Text = "Placed in Views:";
                return;
            }

            var allResults = new List<CadLinkLocationItem>();
            foreach (var cad in selected)
            {
                allResults.AddRange(QueryLogic.GetCadLinkLocations(_doc, cad.Name));
            }

            _allCadResults = allResults;
            ApplyCadFilter();
            TxtCadResultCount.Text = $"Placed in Views: {allResults.Count} instance(s) from {selected.Count} file(s)";
        }

        private void TxtCadSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyCadFilter();
        }

        private void ApplyCadFilter()
        {
            string filter = TxtCadSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ListCadLocations.ItemsSource = _allCadResults;
            }
            else
            {
                ListCadLocations.ItemsSource = _allCadResults
                    .Where(r => (r.CadFileName != null && r.CadFileName.ToLower().Contains(filter))
                             || (r.Name != null && r.Name.ToLower().Contains(filter))
                             || (r.LinkType != null && r.LinkType.ToLower().Contains(filter)))
                    .ToList();
            }
        }

        private void ListCadLocations_HeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string headerText = header.Column.Header?.ToString() ?? "";
                string sortBy;
                if (headerText.StartsWith("CAD")) sortBy = "CadFileName";
                else if (headerText.StartsWith("View")) sortBy = "Name";
                else if (headerText.StartsWith("Type")) sortBy = "LinkType";
                else return;

                var direction = (_lastSortHeader == header && _lastSortDirection == ListSortDirection.Ascending)
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

                _lastSortHeader = header;
                _lastSortDirection = direction;

                var view = CollectionViewSource.GetDefaultView(ListCadLocations.ItemsSource);
                if (view != null)
                {
                    view.SortDescriptions.Clear();
                    view.SortDescriptions.Add(new SortDescription(sortBy, direction));
                }
            }
        }

        private void BtnSelectCadInModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ElementId instanceId)
            {
                if (instanceId == ElementId.InvalidElementId) return;

                _handler.Action = app =>
                {
                    var uidoc = app.ActiveUIDocument;
                    uidoc.Selection.SetElementIds(new List<ElementId> { instanceId });
                };
                _exEvent.Raise();
            }
        }

        private void BtnSelectAllCadInModel_Click(object sender, RoutedEventArgs e)
        {
            var selected = _cadLinkItems.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0) return;

            // Collect all ImportInstance element ids for selected CAD files
            var allInstanceIds = new List<ElementId>();
            foreach (var cad in selected)
            {
                var locations = QueryLogic.GetCadLinkLocations(_doc, cad.Name);
                allInstanceIds.AddRange(locations
                    .Where(l => l.InstanceId != null && l.InstanceId != ElementId.InvalidElementId)
                    .Select(l => l.InstanceId));
            }

            if (allInstanceIds.Count == 0) return;

            _handler.Action = app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(allInstanceIds);
            };
            _exEvent.Raise();
        }

        // ═══════════════════════════════════════════════════════════════════

        private void BtnLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ElementId targetId)
            {
                if (targetId == ElementId.InvalidElementId) return;

                _handler.Action = app =>
                {
                    var targetView = app.ActiveUIDocument.Document.GetElement(targetId) as View;
                    if (targetView != null)
                    {
                        app.ActiveUIDocument.ActiveView = targetView;
                    }
                    else
                    {
                        TaskDialog.Show("Error", "Could not find or activate the target View/Sheet.");
                    }
                };
                
                _exEvent.Raise();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
