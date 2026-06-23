using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoModeling.Tools.CheckFold.Models
{
    public partial class FoldCheckItem : ObservableObject
    {
        /// <summary>
        /// ElementId of the fold floor
        /// </summary>
        public ElementId FoldFloorId { get; set; }

        /// <summary>
        /// Type name of the fold floor
        /// </summary>
        public string FoldTypeName { get; set; }

        /// <summary>
        /// Level name where the fold is placed
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// Fold floor's own thickness from its type (mm)
        /// </summary>
        public double FoldThickness { get; set; }

        /// <summary>
        /// Thickness of adjacent slab 1 - higher slab (mm)
        /// </summary>
        public double Slab1Thickness { get; set; }

        /// <summary>
        /// Type name of slab 1
        /// </summary>
        public string Slab1TypeName { get; set; }

        /// <summary>
        /// Thickness of adjacent slab 2 - lower slab (mm)
        /// </summary>
        public double Slab2Thickness { get; set; }

        /// <summary>
        /// Type name of slab 2
        /// </summary>
        public string Slab2TypeName { get; set; }

        /// <summary>
        /// Gap between the two slabs (mm)
        /// = |Bottom of high slab - Top of low slab|
        /// </summary>
        public double Gap { get; set; }

        /// <summary>
        /// Calculated total thickness = Slab1 + Slab2 + Gap (mm)
        /// </summary>
        public double CalculatedThickness { get; set; }

        /// <summary>
        /// Elevation of the higher slab top (mm)
        /// </summary>
        public double HighSlabElevation { get; set; }

        /// <summary>
        /// Elevation of the lower slab top (mm)
        /// </summary>
        public double LowSlabElevation { get; set; }

        /// <summary>
        /// Step height = HighSlabElevation - LowSlabElevation (mm)
        /// </summary>
        public double StepHeight { get; set; }

        /// <summary>
        /// Status: "OK" if FoldThickness matches CalculatedThickness, "Mismatch" otherwise
        /// </summary>
        public string Status { get; set; }

        [ObservableProperty]
        private bool _isSelected = true;
    }
}

