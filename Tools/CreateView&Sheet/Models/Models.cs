using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoNhan.Tools.CreateViewSheet.Models
{
    public class LevelItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        public double Elevation { get; set; }
        
        public string DisplayName => $"{Name} ({Math.Round(Elevation * 304.8, 1)} mm)";
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

    public class TitleBlockItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class ViewItem : ObservableObject
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class SheetItem : ObservableObject
    {
        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string SheetSeries { get; set; }
        public ElementId Id { get; set; }
        
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public partial class CreateViewRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private LevelItem _selectedLevel;

        [ObservableProperty]
        private string _suffix = "";

        [ObservableProperty]
        private ViewTypeItem _selectedViewType;

        [ObservableProperty]
        private ViewTemplateItem _selectedViewTemplate;
        
        [ObservableProperty]
        private DuplicateMode _duplicateMode = DuplicateMode.DuplicateWithDetailing;

        public string GroupId { get; set; } = Guid.NewGuid().ToString();
        public Action<CreateViewRow, LevelItem> OnLevelChangedCallback { get; set; }

        partial void OnSelectedLevelChanged(LevelItem value)
        {
            OnLevelChangedCallback?.Invoke(this, value);
        }
    }

    public partial class CreateSheetRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        [ObservableProperty]
        private LevelItem _selectedLevel;

        [ObservableProperty]
        private string _suffix = "";
        
        [ObservableProperty]
        private string _sheetNumber;

        [ObservableProperty]
        private string _sheetName;

        public string GroupId { get; set; } = Guid.NewGuid().ToString();
        public Action<CreateSheetRow, LevelItem> OnLevelChangedCallback { get; set; }

        partial void OnSelectedLevelChanged(LevelItem value)
        {
            UpdateSheetName();
            OnLevelChangedCallback?.Invoke(this, value);
        }

        partial void OnSuffixChanged(string value)
        {
            UpdateSheetName();
        }

        private void UpdateSheetName()
        {
            if (SelectedLevel == null) return;
            string suffixPart = string.IsNullOrEmpty(Suffix) ? "" : $" {Suffix}";
            SheetName = $"{SelectedLevel.Name}{suffixPart}";
        }
    }

    public enum DuplicateMode
    {
        Duplicate,
        DuplicateWithDetailing,
        DuplicateAsDependent
    }
}
