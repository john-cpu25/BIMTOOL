using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.CreateSectionWall.ViewModels
{
    public partial class CreateSectionWallViewModel : ObservableObject
    {
        private CreateSectionWallExternalEventHandler _handler;

        [ObservableProperty] private double _height = 3000;
        [ObservableProperty] private double _offset = 1000;
        [ObservableProperty] private bool _flipDirection = false;
        [ObservableProperty] private bool _isWSelected = true;
        [ObservableProperty] private bool _isCWSelected = false;
        [ObservableProperty] private string _statusMessage = "Ready. Adjust settings and pick walls.";

        public string SelectedPrefix => IsCWSelected ? "CW." : "W.";

        public Action RequestHideWindow { get; set; }
        public Action RequestShowWindow { get; set; }

        public CreateSectionWallViewModel(CreateSectionWallExternalEventHandler handler)
        {
            _handler = handler;
            _handler.ViewModel = this;
        }

        [RelayCommand]
        private void PickAndCreate()
        {
            RequestHideWindow?.Invoke();
            _handler.RequestAction = "PICK_AND_CREATE";
            _handler.Raise();
        }
    }
}
