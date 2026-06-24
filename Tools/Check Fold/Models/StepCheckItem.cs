using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RincoModeling.Tools.CheckFold.Models
{
    public partial class StepCheckItem : ObservableObject
    {
        /// <summary>ElementId of the RINCO_AN_Step family instance</summary>
        public ElementId StepFamilyId { get; set; }

        /// <summary>Family type name</summary>
        public string TypeName { get; set; }

        /// <summary>Associated fold type name (nearest fold)</summary>
        public string FoldTypeName { get; set; }

        public System.Collections.Generic.List<ElementId> AllSlabIds { get; set; } = new System.Collections.Generic.List<ElementId>();

        /// <summary>"RL STEP" text parameter value (displayed on view, e.g. "30")</summary>
        [ObservableProperty]
        private string _currentRLValue;

        /// <summary>"Height Offset From Level" value in mm (e.g. -20.0)</summary>
        [ObservableProperty]
        private double _currentOffsetValue;

        /// <summary>Calculated step height = High slab top - Low slab top (mm)</summary>
        public double CalculatedValue { get; set; }

        /// <summary>Whether one of the slabs is sloped (Varies)</summary>
        public bool IsVaries { get; set; }

        public string CalculatedValueStr => IsVaries ? "VARIES" : CalculatedValue.ToString("F0");

        /// <summary>High slab info: "TypeName (offset mm)"</summary>
        public string HighSlabInfo { get; set; }

        public ElementId HighSlabId { get; set; }

        /// <summary>Low slab info: "TypeName (offset mm)"</summary>
        public string LowSlabInfo { get; set; }

        public ElementId LowSlabId { get; set; }

        /// <summary>Primary parameter name used for check</summary>
        public string ParameterName { get; set; }

        /// <summary>Status: "OK" or "Sai"</summary>
        [ObservableProperty]
        private string _status = "";

        /// <summary>Whether this item has been updated</summary>
        [ObservableProperty]
        private bool _isUpdated;

        [ObservableProperty]
        private bool _isSelected = true;
    }
}

