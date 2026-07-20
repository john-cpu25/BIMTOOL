using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RincoNhan.Tools.FillRegionManager
{
    public partial class FillRegionManagerWindow : Window
    {
        public ObservableCollection<FillRegionModel> Items { get; set; }
        private CollectionViewSource _viewSource;

        public FillRegionManagerWindow(List<FillRegionModel> items)
        {
            InitializeComponent();
            Items = new ObservableCollection<FillRegionModel>(items);
            
            _viewSource = new CollectionViewSource();
            _viewSource.Source = Items;
            _viewSource.Filter += ViewSource_Filter;
            _viewSource.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            
            dgFillRegions.ItemsSource = _viewSource.View;
            UpdateCount();
        }

        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is FillRegionModel model)
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    e.Accepted = true;
                }
                else
                {
                    string search = txtSearch.Text.ToLower();
                    e.Accepted = (model.TypeName != null && model.TypeName.ToLower().Contains(search)) ||
                                 (model.HatchName != null && model.HatchName.ToLower().Contains(search)) ||
                                 (model.TypeMark != null && model.TypeMark.ToLower().Contains(search));
                }
            }
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewSource.View.Refresh();
            UpdateCount();
        }

        private void UpdateCount()
        {
            int count = dgFillRegions.Items.Count;
            tbCount.Text = $"{count} items";
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
