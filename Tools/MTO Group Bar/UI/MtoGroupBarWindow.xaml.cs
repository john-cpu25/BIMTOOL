using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.MtoGroupBar.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RincoNhan.Tools.MtoGroupBar.UI
{
    public partial class MtoGroupBarWindow : Window
    {
        private ObservableCollection<MtoGroupItem> _groups;
        private ExternalEvent _exEvent;
        private MtoGroupBarEventHandler _handler;

        public MtoGroupBarWindow(ObservableCollection<MtoGroupItem> groups, ExternalEvent exEvent, MtoGroupBarEventHandler handler)
        {
            // Ensure WPF Application exists (required for XAML resource loading in Revit)

            if (System.Windows.Application.Current == null)

            {

                new System.Windows.Application();

            }


            InitializeComponent();
            _groups = groups;
            _exEvent = exEvent;
            _handler = handler;
            DataGridGroups.ItemsSource = _groups;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DataGridGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridGroups.SelectedItem is MtoGroupItem selectedGroup)
            {
                _handler.Action = app =>
                {
                    app.ActiveUIDocument.Selection.SetElementIds(selectedGroup.ElementIds);
                };
                _exEvent.Raise();
            }
        }

        private void BtnRandomColors_Click(object sender, RoutedEventArgs e)
        {
            // Generate random colors and update UI
            Random rnd = new Random();
            foreach (var group in _groups)
            {
                byte r = (byte)rnd.Next(0, 200); // avoiding pure white/very light colors
                byte g = (byte)rnd.Next(0, 200);
                byte b = (byte)rnd.Next(0, 200);

                group.DisplayColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
                group.RevitColor = new Autodesk.Revit.DB.Color(r, g, b);
            }

            // Apply to Revit
            _handler.Action = app =>
            {
                Document doc = app.ActiveUIDocument.Document;
                View activeView = app.ActiveUIDocument.ActiveView;

                using (Transaction tx = new Transaction(doc, "Override Group Colors"))
                {
                    tx.Start();
                    
                    foreach (var group in _groups)
                    {
                        OverrideGraphicSettings ogs = activeView.GetElementOverrides(group.ElementIds[0]);
                        if (ogs == null) ogs = new OverrideGraphicSettings();

                        ogs.SetProjectionLineColor(group.RevitColor);
                        // Optionally set line weight or other graphics

                        foreach (var id in group.ElementIds)
                        {
                            activeView.SetElementOverrides(id, ogs);
                        }
                    }

                    tx.Commit();
                }
            };
            _exEvent.Raise();
        }
    }
}
