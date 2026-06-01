using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.Json;
using Autodesk.Revit.DB;
using RincoNhan.Tools.Filter.Models;
using System.Windows.Data;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.Filter.ViewModels
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }

    public partial class CategoryCountItem : ObservableObject
    {
        public string Name { get; set; }
        public int Count { get; set; }

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class MainViewModel : ObservableObject
    {
        private RevitDataCollector _collector;
        private List<CategoryCountItem> _allCategoryCounts;
        private List<Category> _allCategoriesList;

        public ObservableCollection<FilterGroupViewModel> Groups { get; set; }
        public ObservableCollection<Category> AvailableCategories { get; set; }
        public ObservableCollection<CategoryCountItem> CategoryCounts { get; set; }
        
        public bool IsUncheckAllVisible => string.IsNullOrWhiteSpace(CategorySearch) && _allCategoryCounts != null && _allCategoryCounts.Any(c => c.IsSelected);

        // IsInProjectView: giữ manual setter vì có side-effect gọi RefreshProjectData()
        private bool _isInProjectView;
        public bool IsInProjectView
        {
            get => _isInProjectView;
            set 
            { 
                if (SetProperty(ref _isInProjectView, value))
                {
                    RefreshProjectData();
                }
            }
        }

        [ObservableProperty]
        private int _totalElementCount;

        // CategorySearch: giữ manual setter vì có side-effect gọi FilterCategories()
        private string _categorySearch;
        public string CategorySearch
        {
            get => _categorySearch;
            set
            {
                if (SetProperty(ref _categorySearch, value))
                {
                    FilterCategories();
                    OnPropertyChanged(nameof(IsUncheckAllVisible));
                }
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusVisible))]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isSuccessStatus;

        [ObservableProperty]
        private string _newFilterName;

        public void SetStatus(string msg, bool isSuccess)
        {
            StatusMessage = msg;
            IsSuccessStatus = isSuccess;
            
            if (isSuccess)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (sh, eh) => {
                    StatusMessage = "";
                    ((System.Windows.Threading.DispatcherTimer)sh).Stop();
                };
                timer.Start();
            }
        }

        public bool StatusVisible => !string.IsNullOrEmpty(StatusMessage);

        // IsDarkMode: giữ manual setter vì có side-effect gọi SaveLibrary()
        private bool _isDarkMode = true;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    SaveLibrary();
                }
            }
        }

        public ObservableCollection<SavedFilter> SavedFilters { get; set; }

        private string _libraryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rincovitch", "FilterBuilder", "Library.json");

        public MainViewModel(RevitDataCollector collector)
        {
            _collector = collector;
            Groups = new ObservableCollection<FilterGroupViewModel>();
            SavedFilters = new ObservableCollection<SavedFilter>();
            AvailableCategories = new ObservableCollection<Category>();
            CategoryCounts = new ObservableCollection<CategoryCountItem>();

            try
            {
                LoadLibrary();

                _allCategoriesList = (_collector.GetCategories(false) ?? Enumerable.Empty<Category>()).OrderBy(c => c.Name).ToList();
                foreach(var c in _allCategoriesList) AvailableCategories.Add(c);

                TotalElementCount = _collector.GetTotalElementCount(false);
                var rawCounts = _collector.GetCategoryElementCounts(false) ?? new Dictionary<string, int>();
                _allCategoryCounts = rawCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new CategoryCountItem { Name = kvp.Key, Count = kvp.Value })
                    .ToList();
                    
                foreach (var item in _allCategoryCounts)
                {
                    item.PropertyChanged += CategoryCountItem_PropertyChanged;
                    CategoryCounts.Add(item);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Error initializing data: " + ex.Message, false);
            }

            if (!Groups.Any()) AddGroup();
        }

#pragma warning disable CS0067
        public event EventHandler OnResetViewRequested;
        public event EventHandler<string> OnVisibilityActionRequested;
