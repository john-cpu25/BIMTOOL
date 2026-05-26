using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ElevationView.ViewModels
{
    public partial class ElevationViewViewModel : ObservableObject
    {
        private ElevationViewExternalEventHandler _handler;
        private ExternalEvent _externalEvent;

        [ObservableProperty] private double _offsetLeft = 2000;
        [ObservableProperty] private double _offsetRight = 1000;
        [ObservableProperty] private double _offsetTop = 100;
        [ObservableProperty] private double _offsetBottom = 100;
        [ObservableProperty] private double _levelHeadOffset = 1620;
        [ObservableProperty] private double _levelTailOffset = -3000;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRefWall))]
        private double _refWallWidth;
        [ObservableProperty] private string _viewName;
        [ObservableProperty] private string _statusMessage;

        public BoundingBoxXYZ InitialCropBox { get; private set; }
        public double RefWallLeftX { get; set; }
        public double RefWallRightX { get; set; }
        public bool HasRefWall => RefWallWidth > 0;

        public ElevationViewViewModel(ElevationViewExternalEventHandler handler, View activeView)
        {
            _handler = handler;
            _handler.ViewModel = this;
            _externalEvent = ExternalEvent.Create(_handler);
            
            ViewName = activeView.Name;
            InitialCropBox = activeView.CropBox;
            StatusMessage = "Ready. Adjust offsets to update crop.";
        }

        partial void OnOffsetLeftChanged(double value)
        {
            UpdateLevelTailOffset();
            RequestUpdate();
        }
        partial void OnOffsetRightChanged(double value)
        {
            UpdateLevelTailOffset();
            RequestUpdate();
        }
        partial void OnOffsetTopChanged(double value) => RequestUpdate();
        partial void OnOffsetBottomChanged(double value) => RequestUpdate();
        partial void OnLevelHeadOffsetChanged(double value) => RequestLevelUpdate();
        partial void OnLevelTailOffsetChanged(double value) => RequestLevelUpdate();
        partial void OnRefWallWidthChanged(double value) => UpdateLevelTailOffset();

        private void UpdateLevelTailOffset()
        {
            if (!HasRefWall) return;
            LevelTailOffset = -(RefWallWidth + OffsetLeft + OffsetRight);
        }

        public Action RequestHideWindow { get; set; }
        public Action RequestShowWindow { get; set; }

        [RelayCommand]
        private void PickWall()
        {
            // Reset offsets to suggested defaults
            OffsetLeft = 2000;
            OffsetRight = 1000;
            OffsetTop = 0;
            OffsetBottom = 0;

            RequestHideWindow?.Invoke();
            _handler.RequestAction = "PICK_WALL";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void PickWalls()
        {
            // Reset offsets to suggested defaults
            OffsetLeft = 2000;
            OffsetRight = 1000;
            OffsetTop = 0;
            OffsetBottom = 0;

            RequestHideWindow?.Invoke();
            _handler.RequestAction = "PICK_WALLS";
            _externalEvent.Raise();
        }

        private void RequestUpdate()
        {
            if (!HasRefWall) return; // Only live update if we have a reference wall
            _handler.RequestAction = "UPDATE_CROP";
            _externalEvent.Raise();
        }

        private void RequestLevelUpdate()
        {
            _handler.RequestAction = "ALIGN_LEVELS";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void AlignLevels()
        {
            _handler.RequestAction = "ALIGN_LEVELS";
            _externalEvent.Raise();
            StatusMessage = "Levels aligned to left.";
        }

        [RelayCommand]
        private void ResetOffsets()
        {
            OffsetLeft = 0;
            OffsetRight = 0;
            OffsetTop = 0;
            OffsetBottom = 0;
            RequestUpdate();
            StatusMessage = "Offsets reset.";
        }
    }
}
