using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.CheckFold.Models;

namespace RincoNhan.Tools.CheckFold.ViewModels
{
    public partial class CheckFoldViewModel : ObservableObject
    {
        private readonly CheckFoldHandler _handler;
        private readonly ExternalEvent _externalEvent;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private int _foldCount;

        [ObservableProperty]
        private int _stepCount;

        [ObservableProperty]
        private int _mismatchCount;

        [ObservableProperty]
        private int _wrongRLCount;

        [ObservableProperty]
        private int _updatedRLCount;

        [ObservableProperty]
        private bool _hasChecked;

        [ObservableProperty]
        private bool _hasUpdated;

        [ObservableProperty]
        private int _missingStepCount;

        [ObservableProperty]
        private bool _hasMissingSteps;

        /// <summary>Tab 1 data</summary>
        public ObservableCollection<FoldCheckItem> FoldItems { get; } = new ObservableCollection<FoldCheckItem>();

        /// <summary>Tab 2 - Wrong RL list (shown after Check)</summary>
        public ObservableCollection<StepCheckItem> WrongRLItems { get; } = new ObservableCollection<StepCheckItem>();

        /// <summary>Tab 2 - Updated RL list (shown after Update)</summary>
        public ObservableCollection<StepCheckItem> UpdatedRLItems { get; } = new ObservableCollection<StepCheckItem>();

        /// <summary>Tab 3 - Missing 3D Steps list</summary>
        public ObservableCollection<MissingStepItem> MissingStepItems { get; } = new ObservableCollection<MissingStepItem>();

        // All step items (internal tracking)
        private List<StepCheckItem> _allStepItems = new List<StepCheckItem>();

        public CheckFoldViewModel(CheckFoldHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);

            // Wire up callbacks from handler → UI via Dispatcher
            _handler.OnFoldDataLoaded = foldItems => Dispatch(() => HandleFoldData(foldItems));
            _handler.OnStepDataLoaded = stepItems => Dispatch(() => HandleStepData(stepItems));
            _handler.NotifyStatus = msg => Dispatch(() => StatusMessage = msg);
            _handler.OnCheckCompleted = wrongCount => Dispatch(() => HandleCheckCompleted(wrongCount));
            _handler.OnUpdateCompleted = (updated, failed) => Dispatch(() => HandleUpdateCompleted(updated, failed));
            _handler.OnResetCompleted = () => Dispatch(() => HandleResetCompleted());
            
            _handler.OnMissingStepsChecked = items => Dispatch(() => HandleMissingStepsChecked(items));
            _handler.OnHighlightsCleared = () => Dispatch(() => HandleHighlightsCleared());

            // Initial load
            _handler.Action = "LoadData";
            _externalEvent.Raise();
        }

        private void Dispatch(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void HandleFoldData(List<FoldCheckItem> items)
        {
            FoldItems.Clear();
            foreach (var item in items)
            {
                FoldItems.Add(item);
            }
            FoldCount = FoldItems.Count;
            MismatchCount = FoldItems.Count(i => i.Status == "Mismatch");
        }

        private void HandleStepData(List<StepCheckItem> items)
        {
            _allStepItems = items;
            StepCount = items.Count;
        }

        private void HandleCheckCompleted(int wrongCount)
        {
            WrongRLItems.Clear();
            UpdatedRLItems.Clear();
            HasUpdated = false;

            foreach (var item in _allStepItems)
            {
                WrongRLItems.Add(item);
            }

            WrongRLCount = wrongCount;
            HasChecked = true;
        }

        private void HandleUpdateCompleted(int updated, int failed)
        {
            // Move updated items from Wrong list to Updated list
            var updatedItems = WrongRLItems.Where(i => i.IsSelected).ToList();
            foreach (var item in updatedItems)
            {
                item.CurrentRLValue = item.CalculatedValueStr;
                item.CurrentOffsetValue = item.IsVaries ? 0 : -Math.Abs(item.CalculatedValue);
                item.Status = "OK";
                item.IsUpdated = true;
                UpdatedRLItems.Add(item);
            }

            // Remove from wrong list
            foreach (var item in updatedItems)
            {
                WrongRLItems.Remove(item);
            }

            UpdatedRLCount = UpdatedRLItems.Count;
            WrongRLCount = WrongRLItems.Count;
            HasUpdated = true;
        }

        private void HandleResetCompleted()
        {
            WrongRLItems.Clear();
            UpdatedRLItems.Clear();
            WrongRLCount = 0;
            UpdatedRLCount = 0;
            HasChecked = false;
            HasUpdated = false;
        }

        [RelayCommand]
        private void Refresh()
        {
            StatusMessage = "Refreshing data...";
            HasChecked = false;
            HasUpdated = false;
            HasMissingSteps = false;
            WrongRLItems.Clear();
            UpdatedRLItems.Clear();
            MissingStepItems.Clear();

            _handler.Action = "LoadData";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void CheckSteps()
        {
            _handler.Action = "CheckSteps";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void UpdateSteps()
        {
            var itemsToUpdate = WrongRLItems.Where(i => i.IsSelected).ToList();
            if (!itemsToUpdate.Any())
            {
                StatusMessage = "Không có item nào được chọn để cập nhật. Hãy Check trước.";
                return;
            }

            _handler.ItemsToUpdate = itemsToUpdate;
            _handler.Action = "UpdateSteps";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ResetOverrides()
        {
            _handler.Action = "ResetOverrides";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void CheckMissingSteps()
        {
            StatusMessage = "Đang kiểm tra 3D Step Missing...";
            _handler.Action = "CheckMissingSteps";
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ClearHighlights()
        {
            StatusMessage = "Đang xóa các đường highlight đỏ...";
            _handler.Action = "ClearHighlights";
            _externalEvent.Raise();
        }

        private void HandleMissingStepsChecked(List<MissingStepItem> items)
        {
            MissingStepItems.Clear();
            foreach(var item in items)
            {
                MissingStepItems.Add(item);
            }
            MissingStepCount = items.Count;
            HasMissingSteps = items.Count > 0;
        }

        private void HandleHighlightsCleared()
        {
            HasMissingSteps = false;
            MissingStepItems.Clear();
            MissingStepCount = 0;
            StatusMessage = "Đã dọn dẹp view thành công.";
        }

        [RelayCommand]
        private void SelectAllFold()
        {
            bool allSelected = FoldItems.All(i => i.IsSelected);
            foreach (var item in FoldItems)
            {
                item.IsSelected = !allSelected;
            }
        }

        /// <summary>
        /// Select and zoom to an element in Revit when user clicks a row.
        /// </summary>
        public void SelectElementInRevit(ElementId elementId)
        {
            if (elementId == null) return;
            _handler.ElementIdToSelect = elementId;
            _handler.Action = "SelectElement";
            _externalEvent.Raise();
        }
    }
}
