using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.WallDivide.ViewModels
{
    public partial class WallItemViewModel : ObservableObject
    {
        public ElementId WallId { get; set; }
        public string IdString => WallId.ToString();
        public string TypeName { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public double Volume { get; set; }
        public double Weight { get; set; }
        public bool HasHostedElements { get; set; }
        public bool NeedsDivision { get; set; }
        public bool IsInGroup { get; set; }
        public int SuggestedParts { get; set; }

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private int _selectedParts;

        public string StatusIcon => (IsInGroup ? "🔒" : "") + (NeedsDivision ? "×" : "✓");
        public string ToolTip => (IsInGroup ? "Grouped Wall (Division Locked). " : "") + (NeedsDivision ? "Weight exceeds 15 tons limit" : "OK");

        public WallItemViewModel(WallData data)
        {
            WallId = data.Id;
            TypeName = data.Name;
            Height = data.Height;
            Length = data.Length;
            Volume = data.Volume;
            Weight = data.Weight;
            HasHostedElements = data.HasHostedElements;
            NeedsDivision = data.NeedsDivision;
            SuggestedParts = data.SuggestedParts;
            IsInGroup = data.GroupId != ElementId.InvalidElementId;
            
            IsSelected = NeedsDivision && !IsInGroup;
            SelectedParts = SuggestedParts > 1 ? SuggestedParts : 2;
        }
    }

    public partial class WallDivideViewModel : ObservableObject
    {
        private WallDivideExternalEventHandler _handler;
        private RevitDataCollector _collector;

        [ObservableProperty] private string _groupName;
        [ObservableProperty] private bool _hasGroup;
        [ObservableProperty] private bool _isDivideAllowed;
        [ObservableProperty] private string _wallCountText;
        [ObservableProperty] private bool _hasWalls;
        [ObservableProperty] private string _floorName;
        [ObservableProperty] private double _floorThickness;
        [ObservableProperty] private bool _hasFloor;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private bool _isProcessing;
        [ObservableProperty] private bool _isSelectAll;

        partial void OnIsSelectAllChanged(bool value)
        {
            if (WallItems == null) return;
            foreach (var item in WallItems)
            {
                item.IsSelected = value;
            }
        }

        public ElementId FloorId { get; private set; }
        public ElementId SelectedWallId { get; private set; }
        public ObservableCollection<WallItemViewModel> WallItems { get; set; }

        public WallDivideViewModel(WallDivideExternalEventHandler handler, Document doc)
        {
            _handler = handler;
            _handler.ViewModel = this;
            _collector = new RevitDataCollector(doc);
            WallItems = new ObservableCollection<WallItemViewModel>();
            IsSelectAll = true; // Default to checked
        }

        [RelayCommand]
        private void PickGroup()
        {
            _handler.RequestAction = "PICK_GROUP";
            _handler.Raise();
        }

        [RelayCommand]
        private void PickWalls()
        {
            _handler.RequestAction = "PICK_WALLS";
            _handler.Raise();
        }

        [RelayCommand]
        private void PickFloor()
        {
            _handler.RequestAction = "PICK_FLOOR";
            _handler.Raise();
        }

        [RelayCommand]
        private void ApplyTopOffset()
        {
            if (!HasFloor) return;
            _handler.RequestAction = "APPLY_TOP_OFFSET";
            _handler.Raise();
        }

        [RelayCommand]
        private void DisallowJoin()
        {
            _handler.RequestAction = "DISALLOW_JOIN";
            _handler.Raise();
        }

        [RelayCommand]
        private void Highlight()
        {
            _handler.RequestAction = "HIGHLIGHT";
            _handler.Raise();
        }

        [RelayCommand]
        private void Divide()
        {
            IsProcessing = true;
            _handler.RequestAction = "DIVIDE";
            _handler.Raise();
        }

        [RelayCommand]
        private void BoxWall(WallItemViewModel item)
        {
            if (item == null) return;
            SelectedWallId = item.WallId;
            _handler.RequestAction = "BOX_WALL";
            _handler.Raise();
        }

        public void LoadGroup(Group group)
        {
            GroupName = group.Name;
            HasGroup = true;
            HasWalls = true;
            IsDivideAllowed = false; // Disable divide for group selection

            var dataList = _collector.GetWallsFromGroup(group);
            WallItems.Clear();
            foreach (var data in dataList)
            {
                WallItems.Add(new WallItemViewModel(data));
            }
            
            WallCountText = $"{WallItems.Count} walls from Group {GroupName}";
            SetStatus($"Loaded {WallItems.Count} walls from group. (Opening volumes subtracted)", true);
        }

        public void LoadWalls(List<Wall> walls)
        {
            GroupName = "Direct Selection";
            HasGroup = true;
            IsDivideAllowed = true; // Enable divide for direct selection
            WallItems.Clear();
            foreach (var wall in walls)
            {
                var data = _collector.ExtractWallData(wall);
                var item = new WallItemViewModel(data);
                item.IsSelected = IsSelectAll; // Match header checkbox
                WallItems.Add(item);
            }

            HasWalls = WallItems.Count > 0;
            WallCountText = $"{WallItems.Count} walls loaded";
            SetStatus($"Loaded {WallItems.Count} individual walls. (Opening volumes subtracted)", true);
        }

        public void LoadFloor(Floor floor)
        {
            FloorId = floor.Id;
            FloorName = floor.Name;
            FloorThickness = _collector.GetFloorThickness(floor);
            HasFloor = true;
            
            SetStatus($"Selected floor: {FloorName} ({FloorThickness:F0}mm)", true);
        }

        public void SetStatus(string msg, bool autoHide)
        {
            StatusMessage = msg;
            if (autoHide)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                timer.Tick += (s, e) => { StatusMessage = ""; ((System.Windows.Threading.DispatcherTimer)s).Stop(); };
                timer.Start();
            }
        }
    }
}
