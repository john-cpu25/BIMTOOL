using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.RebarColumn.ViewModels
{
    public partial class ApplyStoreyItem : ObservableObject
    {
        public int No { get; set; }
        public string Section { get; set; }
        public string LevelName { get; set; }
        public double Elevation { get; set; }

        [ObservableProperty]
        private bool _isSelected;

        public ApplyToOtherFloorsViewModel ParentViewModel { get; set; }

        partial void OnIsSelectedChanged(bool value)
        {
            ParentViewModel?.UpdateSelectedCount();
        }
    }

    public partial class ApplyToOtherFloorsViewModel : ObservableObject
    {
        public ObservableCollection<ApplyStoreyItem> StoreyItems { get; } = new ObservableCollection<ApplyStoreyItem>();

        [ObservableProperty]
        private int _selectedCount = 0;

        public ApplyToOtherFloorsViewModel()
        {
            // Populate mock/representative storey stack matching the user's photo
            StoreyItems.Add(new ApplyStoreyItem { No = 1, Section = "Rec: (S=400x300)", LevelName = "TẦNG 5", Elevation = 14650, IsSelected = false, ParentViewModel = this });
            StoreyItems.Add(new ApplyStoreyItem { No = 2, Section = "Rec: (S=400x300)", LevelName = "TẦNG 4", Elevation = 11050, IsSelected = false, ParentViewModel = this });
            StoreyItems.Add(new ApplyStoreyItem { No = 3, Section = "Rec: (S=400x300)", LevelName = "TẦNG 3", Elevation = 7450, IsSelected = true, ParentViewModel = this });
            StoreyItems.Add(new ApplyStoreyItem { No = 4, Section = "Rec: (S=400x300)", LevelName = "TẦNG 2", Elevation = 3850, IsSelected = true, ParentViewModel = this });
            StoreyItems.Add(new ApplyStoreyItem { No = 5, Section = "Rec: (S=400x300)", LevelName = "TẦNG 1", Elevation = -50, IsSelected = true, ParentViewModel = this });
            StoreyItems.Add(new ApplyStoreyItem { No = 6, Section = "Rec: (S=400x300)", LevelName = "MÓNG", Elevation = -1100, IsSelected = true, ParentViewModel = this });

            UpdateSelectedCount();
        }

        public void UpdateSelectedCount()
        {
            SelectedCount = StoreyItems.Count(item => item.IsSelected);
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in StoreyItems)
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void UnselectAll()
        {
            foreach (var item in StoreyItems)
            {
                item.IsSelected = false;
            }
        }
    }
}
