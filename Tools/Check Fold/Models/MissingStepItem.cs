using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.CheckFold.Models
{
    public class MissingStepItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public ElementId HighSlabId { get; set; }
        public string HighSlabInfo { get; set; }
        public ElementId LowSlabId { get; set; }
        public string LowSlabInfo { get; set; }
        public double StepHeight { get; set; }
        public Curve StepEdge { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private string _status = "Missing";
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
