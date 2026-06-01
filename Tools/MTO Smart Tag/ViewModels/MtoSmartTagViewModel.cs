using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.MtoSmartTag.ViewModels
{
    public partial class TagTypeItem : ObservableObject
    {
        public FamilySymbol Symbol { get; }
        public string DisplayName => $"{Symbol.FamilyName} : {Symbol.Name}";
        public ElementId Id => Symbol.Id;

        public TagTypeItem(FamilySymbol symbol)
        {
            Symbol = symbol;
        }
    }

    public partial class DirectionItem : ObservableObject
    {
        public OffsetDirection Direction { get; }
        public string DisplayName { get; }
        public string Icon { get; }

        public DirectionItem(OffsetDirection direction, string displayName, string icon)
        {
            Direction = direction;
            DisplayName = displayName;
            Icon = icon;
        }
    }

    public partial class TargetFamilyItem : ObservableObject
    {
        public string Name { get; }

        [ObservableProperty]
        private bool _isSelected;

        public TargetFamilyItem(string name, bool isSelected = false)
        {
            Name = name;
            _isSelected = isSelected;
        }
    }

    public partial class MtoSmartTagViewModel : ObservableObject
    {
        private MtoSmartTagHandler _handler;
        private ExternalEvent _externalEvent;

        // Tag Types
        public ObservableCollection<TagTypeItem> TagTypes { get; set; }

        [ObservableProperty]
        private TagTypeItem _selectedTagType;

        // Target Families (which families to tag/layer)
        public ObservableCollection<TargetFamilyItem> TargetFamilies { get; set; }

        // Direction Options
        public ObservableCollection<DirectionItem> Directions { get; set; }

        [ObservableProperty]
        private DirectionItem _selectedDirection;

        // Offset Distance (mm)
        [ObservableProperty]
        private double _offsetDistance = 150;

        // Direct X/Y offset (mm)
        [ObservableProperty]
        private double _offsetX = 0;

        [ObservableProperty]
        private double _offsetY = 0;

        [ObservableProperty]
        private bool _useDirectXY = false;

        // Add Leader
        [ObservableProperty]
        private bool _addLeader = false;

        // Force Re-tag (ignore already tagged check)
        [ObservableProperty]
        private bool _forceRetag = true;

        // Only tag items that already have a tag
        [ObservableProperty]
        private bool _onlyAlreadyTagged = true;

        // Color Override
        [ObservableProperty]
        private bool _applyColorOverride = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewBrush))]
        private byte _colorR = 255;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewBrush))]
        private byte _colorG = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PreviewBrush))]
        private byte _colorB = 0;

        public System.Windows.Media.SolidColorBrush PreviewBrush =>
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(ColorR, ColorG, ColorB));

        // Status
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusVisible))]
        private string _statusMessage;

        public bool StatusVisible => !string.IsNullOrEmpty(StatusMessage);

        // Count info
        [ObservableProperty]
        private string _itemCountInfo;

        public MtoSmartTagViewModel(Document doc, View activeView, MtoSmartTagHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);

            // Initialize directions
            Directions = new ObservableCollection<DirectionItem>
            {
                new DirectionItem(OffsetDirection.TopLeft, "Top-Left", "↖"),
                new DirectionItem(OffsetDirection.Top, "Top", "↑"),
                new DirectionItem(OffsetDirection.TopRight, "Top-Right", "↗"),
                new DirectionItem(OffsetDirection.Left, "Left", "←"),
                new DirectionItem(OffsetDirection.Right, "Right", "→"),
                new DirectionItem(OffsetDirection.BottomLeft, "Bottom-Left", "↙"),
                new DirectionItem(OffsetDirection.Bottom, "Bottom", "↓"),
                new DirectionItem(OffsetDirection.BottomRight, "Bottom-Right", "↘"),
            };
            SelectedDirection = Directions.First(); // Default: TopLeft

            // Find Detail Item Tag families
            var tagFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category != null &&
                             fs.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_DetailComponentTags)
                .OrderByDescending(fs => fs.FamilyName.Contains("RINCO_TAG_Reo") || fs.FamilyName.Contains("Reo Tag_Mark")) // Prioritize known tag families
                .ThenBy(fs => fs.FamilyName)
                .ThenBy(fs => fs.Name)
                .ToList();

            TagTypes = new ObservableCollection<TagTypeItem>(tagFamilies.Select(fs => new TagTypeItem(fs)));
            SelectedTagType = TagTypes.FirstOrDefault();

            // Find distinct Detail Item families in the view (for target family selection)
            var detailFamilies = new FilteredElementCollector(doc, activeView.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Select(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName;
                })
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            TargetFamilies = new ObservableCollection<TargetFamilyItem>(
                detailFamilies.Select(f => 
                {
                    bool isDefaultSelected = f.Contains("Reinforcement_Distribution") || f.Contains("ZBar");
                    var item = new TargetFamilyItem(f, isDefaultSelected);
                    item.PropertyChanged += (s, e) => 
                    {
                        if (e.PropertyName == nameof(TargetFamilyItem.IsSelected))
                        {
                            UpdateItemCount(doc, activeView);
                        }
                    };
                    return item;
                })
            );

            // If no default matched, select first
            if (!TargetFamilies.Any(f => f.IsSelected) && TargetFamilies.Any())
            {
                TargetFamilies.First().IsSelected = true;
            }

            // Count items in view (for selected families)
            UpdateItemCount(doc, activeView);
        }

        private void UpdateItemCount(Document doc, View view)
        {
            var selectedFamilies = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();

            if (!selectedFamilies.Any())
            {
                ItemCountInfo = "No target families selected";
                return;
            }

            int count = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Where(e =>
                {
                    var typeId = e.GetTypeId();
                    var type = doc.GetElement(typeId) as FamilySymbol;
                    return type?.FamilyName != null && selectedFamilies.Contains(type.FamilyName);
                })
                .Count();

            ItemCountInfo = $"Found {count} items for selected families in current view";
        }

        private Action<string> GetStatusNotifier()
        {
            return msg =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = msg;
                });
            };
        }

        [RelayCommand]
        private void TagAll()
        {
            if (SelectedTagType == null)
            {
                StatusMessage = "Please select a Tag Type.";
                return;
            }

            if (SelectedDirection == null)
            {
                StatusMessage = "Please select an offset direction.";
                return;
            }

            _handler.Action = "TagAll";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.SelectedTagTypeId = SelectedTagType.Id;
            _handler.Direction = SelectedDirection.Direction;
            _handler.OffsetDistanceMm = OffsetDistance;
            _handler.UseDirectOffset = UseDirectXY;
            _handler.OffsetXMm = OffsetX;
            _handler.OffsetYMm = OffsetY;
            _handler.AddLeader = AddLeader;
            _handler.ForceRetag = ForceRetag;
            _handler.OnlyAlreadyTagged = OnlyAlreadyTagged;
            _handler.ApplyColorOverride = ApplyColorOverride;
            _handler.ColorR = ColorR;
            _handler.ColorG = ColorG;
            _handler.ColorB = ColorB;
            _handler.NotifyStatus = GetStatusNotifier();

            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ResetColor()
        {
            _handler.Action = "ResetColor";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }

        // ===== Layer X/Y Commands =====

        [RelayCommand]
        private void ShowLayerX()
        {
            _handler.Action = "ShowLayer";
            _handler.LayerDirection = "X";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void HideLayerX()
        {
            _handler.Action = "HideLayer";
            _handler.LayerDirection = "X";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ShowLayerY()
        {
            _handler.Action = "ShowLayer";
            _handler.LayerDirection = "Y";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void HideLayerY()
        {
            _handler.Action = "HideLayer";
            _handler.LayerDirection = "Y";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void ShowAllLayers()
        {
            _handler.Action = "ShowAll";
            _handler.TargetFamilyNames = TargetFamilies.Where(f => f.IsSelected).Select(f => f.Name).ToList();
            _handler.NotifyStatus = GetStatusNotifier();
            _externalEvent.Raise();
        }
    }
}