#pragma warning restore CS0067

        [RelayCommand]
        private void AddGroup()
        {
            var group = new FilterGroupViewModel(this, _collector, Groups.Count + 1);
            Groups.Add(group);
        }

        [RelayCommand]
        private void ClearAll() => Groups.Clear();

        [RelayCommand]
        private void SaveCurrentFilter()
        {
            string filterName = string.IsNullOrWhiteSpace(NewFilterName) 
                ? $"Saved Filter {SavedFilters.Count + 1}" 
                : NewFilterName;

            var filter = new SavedFilter 
            { 
                Name = filterName, 
                Description = $"{Groups.Count} groups with {Groups.Sum(g => g.Rules.Count)} rules",
                SavedAt = DateTime.Now
            };

            foreach (var groupVm in Groups)
            {
                var savedGroup = new SavedGroup { IsAndLogic = groupVm.IsAndLogic };
                foreach (var ruleVm in groupVm.Rules)
                {
                    savedGroup.Rules.Add(new SavedRule
                    {
                        CategoryName = ruleVm.SelectedCategory?.Name,
                        FamilyName = ruleVm.SelectedFamily,
                        ParameterName = ruleVm.SelectedParameter?.Definition.Name,
                        Operator = ruleVm.SelectedOperator,
                        Value = ruleVm.Value
                    });
                }
                filter.Groups.Add(savedGroup);
            }

            SavedFilters.Add(filter);
            SaveLibrary();
            
            NewFilterName = "";
            SetStatus($"Filter '{filterName}' saved to library.", true);
        }

        [RelayCommand]
        private void UncheckAllCategories()
        {
            foreach (var item in _allCategoryCounts)
            {
                item.IsSelected = false;
            }
            UpdateAvailableCategories();
        }

        [RelayCommand]
        private void LoadSavedFilter(SavedFilter filter)
        {
            if (filter == null || !filter.Groups.Any()) return;

            var savedCategoryNames = filter.Groups
                .SelectMany(g => g.Rules)
                .Select(r => r.CategoryName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet();

            bool changed = false;
            foreach (var catCount in _allCategoryCounts)
            {
                if (savedCategoryNames.Contains(catCount.Name))
                {
                    if (!catCount.IsSelected) { catCount.IsSelected = true; changed = true; }
                }
            }
            if (changed) 
            {
                UpdateAvailableCategories();
                OnPropertyChanged(nameof(IsUncheckAllVisible));
            }

            Groups.Clear();
            foreach (var savedGroup in filter.Groups)
            {
                var groupVm = new FilterGroupViewModel(this, _collector, Groups.Count + 1);
                groupVm.IsAndLogic = savedGroup.IsAndLogic;
                groupVm.Rules.Clear();

                foreach (var savedRule in savedGroup.Rules)
                {
                    var ruleVm = new FilterRuleViewModel(groupVm, this, _collector);
                    
                    var category = AvailableCategories.FirstOrDefault(c => c.Name == savedRule.CategoryName);
                    if (category != null)
                    {
                        ruleVm.SelectedCategory = category;
                        
                        if (!string.IsNullOrEmpty(savedRule.FamilyName))
                            ruleVm.SelectedFamily = ruleVm.AvailableFamilies.FirstOrDefault(f => f == savedRule.FamilyName);

                        if (!string.IsNullOrEmpty(savedRule.ParameterName))
                            ruleVm.SelectedParameter = ruleVm.AvailableParameters.FirstOrDefault(p => p.Definition.Name == savedRule.ParameterName);

                        ruleVm.Value = savedRule.Value;
                    }

                    if (!string.IsNullOrEmpty(savedRule.Operator))
                        ruleVm.SelectedOperator = ruleVm.Operators.FirstOrDefault(op => op == savedRule.Operator) ?? "= equals";

                    groupVm.Rules.Add(ruleVm);
                }
                
                if (!groupVm.Rules.Any()) groupVm.Rules.Add(new FilterRuleViewModel(groupVm, this, _collector));
                Groups.Add(groupVm);
            }

            SetStatus($"Loaded filter '{filter.Name}'.", true);
        }

        [RelayCommand]
        private void RemoveSavedFilter(SavedFilter filter)
        {
            if (filter != null)
            {
                SavedFilters.Remove(filter);
                SaveLibrary();
            }
        }

        private void SaveLibrary()
        {
            try
            {
                string dir = Path.GetDirectoryName(_libraryPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var lib = new FilterLibrary 
                { 
                    IsDarkMode = this.IsDarkMode,
                    Filters = SavedFilters.ToList() 
                };
                string json = JsonSerializer.Serialize(lib, options);
                File.WriteAllText(_libraryPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving library: " + ex.Message);
            }
        }

        private void LoadLibrary()
        {
            try
            {
                if (File.Exists(_libraryPath))
                {
                    string json = File.ReadAllText(_libraryPath);
                    var lib = JsonSerializer.Deserialize<FilterLibrary>(json);
                    if (lib != null)
                    {
                        IsDarkMode = lib.IsDarkMode;
                        SavedFilters.Clear();
                        foreach (var item in lib.Filters) SavedFilters.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading library: " + ex.Message);
                SetStatus("Failed to load filter library.", false);
            }
        }

        private void CategoryCountItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CategoryCountItem.IsSelected))
            {
                UpdateAvailableCategories();
            }
        }

        private void UpdateAvailableCategories()
        {
            var selectedNames = _allCategoryCounts.Where(c => c.IsSelected).Select(c => c.Name).ToHashSet();
            AvailableCategories.Clear();

            if (selectedNames.Any())
            {
                foreach (var c in _allCategoriesList.Where(cat => selectedNames.Contains(cat.Name)))
                {
                    AvailableCategories.Add(c);
                }
            }
            else
            {
                foreach (var c in _allCategoriesList)
                {
                    AvailableCategories.Add(c);
                }
            }
            OnPropertyChanged(nameof(IsUncheckAllVisible));
        }

        private void FilterCategories()
        {
            CategoryCounts.Clear();
            var filtered = string.IsNullOrWhiteSpace(CategorySearch)
                ? _allCategoryCounts
                : _allCategoryCounts.Where(c => c.Name.IndexOf(CategorySearch, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var item in filtered)
                CategoryCounts.Add(item);
        }

        // Old AddGroup removed — now handled by [RelayCommand] AddGroup above
        public void RemoveGroup(FilterGroupViewModel group)
        {
            Groups.Remove(group);
            for (int i = 0; i < Groups.Count; i++)
            {
                Groups[i].GroupNumber = i + 1;
            }
        }

        public void RefreshProjectData()
        {
            try
            {
                bool inViewOnly = !IsInProjectView;
                
                var categories = _collector.GetCategories(inViewOnly);
                if (categories != null)
                {
                    _allCategoriesList = categories.ToList();
                    UpdateAvailableCategories();
                }
                
                TotalElementCount = _collector.GetTotalElementCount(inViewOnly);
                var rawCounts = _collector.GetCategoryElementCounts(inViewOnly) ?? new Dictionary<string, int>();
                
                var newCounts = rawCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new CategoryCountItem { Name = kvp.Key, Count = kvp.Value })
                    .ToList();

                var selectedNames = (_allCategoryCounts ?? Enumerable.Empty<CategoryCountItem>())
                    .Where(c => c.IsSelected)
                    .Select(c => c.Name)
                    .ToHashSet();

                foreach (var nc in newCounts)
                {
                    if (selectedNames.Contains(nc.Name)) nc.IsSelected = true;
                    nc.PropertyChanged += CategoryCountItem_PropertyChanged;
                }

                _allCategoryCounts = newCounts;
                FilterCategories();
                
                foreach(var group in Groups)
                {
                    foreach(var rule in group.Rules)
                    {
                        var currentCat = rule.SelectedCategory;
                        if (currentCat != null)
                        {
                            rule.SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == currentCat.Id) ?? currentCat;
                        }
                    }
                }

                SetStatus(string.Format("Scope switched to {0}.", IsInProjectView ? "Entire Project" : "Current View"), true);
            }
            catch (Exception ex)
            {
                SetStatus("Error refreshing data: " + ex.Message, false);
            }
        }
    }

    public partial class FilterGroupViewModel : ObservableObject
    {
        private MainViewModel _parent;
        private RevitDataCollector _collector;

        [ObservableProperty]
        private int _groupNumber;

        [ObservableProperty]
        private bool _isAndLogic = true;

        public ObservableCollection<FilterRuleViewModel> Rules { get; set; }

        public FilterGroupViewModel(MainViewModel parent, RevitDataCollector collector, int num)
        {
            _parent = parent;
            _collector = collector;
            GroupNumber = num;
            Rules = new ObservableCollection<FilterRuleViewModel>();

            AddRule();
        }

        [RelayCommand]
        private void AddRule() => Rules.Add(new FilterRuleViewModel(this, _parent, _collector));

        [RelayCommand]
        private void RemoveGroup() => _parent.RemoveGroup(this);

        [RelayCommand]
        private void SetAndLogic() => IsAndLogic = true;

        [RelayCommand]
        private void SetOrLogic() => IsAndLogic = false;

        public void RemoveRule(FilterRuleViewModel rule) => Rules.Remove(rule);
    }

    public partial class FilterRuleViewModel : ObservableObject
    {
        private FilterGroupViewModel _parent;
        private MainViewModel _main;
        private RevitDataCollector _collector;

        public ObservableCollection<Category> Categories => _main.AvailableCategories;
        
        private Category _selectedCategory;
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                if (SetProperty(ref _selectedCategory, value))
                {
                    LoadFamilies();
                    LoadParameters();
                }
            }
        }

        public ObservableCollection<string> AvailableFamilies { get; set; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedFamily;

        public ObservableCollection<Parameter> AvailableParameters { get; set; } = new ObservableCollection<Parameter>();

        // SelectedParameter: giữ manual setter vì có side-effect gọi LoadAvailableValues()
        private Parameter _selectedParameter;
        public Parameter SelectedParameter
        {
            get => _selectedParameter;
            set 
            { 
                if (SetProperty(ref _selectedParameter, value))
                {
                    LoadAvailableValues();
                }
            }
        }

        public ObservableCollection<string> Operators { get; set; } = new ObservableCollection<string> 
        { 
            "= equals", "> greater than", "< less than", "contains", "does not equal" 
        };

        [ObservableProperty]
        private string _selectedOperator;

        public ObservableCollection<string> AvailableValues { get; set; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _value;

        public FilterRuleViewModel(FilterGroupViewModel parent, MainViewModel main, RevitDataCollector collector)
        {
            _parent = parent;
            _main = main;
            _collector = collector;
            SelectedOperator = "= equals";
        }

        [RelayCommand]
        private void RemoveRule() => _parent.RemoveRule(this);

        private void LoadFamilies()
        {
            AvailableFamilies.Clear();
            if (SelectedCategory != null)
            {
                var fams = _collector.GetFamiliesForCategory(SelectedCategory, false);
                foreach (var f in fams) AvailableFamilies.Add(f);
                if (AvailableFamilies.Any()) SelectedFamily = AvailableFamilies.First();
            }
            else
            {
                SelectedFamily = null;
            }
        }

        private void LoadParameters()
        {
            AvailableParameters.Clear();
            if (SelectedCategory != null)
            {
                var pList = _collector.GetParametersForCategory(SelectedCategory, false);
                foreach (var p in pList) AvailableParameters.Add(p);
            }
        }

        private void LoadAvailableValues()
        {
            AvailableValues.Clear();
            if (SelectedCategory != null && SelectedParameter != null)
            {
                var valDict = _collector.GetParameterValues(SelectedCategory, SelectedParameter, false);
                var sortedVals = valDict.Keys.OrderBy(k => k).ToList();
                foreach (var v in sortedVals) AvailableValues.Add(v);
            }
        }
    }
}
