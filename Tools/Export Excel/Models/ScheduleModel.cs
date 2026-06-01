using System;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoNhan.Tools.ExportExcel.Models
{
    public partial class ScheduleModel : ObservableObject
    {
        public ViewSchedule Schedule { get; }
        public string Name { get; }
        public int Id { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        public ScheduleModel(ViewSchedule schedule)
        {
            Schedule = schedule;
            Name = schedule.Name;
#if REVIT2024_OR_GREATER
            Id = (int)schedule.Id.Value;
#else
            Id = schedule.Id.IntegerValue;
#endif
        }
    }
}
