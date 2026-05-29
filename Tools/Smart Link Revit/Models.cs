using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.SmartLinkRevit
{
    public class HostViewInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name { get; set; }
        public View View { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LinkInstanceInfo
    {
        public string Name { get; set; }
        public RevitLinkInstance Instance { get; set; }
    }

    public class LinkedViewInfo
    {
        public string Name { get; set; }
        public ElementId ViewId { get; set; }
    }
}
