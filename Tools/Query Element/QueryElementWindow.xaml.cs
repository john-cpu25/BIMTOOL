using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.QueryElement
{
    public partial class QueryElementWindow : Window
    {
        private Document _doc;
        private ExternalEvent _exEvent;
        private QueryElementEventHandler _handler;

        public QueryElementWindow(Document doc, ExternalEvent exEvent, QueryElementEventHandler handler)
        {
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
