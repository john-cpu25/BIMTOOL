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

    public partial class MultiAddRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected = true;

        public SheetItem Sheet { get; set; }

        [ObservableProperty]
        private ViewItem _archView;

        [ObservableProperty]
        private ViewItem _underView;

        [ObservableProperty]
        private ViewItem _overView;

        [ObservableProperty]
        private string _status = "—";

        public void UpdateStatus()
        {
            int count = 0;
            if (ArchView != null) count++;
            if (UnderView != null) count++;
            if (OverView != null) count++;

            if (count == 3) Status = "✓ OK";
            else if (count > 0) Status = $"⚠ {count}/3";
            else Status = "✗ No Match";
        }

        partial void OnArchViewChanged(ViewItem value) => UpdateStatus();
        partial void OnUnderViewChanged(ViewItem value) => UpdateStatus();
        partial void OnOverViewChanged(ViewItem value) => UpdateStatus();
    }

    public class ViewportTitleItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public partial class ViewportTitleRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string ViewName { get; set; }
        public string ViewType { get; set; }  // FloorPlan, CeilingPlan, Section, etc.
        public string CurrentTitle { get; set; }
        public string SheetNumber { get; set; }  // Empty if not on sheet
        public bool IsOnSheet { get; set; }
        public ElementId ViewportId { get; set; }  // Viewport element id
        public ElementId ViewId { get; set; }
    }

    public class ScopeBoxItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public partial class ScopeBoxViewRow : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string ViewName { get; set; }
        public string ViewType { get; set; }
        public string CurrentScopeBox { get; set; }
        public ElementId ViewId { get; set; }
    }
}
