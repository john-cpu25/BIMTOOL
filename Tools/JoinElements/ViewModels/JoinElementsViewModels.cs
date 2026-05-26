using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.JoinElements.ViewModels
{
    public partial class CategoryPairViewModel : ObservableObject
    {
        [ObservableProperty]
        private Category _categoryA;

        [ObservableProperty]
        private Category _categoryB;

        // RemoveCommand được truyền từ ngoài vào — dùng interface IRelayCommand<T> để tương thích với source generator
        public IRelayCommand<CategoryPairViewModel> RemoveCommand { get; set; }

        public CategoryPairViewModel(IRelayCommand<CategoryPairViewModel> removeCommand)
        {
            RemoveCommand = removeCommand;
        }
    }

    public partial class MainViewModel : ObservableObject
    {
        private RevitDataCollector _collector;
        private JoinElementsExternalEventHandler _handler;

        public ObservableCollection<Category> AvailableCategories { get; set; }
        public ObservableCollection<CategoryPairViewModel> CategoryPairs { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusVisible))]
        private string _statusMessage;

        public bool StatusVisible => !string.IsNullOrEmpty(StatusMessage);

        [ObservableProperty]
        private bool _isProcessing;

        public MainViewModel(RevitDataCollector collector, JoinElementsExternalEventHandler handler)
        {
            _collector = collector;
            _handler = handler;
            _handler.ViewModel = this;

            var allowedCategories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Columns,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_StructuralFoundation
            };

            var categories = _collector.GetModelCategories(true)
                .Where(c => allowedCategories.Contains((BuiltInCategory)c.Id.IntegerValue))
                .ToList();

            AvailableCategories = new ObservableCollection<Category>(categories);
            CategoryPairs = new ObservableCollection<CategoryPairViewModel>();

            // Add one empty pair by default
            AddPair();
        }

        [RelayCommand]
        private void JoinAll() => ExecuteAction("JOIN_ALL");

        [RelayCommand]
        private void UnjoinAll() => ExecuteAction("UNJOIN_ALL");

        [RelayCommand]
        private void SwitchAll() => ExecuteAction("SWITCH_ALL");

        [RelayCommand]
        private void AddPair()
        {
            CategoryPairs.Add(new CategoryPairViewModel(RemovePairCommand));
        }

        [RelayCommand]
        private void RemovePair(CategoryPairViewModel pair)
        {
            if (pair != null) CategoryPairs.Remove(pair);
        }

        [RelayCommand]
        private void JoinPairs() => ExecuteAction("JOIN_PAIRS");

        [RelayCommand]
        private void UnjoinPairs() => ExecuteAction("UNJOIN_PAIRS");

        [RelayCommand]
        private void SwitchPairs() => ExecuteAction("SWITCH_PAIRS");

        private void ExecuteAction(string action)
        {
            _handler.RequestAction = action;
            _handler.Raise();
            SetStatus($"Processing {action.Replace("_", " ")}...", false);
        }

        public void SetStatus(string msg, bool autoHide = true)
        {
            StatusMessage = msg;
            if (autoHide)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (sh, eh) => {
                    StatusMessage = "";
                    ((System.Windows.Threading.DispatcherTimer)sh).Stop();
                };
                timer.Start();
            }
        }
    }
}
