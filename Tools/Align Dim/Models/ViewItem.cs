using System;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoNhan.Tools.Align_Dim.Models
{
    public partial class ViewItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public string ViewName { get; set; }
        public ElementId ViewId { get; set; }
        public string ViewType { get; set; }

        public ViewItem(View view)
        {
            ViewName = view.Name;
            ViewId = view.Id;
            ViewType = view.ViewType.ToString();
            IsSelected = false;
        }
    }
}
