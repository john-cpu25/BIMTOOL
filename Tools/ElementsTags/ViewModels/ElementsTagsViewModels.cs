using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.ElementsTags.ViewModels
{
    public partial class CategoryItemViewModel : ObservableObject
    {
        public Category Category { get; }
        public string Name => Category?.Name;
        public List<FamilySymbol> TagTypes { get; }

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private FamilySymbol _selectedTagType;

        [ObservableProperty]
        private Color _overrideColor;

        public CategoryItemViewModel(Category category, List<FamilySymbol> tagTypes)
        {
            Category = category;
            TagTypes = tagTypes;
            SelectedTagType = TagTypes.FirstOrDefault();
            IsSelected = true;

            int bic = (int)(category?.Id.GetIdValue() ?? 0);
            if (bic == (int)BuiltInCategory.OST_Floors) OverrideColor = new Color(0, 255, 255); 
            else if (bic == (int)BuiltInCategory.OST_StructuralColumns) OverrideColor = new Color(0, 255, 0); 
            else if (bic == (int)BuiltInCategory.OST_StructuralFoundation) OverrideColor = new Color(255, 255, 0); 
            else if (bic == (int)BuiltInCategory.OST_StructuralFraming) OverrideColor = new Color(0, 0, 255); 
            else if (bic == (int)BuiltInCategory.OST_Walls) OverrideColor = new Color(255, 105, 180); 
            else OverrideColor = new Color(255, 0, 255); 
        }
    }

    public partial class ErrorItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public ElementId ElementId { get; set; }
        public string IdValue { get; set; }
        public string Category { get; set; }
        public string ErrorType { get; set; }
    }

    public partial class MainViewModel : ObservableObject
    {
        private RevitDataCollector _collector;
        private ElementsTagsExternalEventHandler _handler;
        private ExternalEvent _externalEvent;

        public ObservableCollection<CategoryItemViewModel> Categories { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasErrors))]
        private ObservableCollection<ErrorItemViewModel> _errorResults = new ObservableCollection<ErrorItemViewModel>();

        public bool HasErrors => ErrorResults != null && ErrorResults.Any();

        [ObservableProperty]
        private bool _addLeader = true;

        [ObservableProperty]
        private bool _onlyUntagged = true;

        [ObservableProperty]
        private bool _autoRehostSection = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusVisible))]
        private string _statusMessage;

        public bool StatusVisible => !string.IsNullOrEmpty(StatusMessage);

        public MainViewModel(RevitDataCollector collector, ElementsTagsExternalEventHandler handler)
        {
            _collector = collector;
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);

            _handler.ReportErrors = errors => 
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    ErrorResults = new ObservableCollection<ErrorItemViewModel>(errors);
                });
            };

            var viewCategories = _collector.GetTaggableCategories();
            Categories = new ObservableCollection<CategoryItemViewModel>();
            foreach (var cat in viewCategories)
            {
                var types = _collector.GetTagTypes(cat);
                if (types.Any())
                {
                    Categories.Add(new CategoryItemViewModel(cat, types));
                }
            }
        }

        [RelayCommand]
        private void TagAll() => ExecuteAction("TagAll");

        [RelayCommand]
        private void CheckTag2D() => ExecuteAction("CheckTag2D");

        [RelayCommand]
        private void ClashTag() => ExecuteAction("ClashTag");

        [RelayCommand]
        private void CheckWallTagSection() => ExecuteAction("CheckWallTagSection");

        [RelayCommand]
        private void Reset() => ExecuteAction("ResetAll");

        [RelayCommand]
        private void CheckFloor() => ExecuteAction("CheckFloor");

        [RelayCommand]
        private void PickColor(CategoryItemViewModel item)
        {
            if (item == null) return;
            
            ColorSelectionDialog dialog = new ColorSelectionDialog();
            dialog.OriginalColor = item.OverrideColor;
            
            if (dialog.Show() == ItemSelectionDialogResult.Confirmed)
            {
                item.OverrideColor = dialog.SelectedColor;
            }
        }


        [RelayCommand]
        private void UpdateSelectedTags()
        {
            var selectedTags = ErrorResults.Where(x => x.IsSelected).ToList();
            if (selectedTags.Count == 0)
            {
                StatusMessage = "Vui lòng chọn ít nhất 1 tag để update.";
                return;
            }

            StatusMessage = "";
            
            _handler.Action = "UpdateWallTagSection";
            _handler.SelectedTagsToUpdate = selectedTags;
            _handler.NotifyStatus = msg => StatusMessage = msg;
            
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void UncheckAll()
        {
            if (ErrorResults == null) return;
            foreach (var item in ErrorResults)
            {
                item.IsSelected = false;
            }
        }

        [RelayCommand]
        private void ShowElement(ErrorItemViewModel item)
        {
            if (item == null) return;
            _handler.Action = "ShowElement";
            _handler.ElementIdToShow = item.ElementId;
            _externalEvent.Raise();
        }

        private void ExecuteAction(string action)
        {
            ErrorResults = new ObservableCollection<ErrorItemViewModel>();
            
            _handler.Action = action;
            _handler.SelectedCategories = Categories.Where(c => c.IsSelected).ToList();
            _handler.AddLeader = AddLeader;
            _handler.OnlyUntagged = OnlyUntagged;
            _handler.AutoRehostSection = AutoRehostSection;
            _handler.NotifyStatus = msg => StatusMessage = msg;

            _externalEvent.Raise();
        }
    }
}
