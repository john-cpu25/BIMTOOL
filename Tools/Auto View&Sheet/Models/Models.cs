using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.AutoViewSheet.Models
{
    // Re-use items from CreateViewSheet if possible, but let's define them cleanly here to avoid strict coupling
    public class LevelItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        public double Elevation { get; set; }
    }

    public class ViewTypeItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class ViewTemplateItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class ScopeBoxItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class TitleBlockItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class ViewportTitleItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class ViewItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class AutoViewSheetRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set 
            { 
                _isSelected = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsGroupSelected));
            }
        }

        public bool IsGroupSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(IsGroupSelected));
                    GroupSelectionChangedCallback?.Invoke(this, value);
                }
            }
        }

        public string SheetNumberAndName => $"{SheetNumber} - {SheetName}";

        private LevelItem _selectedLevel;
        public LevelItem SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ViewName));
                LevelChangedCallback?.Invoke(this, value);
            }
        }

        private string _suffix;
        public string Suffix
        {
            get => _suffix;
            set
            {
                _suffix = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ViewName));
            }
        }

        public string ViewName
        {
            get
            {
                if (SelectedLevel == null) return "";
                string s = string.IsNullOrWhiteSpace(Suffix) ? "" : $" {Suffix}";
                return $"{SelectedLevel.Name}{s}";
            }
        }

        private ViewTypeItem _selectedViewType;
        public ViewTypeItem SelectedViewType
        {
            get => _selectedViewType;
            set { _selectedViewType = value; OnPropertyChanged(); }
        }

        private ViewTemplateItem _selectedViewTemplate;
        public ViewTemplateItem SelectedViewTemplate
        {
            get => _selectedViewTemplate;
            set { _selectedViewTemplate = value; OnPropertyChanged(); }
        }

        private ScopeBoxItem _selectedScopeBox;
        public ScopeBoxItem SelectedScopeBox
        {
            get => _selectedScopeBox;
            set 
            { 
                if (_selectedScopeBox != value)
                {
                    _selectedScopeBox = value; 
                    OnPropertyChanged(); 
                    ScopeBoxChangedCallback?.Invoke(this, value);
                }
            }
        }

        private string _sheetNumber;
        public string SheetNumber
        {
            get => _sheetNumber;
            set 
            { 
                if (_sheetNumber != value)
                {
                    _sheetNumber = value; 
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(SheetNumberAndName));
                    SheetNumberChangedCallback?.Invoke(this, value);
                }
            }
        }

        private string _sheetName;
        public string SheetName
        {
            get => _sheetName;
            set
            {
                if (_sheetName != value)
                {
                    _sheetName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SheetNumberAndName));
                    SheetNameChangedCallback?.Invoke(this, value);
                }
            }
        }

        public string GroupId { get; set; }
        public Action<AutoViewSheetRow, LevelItem> LevelChangedCallback { get; set; }
        public Action<AutoViewSheetRow, string> SheetNumberChangedCallback { get; set; }
        public Action<AutoViewSheetRow, string> SheetNameChangedCallback { get; set; }
        public Action<AutoViewSheetRow, ScopeBoxItem> ScopeBoxChangedCallback { get; set; }
        public Action<AutoViewSheetRow, bool> GroupSelectionChangedCallback { get; set; }
    }
}
