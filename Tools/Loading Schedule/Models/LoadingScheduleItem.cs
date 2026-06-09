using Autodesk.Revit.DB;

namespace RincoNhan.Tools.LoadingSchedule.Models
{
    /// <summary>
    /// Represents one row in the Loading Schedule legend table.
    /// Each row corresponds to a unique FilledRegionType found in the current view.
    /// </summary>
    public class LoadingScheduleItem
    {
        /// <summary>Row number (1-based), displayed in N.o column.</summary>
        public int Number { get; set; }

        /// <summary>FilledRegionType ElementId used to create the hatch preview.</summary>
        public ElementId FilledRegionTypeId { get; set; }

        /// <summary>Display name of the FilledRegionType.</summary>
        public string TypeName { get; set; }

        /// <summary>Whether this item is selected for inclusion in the legend.</summary>
        public bool IsSelected { get; set; } = true;

        /// <summary>Color string for display in the UI (R,G,B).</summary>
        public string ColorDisplay { get; set; } = "";

        /// <summary>Foreground pattern name for display in the UI.</summary>
        public string PatternName { get; set; } = "";
    }
}
