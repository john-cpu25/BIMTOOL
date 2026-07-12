using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Core;
using RincoNhan.Tools.TwoDFamilyManager.Models;

namespace RincoNhan.Tools.TwoDFamilyManager.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);
    }

    public class FamilyManagerViewModel : ObservableObject
    {
        private Document _doc;
        private ExternalEvent _externalEvent;
        private FamilyManagerEventHandler _handler;

        private ObservableCollection<FamilyCategoryItem> _allCategories;

        private ObservableCollection<FamilyCategoryItem> _categories;
        public ObservableCollection<FamilyCategoryItem> Categories
        {
            get => _categories;
            set { _categories = value; OnPropertyChanged(); }
        }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set 
            { 
                _searchText = value; 
                OnPropertyChanged();
                FilterCategories();
            }
        }

        private FamilySymbolItem _selectedSymbol;
        public FamilySymbolItem SelectedSymbol
        {
            get => _selectedSymbol;
            set 
            { 
                _selectedSymbol = value; 
                OnPropertyChanged();
            }
        }

        public ICommand PlaceCommand { get; }

        public FamilyManagerViewModel(Document doc, ExternalEvent externalEvent, FamilyManagerEventHandler handler)
        {
            _doc = doc;
            _externalEvent = externalEvent;
            _handler = handler;

            Categories = new ObservableCollection<FamilyCategoryItem>();
            _allCategories = new ObservableCollection<FamilyCategoryItem>();

            PlaceCommand = new RelayCommand(PlaceSymbol, CanPlaceSymbol);

            LoadFamilies();
        }

        private void LoadFamilies()
        {
            // Collect all 2D families (Detail Items, Generic Annotations)
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => s.Category != null && 
                            (s.Category.CategoryType == CategoryType.Annotation || 
                             s.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_DetailComponents))
                .ToList();

            var groupedByCategory = collector.GroupBy(s => s.Category.Name).OrderBy(g => g.Key);

            foreach (var categoryGroup in groupedByCategory)
            {
                var categoryItem = new FamilyCategoryItem { Name = categoryGroup.Key, IsExpanded = true };

                var groupedByFamily = categoryGroup.GroupBy(s => s.Family.Name).OrderBy(g => g.Key);

                foreach (var familyGroup in groupedByFamily)
                {
                    var familyModelItem = new FamilyModelItem 
                    { 
                        Name = familyGroup.Key,
                        Family = familyGroup.First().Family,
                        IsExpanded = true
                    };

                    foreach (var symbol in familyGroup.OrderBy(s => s.Name))
                    {
                        familyModelItem.Symbols.Add(new FamilySymbolItem 
                        { 
                            Name = symbol.Name, 
                            Symbol = symbol,
                            Image = GetPreviewImage(symbol)
                        });
                    }

                    categoryItem.Families.Add(familyModelItem);
                }

                _allCategories.Add(categoryItem);
                Categories.Add(categoryItem);
            }
        }

        private void FilterCategories()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                Categories = new ObservableCollection<FamilyCategoryItem>(_allCategories);
                return;
            }

            var lowerSearch = SearchText.ToLower();
            var filteredCategories = new ObservableCollection<FamilyCategoryItem>();

            foreach (var category in _allCategories)
            {
                var newCategory = new FamilyCategoryItem { Name = category.Name, IsExpanded = true };

                foreach (var family in category.Families)
                {
                    // Check if family name or any symbol name matches
                    bool familyMatches = family.Name.ToLower().Contains(lowerSearch);
                    var matchedSymbols = family.Symbols.Where(s => s.Name.ToLower().Contains(lowerSearch)).ToList();

                    if (familyMatches || matchedSymbols.Any())
                    {
                        var newFamily = new FamilyModelItem 
                        { 
                            Name = family.Name, 
                            Family = family.Family,
                            IsExpanded = true
                        };

                        // If family matches, show all symbols. If not, show only matched symbols.
                        IEnumerable<FamilySymbolItem> symbolsToAdd = familyMatches ? family.Symbols : (IEnumerable<FamilySymbolItem>)matchedSymbols;

                        foreach (var sym in symbolsToAdd)
                        {
                            newFamily.Symbols.Add(new FamilySymbolItem 
                            { 
                                Name = sym.Name, 
                                Symbol = sym.Symbol,
                                Image = sym.Image
                            });
                        }

                        newCategory.Families.Add(newFamily);
                    }
                }

                if (newCategory.Families.Any())
                {
                    filteredCategories.Add(newCategory);
                }
            }

            Categories = filteredCategories;
        }

        private bool CanPlaceSymbol(object parameter)
        {
            var symbolToPlace = parameter as FamilySymbolItem ?? SelectedSymbol;
            return symbolToPlace != null;
        }

        private void PlaceSymbol(object parameter)
        {
            var symbolToPlace = parameter as FamilySymbolItem ?? SelectedSymbol;
            if (symbolToPlace != null)
            {
                _handler.SymbolToPlace = symbolToPlace.Symbol;
                _externalEvent.Raise();
            }
        }

        private BitmapImage GetPreviewImage(FamilySymbol symbol)
        {
            var size = new System.Drawing.Size(64, 64);
            var bmp = symbol.GetPreviewImage(size);
            if (bmp == null) return null;
            
            using (var memory = new MemoryStream())
            {
                bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
    }
}
