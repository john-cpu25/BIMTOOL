using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.AlignTags
{
    public partial class AlignTagsViewModel : ObservableObject
    {
        private readonly ExternalEvent _externalEvent;
        private readonly AlignTagsExternalEventHandler _handler;

        [ObservableProperty]
        private string _statusMessage = "B1: Chọn Tag chuẩn | B2: Chọn các Tag cần căn chỉnh";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasReference))]
        private ElementId _referenceTagId;

        public bool HasReference => ReferenceTagId != null && ReferenceTagId != ElementId.InvalidElementId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TargetCount))]
        private List<ElementId> _targetTagIds = new List<ElementId>();

        public int TargetCount => TargetTagIds?.Count ?? 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAlignVertical))]
        private bool _isAlignHorizontal = true;

        public bool IsAlignVertical
        {
            get => !IsAlignHorizontal;
            set => IsAlignHorizontal = !value;
        }

        public AlignTagsViewModel(AlignTagsExternalEventHandler handler)
        {
            _handler = handler;
            _externalEvent = ExternalEvent.Create(_handler);
        }

        [RelayCommand]
        private void PickReference()
        {
            _handler.Action = "PickReference";
            _handler.ViewModel = this;
            _externalEvent.Raise();
            StatusMessage = "Pick a reference tag in Revit...";
        }

        [RelayCommand]
        private void PickTargets()
        {
            _handler.Action = "PickTargets";
            _handler.ViewModel = this;
            _externalEvent.Raise();
            StatusMessage = "Select target tags to align (ESC when finished)...";
        }

        [RelayCommand]
        private void Align()
        {
            if (!HasReference)
            {
                StatusMessage = "Error: Please pick a reference tag first.";
                return;
            }

            if (TargetCount == 0)
            {
                StatusMessage = "Error: Please select target tags to align.";
                return;
            }

            _handler.Action = IsAlignHorizontal ? "AlignHorizontal" : "AlignVertical";
            _handler.ReferenceTagId = ReferenceTagId;
            _handler.TargetTagIds = TargetTagIds;
            _externalEvent.Raise();
            
            StatusMessage = $"Aligning {TargetCount} tags...";
        }

        public void UpdateSelection(ElementId refId, List<ElementId> targetIds)
        {
            if (refId != null) ReferenceTagId = refId;
            if (targetIds != null) TargetTagIds = targetIds;
            
            string refStatus = HasReference ? "Chuẩn: Đã chọn" : "B1: Chọn Tag chuẩn";
            string targetStatus = TargetCount > 0 ? $"Đã chọn {TargetCount} tag" : "B2: Chọn các Tag";

            StatusMessage = $"{refStatus} | {targetStatus} | B4: Thực thi";
        }
    }
}
