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
        private List<ElementItem> _allViewItems;
        private List<ElementItem> _allLegendItems;
        private List<ElementItem> _allGroupItems;
        private List<GroupLocationItem> _currentGroupResults = new List<GroupLocationItem>();
        private List<ElementItem> _allGroup3DItems;
        private List<ModelGroup3DLocationItem> _currentGroup3DResults = new List<ModelGroup3DLocationItem>();
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
            // View tab
            _allViewItems = QueryLogic.GetAllViews(_doc);
            ListViewNames.ItemsSource = _allViewItems.Select(v => v.Name).ToList();

            // Legend tab
            _allLegendItems = QueryLogic.GetAllLegends(_doc);
            ListLegendNames.ItemsSource = _allLegendItems.Select(l => l.Name).ToList();

            // Group 2D tab – deduplicated names
            _allGroupItems = QueryLogic.GetAllGroups(_doc);
            ListGroupNames.ItemsSource = _allGroupItems.Select(g => g.Name).ToList();

            // Group 3D tab – deduplicated model group names
            _allGroup3DItems = QueryLogic.GetAllModelGroups3D(_doc);
            ListGroup3DNames.ItemsSource = _allGroup3DItems.Select(g => g.Name).ToList();

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

        // ═══════════════════════════════════════════════════════════════════
        //  VIEW TAB
        // ═══════════════════════════════════════════════════════════════════

        private void TxtViewSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allViewItems == null) return;
            string filter = TxtViewSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ListViewNames.ItemsSource = _allViewItems.Select(v => v.Name).ToList();
            }
            else
            {
                ListViewNames.ItemsSource = _allViewItems
                    .Where(v => v.Name.ToLower().Contains(filter))
                    .Select(v => v.Name)
                    .ToList();
            }
        }

        private void ListViewNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewNames.SelectedItem is string viewName)
            {
                var item = _allViewItems.FirstOrDefault(v => v.Name == viewName);
                if (item != null)
                {
                    var locations = QueryLogic.GetViewLocation(_doc, item.Id);
                    ListViewLocations.ItemsSource = locations;
                    var sheetInfo = locations.FirstOrDefault();
                    TxtViewResultInfo.Text = sheetInfo != null && sheetInfo.IsClickable
                        ? $"Placed on Sheet: {sheetInfo.Name}"
                        : "Placed on Sheet: —";
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  LEGEND TAB
        // ═══════════════════════════════════════════════════════════════════

        private void TxtLegendSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allLegendItems == null) return;
            string filter = TxtLegendSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ListLegendNames.ItemsSource = _allLegendItems.Select(l => l.Name).ToList();
            }
            else
            {
                ListLegendNames.ItemsSource = _allLegendItems
                    .Where(l => l.Name.ToLower().Contains(filter))
                    .Select(l => l.Name)
                    .ToList();
            }
        }

        private void ListLegendNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListLegendNames.SelectedItem is string legendName)
            {
                var item = _allLegendItems.FirstOrDefault(l => l.Name == legendName);
                if (item != null)
                {
                    var locations = QueryLogic.GetLegendLocations(_doc, item.Id);
                    ListLegendLocations.ItemsSource = locations;
                    TxtLegendResultInfo.Text = $"Placed on Sheets: {locations.Count(l => l.IsClickable)} sheet(s)";
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GROUP TAB
        // ═══════════════════════════════════════════════════════════════════

        private void TxtGroupSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allGroupItems == null) return;
            string filter = TxtGroupSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ListGroupNames.ItemsSource = _allGroupItems.Select(g => g.Name).ToList();
            }
            else
            {
                ListGroupNames.ItemsSource = _allGroupItems
                    .Where(g => g.Name.ToLower().Contains(filter))
                    .Select(g => g.Name)
                    .ToList();
            }
        }

        private void ListGroupNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListGroupNames.SelectedItem is string displayName)
            {
                string groupName = QueryLogic.ParseGroupDisplayName(displayName);
                _currentGroupResults = QueryLogic.GetGroupLocationsByName(_doc, groupName);
                ListGroupLocations.ItemsSource = _currentGroupResults;
                TxtGroupResultCount.Text = $"Instances: {_currentGroupResults.Count}";
            }
        }

        private void BtnSelectGroupInModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ElementId groupElemId)
            {
                if (groupElemId == ElementId.InvalidElementId) return;

                _handler.Action = app =>
                {
                    var uidoc = app.ActiveUIDocument;
                    uidoc.Selection.SetElementIds(new List<ElementId> { groupElemId });
                };
                _exEvent.Raise();
            }
        }

        private void BtnSelectAllGroupInModel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGroupResults == null || _currentGroupResults.Count == 0) return;

            var allIds = _currentGroupResults
                .Where(g => g.GroupElementId != null && g.GroupElementId != ElementId.InvalidElementId)
                .Select(g => g.GroupElementId)
                .ToList();

            if (allIds.Count == 0) return;

            _handler.Action = app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(allIds);
            };
            _exEvent.Raise();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GROUP 3D TAB
        // ═══════════════════════════════════════════════════════════════════

        private void TxtGroup3DSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allGroup3DItems == null) return;
            string filter = TxtGroup3DSearch.Text?.Trim().ToLower() ?? "";
            if (string.IsNullOrEmpty(filter))
            {
                ListGroup3DNames.ItemsSource = _allGroup3DItems.Select(g => g.Name).ToList();
            }
            else
            {
                ListGroup3DNames.ItemsSource = _allGroup3DItems
                    .Where(g => g.Name.ToLower().Contains(filter))
                    .Select(g => g.Name)
                    .ToList();
            }
        }

        private void ListGroup3DNames_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListGroup3DNames.SelectedItem is string displayName)
            {
                string groupName = QueryLogic.ParseGroupDisplayName(displayName);
                _currentGroup3DResults = QueryLogic.GetModelGroup3DLocationsByName(_doc, groupName);
                ListGroup3DLocations.ItemsSource = _currentGroup3DResults;
                TxtGroup3DResultCount.Text = $"Instances: {_currentGroup3DResults.Count}";
            }
        }

        private void BtnSelectGroup3DInModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is ElementId groupElemId)
            {
                if (groupElemId == ElementId.InvalidElementId) return;

                _handler.Action = app =>
                {
                    var uidoc = app.ActiveUIDocument;
                    uidoc.Selection.SetElementIds(new List<ElementId> { groupElemId });
                };
                _exEvent.Raise();
            }
        }

        private void BtnSelectAllGroup3DInModel_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGroup3DResults == null || _currentGroup3DResults.Count == 0) return;

            var allIds = _currentGroup3DResults
                .Where(g => g.GroupElementId != null && g.GroupElementId != ElementId.InvalidElementId)
                .Select(g => g.GroupElementId)
                .ToList();

            if (allIds.Count == 0) return;

            _handler.Action = app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(allIds);
            };
            _exEvent.Raise();
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
