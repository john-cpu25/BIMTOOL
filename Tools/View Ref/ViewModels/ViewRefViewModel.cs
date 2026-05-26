using System;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.ViewRef.ViewModels
{
    public partial class ViewRefViewModel : ObservableObject
    {
        private ViewRefExternalEventHandler _handler;

        [ObservableProperty]
        private ObservableCollection<ElementType> _familyTypes;

        [ObservableProperty]
        private bool _isNSelected = false;
        [ObservableProperty]
        private ElementType _selectedNFamily;

        [ObservableProperty]
        private bool _isWSelected = false;
        [ObservableProperty]
        private ElementType _selectedWFamily;

        [ObservableProperty]
        private bool _isESelected = false;
        [ObservableProperty]
        private ElementType _selectedEFamily;

        [ObservableProperty]
        private bool _isSSelected = false;
        [ObservableProperty]
        private ElementType _selectedSFamily;

        [ObservableProperty]
        private string _statusMessage = "Ready. Select a type and pick walls.";

        public Action RequestHideWindow { get; set; }
        public Action RequestShowWindow { get; set; }

        public ViewRefViewModel(ViewRefExternalEventHandler handler, ObservableCollection<ElementType> familyTypes)
        {
            _handler = handler;
            _handler.ViewModel = this;
            FamilyTypes = familyTypes;
            
            if (FamilyTypes.Count > 0)
            {
                SelectedNFamily = FamilyTypes.FirstOrDefault(f => f.Name.Contains("_N")) ?? FamilyTypes[0];
                SelectedWFamily = FamilyTypes.FirstOrDefault(f => f.Name.Contains("_W")) ?? FamilyTypes[0];
                SelectedEFamily = FamilyTypes.FirstOrDefault(f => f.Name.Contains("_E")) ?? FamilyTypes[0];
                SelectedSFamily = FamilyTypes.FirstOrDefault(f => f.Name.Contains("_S")) ?? FamilyTypes[0];
            }
            else
            {
                StatusMessage = "No View Reference types found.";
            }
        }

        [RelayCommand]
        private void PickAndApply()
        {
            if (!IsNSelected && !IsWSelected && !IsESelected && !IsSSelected)
            {
                StatusMessage = "Please select at least one direction.";
                return;
            }

            RequestHideWindow?.Invoke();
            _handler.RequestAction = "PICK_AND_PLACE";
            _handler.Raise();
        }
    }
}
