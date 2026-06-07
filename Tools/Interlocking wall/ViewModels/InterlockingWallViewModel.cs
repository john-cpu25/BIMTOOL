using System;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RincoNhan.Tools.InterlockingWall.ViewModels
{
    public partial class InterlockingWallViewModel : ObservableObject
    {
        private InterlockingWallExternalEventHandler _handler;

        // ===== Tab 1: Merge Walls =====
        [ObservableProperty] private string _wall1Name = "";
        [ObservableProperty] private string _wall1IdText = "";
        [ObservableProperty] private string _wall2Name = "";
        [ObservableProperty] private string _wall2IdText = "";
        [ObservableProperty] private bool _hasTwoWalls;

        public ElementId Wall1Id { get; set; }
        public ElementId Wall2Id { get; set; }

        // ===== Tab 2: Split Wall =====
        [ObservableProperty] private string _selectedWallName = "";
        [ObservableProperty] private string _selectedWallIdText = "";
        [ObservableProperty] private double _wallLengthMm;
        [ObservableProperty] private double _wallHeightMm;
        [ObservableProperty] private bool _hasWallToSplit;

        // Split settings
        [ObservableProperty] private int _panelCount = 2;
        [ObservableProperty] private bool _isEqualSplit = true;
        [ObservableProperty] private string _startPointText = "";
        [ObservableProperty] private string _endPointText = "";
        [ObservableProperty] private bool _isWallReversed; // true if wall goes right-to-left
        // Display labels swap when reversed
        [ObservableProperty] private string _leftLabel = "START";
        [ObservableProperty] private string _leftCoord = "";
        [ObservableProperty] private string _rightLabel = "END";
        [ObservableProperty] private string _rightCoord = "";

        // Segments collection for UI display
        public ObservableCollection<SegmentInfo> Segments { get; } = new ObservableCollection<SegmentInfo>();

        public ElementId SplitWallId { get; set; }

        // ===== Tab 3: Group Wall =====
        [ObservableProperty] private bool _hasGroupSelected;
        [ObservableProperty] private string _groupName = "";
        [ObservableProperty] private string _groupIdText = "";
        public ElementId SelectedGroupId { get; set; }

        // 4 wall info
        [ObservableProperty] private string _groupWall1Info = "";
        [ObservableProperty] private string _groupWall2Info = "";
        [ObservableProperty] private string _groupWall3Info = "";
        [ObservableProperty] private string _groupWall4Info = "";

        // Wall 3 split settings
        [ObservableProperty] private int _wall3PanelCount = 3;
        [ObservableProperty] private bool _isWall3EqualSplit = false;
        [ObservableProperty] private double _wall3LengthMm; // Effective length (includes W1+W2 thickness)
        public ObservableCollection<SegmentInfo> Wall3Segments { get; } = new ObservableCollection<SegmentInfo>();

        // Wall 4 split settings
        [ObservableProperty] private int _wall4PanelCount = 3;
        [ObservableProperty] private bool _isWall4EqualSplit = false;
        [ObservableProperty] private double _wall4LengthMm; // Effective length (includes W1+W2 thickness)
        public ObservableCollection<SegmentInfo> Wall4Segments { get; } = new ObservableCollection<SegmentInfo>();

        // Store the identified wall element IDs within the group
        public ElementId GroupWall1Id { get; set; }
        public ElementId GroupWall2Id { get; set; }
        public ElementId GroupWall3Id { get; set; }
        public ElementId GroupWall4Id { get; set; }

        // Wall thicknesses (mm) - for effective length calculation
        public double Wall1ThicknessMm { get; set; }
        public double Wall2ThicknessMm { get; set; }
        public double Wall3ThicknessMm { get; set; }
        public double Wall4ThicknessMm { get; set; }

        // Actual curve lengths (mm) - without wall thickness overlap
        public double Wall1ActualLengthMm { get; set; }
        public double Wall2ActualLengthMm { get; set; }
        public double Wall3ActualLengthMm { get; set; }
        public double Wall4ActualLengthMm { get; set; }

        // ===== Common =====
        [ObservableProperty] private string _statusMessage = "";

        public InterlockingWallViewModel(InterlockingWallExternalEventHandler handler)
        {
            _handler = handler;
            _handler.ViewModel = this;
        }

        // ===== Tab 1 Commands =====
        [RelayCommand]
        private void PickTwoWalls()
        {
            _handler.RequestAction = "PICK_TWO_WALLS";
            _handler.Raise();
        }

        [RelayCommand]
        private void JoinWalls()
        {
            if (!HasTwoWalls) return;
            _handler.RequestAction = "JOIN_WALLS";
            _handler.Raise();
        }

        // ===== Tab 2 Commands =====
        [RelayCommand]
        private void PickWallToSplit()
        {
            _handler.RequestAction = "PICK_WALL_TO_SPLIT";
            _handler.Raise();
        }

        [RelayCommand]
        private void SplitWall()
        {
            if (!HasWallToSplit) return;
            _handler.RequestAction = "SPLIT_WALL";
            _handler.Raise();
        }

        // ===== Tab 3 Commands =====
        [RelayCommand]
        private void PickGroup()
        {
            _handler.RequestAction = "PICK_GROUP";
            _handler.Raise();
        }

        [RelayCommand]
        private void ExecuteGroup()
        {
            if (!HasGroupSelected) return;
            _handler.RequestAction = "EXECUTE_GROUP";
            _handler.Raise();
        }

        // ===== Data Loading =====
        public void LoadTwoWalls(Wall wall1, Wall wall2)
        {
            Wall1Id = wall1.Id;
            Wall1Name = wall1.Name;
            Wall1IdText = wall1.Id.ToString();

            Wall2Id = wall2.Id;
            Wall2Name = wall2.Name;
            Wall2IdText = wall2.Id.ToString();

            HasTwoWalls = true;
            SetStatus($"Selected: {Wall1Name} + {Wall2Name}", true);
        }

        public void LoadWallToSplit(Wall wall)
        {
            SplitWallId = wall.Id;
            SelectedWallName = wall.Name;
            SelectedWallIdText = wall.Id.ToString();

            // Get wall dimensions
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve != null)
            {
                double lengthFt = locCurve.Curve.Length;
#if REVIT2021_OR_GREATER
                WallLengthMm = UnitUtils.ConvertFromInternalUnits(lengthFt, UnitTypeId.Millimeters);
#else
                WallLengthMm = UnitUtils.ConvertFromInternalUnits(lengthFt, DisplayUnitType.DUT_MILLIMETERS);
#endif

                // Capture start/end coordinates for UI display
                var startPt = locCurve.Curve.GetEndPoint(0);
                var endPt = locCurve.Curve.GetEndPoint(1);
#if REVIT2021_OR_GREATER
                double sx = UnitUtils.ConvertFromInternalUnits(startPt.X, UnitTypeId.Millimeters);
                double sy = UnitUtils.ConvertFromInternalUnits(startPt.Y, UnitTypeId.Millimeters);
                double ex = UnitUtils.ConvertFromInternalUnits(endPt.X, UnitTypeId.Millimeters);
                double ey = UnitUtils.ConvertFromInternalUnits(endPt.Y, UnitTypeId.Millimeters);
#else
                double sx = UnitUtils.ConvertFromInternalUnits(startPt.X, DisplayUnitType.DUT_MILLIMETERS);
                double sy = UnitUtils.ConvertFromInternalUnits(startPt.Y, DisplayUnitType.DUT_MILLIMETERS);
                double ex = UnitUtils.ConvertFromInternalUnits(endPt.X, DisplayUnitType.DUT_MILLIMETERS);
                double ey = UnitUtils.ConvertFromInternalUnits(endPt.Y, DisplayUnitType.DUT_MILLIMETERS);
#endif
                StartPointText = $"({sx:F0}, {sy:F0})";
                EndPointText = $"({ex:F0}, {ey:F0})";

                // Detect wall direction: if START.X > END.X, wall goes right-to-left
                IsWallReversed = sx > ex;

                if (IsWallReversed)
                {
                    // Wall goes right-to-left: START on right, END on left
                    LeftLabel = "END";
                    LeftCoord = EndPointText;
                    RightLabel = "START";
                    RightCoord = StartPointText;
                }
                else
                {
                    // Wall goes left-to-right: START on left, END on right
                    LeftLabel = "START";
                    LeftCoord = StartPointText;
                    RightLabel = "END";
                    RightCoord = EndPointText;
                }
            }

            double heightFt = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
#if REVIT2021_OR_GREATER
            WallHeightMm = UnitUtils.ConvertFromInternalUnits(heightFt, UnitTypeId.Millimeters);
#else
            WallHeightMm = UnitUtils.ConvertFromInternalUnits(heightFt, DisplayUnitType.DUT_MILLIMETERS);
#endif

            PanelCount = 2;
            IsEqualSplit = true;
            HasWallToSplit = true;
            RecalculateSegments();

            SetStatus($"Selected wall: {SelectedWallName} ({WallLengthMm:F0}mm)", true);
        }

        partial void OnPanelCountChanged(int value)
        {
            if (value < 2) PanelCount = 2;
            if (value > 20) PanelCount = 20;
            RecalculateSegments();
        }

        partial void OnIsEqualSplitChanged(bool value)
        {
            RecalculateSegments();
        }

        private bool _isRecalculating;
        private const double PREVIEW_TOTAL_WIDTH = 380.0;

        public void RecalculateSegments()
        {
            if (WallLengthMm <= 0) return;

            int count = PanelCount;
            if (count < 2) count = 2;

            _isRecalculating = true;

            // Unsubscribe old segments
            foreach (var seg in Segments)
                seg.PropertyChanged -= OnSegmentPropertyChanged;

            double eachLength = Math.Round(WallLengthMm / count, 0);
            Segments.Clear();
            for (int i = 0; i < count; i++)
            {
                double len = (i == count - 1)
                    ? WallLengthMm - eachLength * (count - 1)
                    : eachLength;

                var seg = new SegmentInfo
                {
                    Index = i + 1,
                    LengthMm = Math.Round(len, 0),
                    Ratio = len / WallLengthMm,
                    PreviewWidth = (len / WallLengthMm) * PREVIEW_TOTAL_WIDTH
                };
                seg.PropertyChanged += OnSegmentPropertyChanged;
                Segments.Add(seg);
            }

            _isRecalculating = false;
        }

        private void OnSegmentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isRecalculating) return;
            if (IsEqualSplit) return;
            if (e.PropertyName != nameof(SegmentInfo.LengthMm)) return;
            if (Segments.Count < 2) return;

            _isRecalculating = true;

            var edited = sender as SegmentInfo;
            int editedIdx = Segments.IndexOf(edited);

            // Determine which segment to adjust
            // If user edited the last segment → adjust the second-to-last
            // If user edited any other → adjust the last segment
            int adjustIdx = (editedIdx == Segments.Count - 1)
                ? Segments.Count - 2
                : Segments.Count - 1;

            // Sum all segments except the one being adjusted
            double sumOthers = 0;
            for (int i = 0; i < Segments.Count; i++)
            {
                if (i != adjustIdx)
                    sumOthers += Segments[i].LengthMm;
            }

            // Adjust target segment = Total - sum(others)
            double adjustLen = WallLengthMm - sumOthers;
            if (adjustLen < 0) adjustLen = 0;

            Segments[adjustIdx].LengthMm = Math.Round(adjustLen, 0);

            // Update all ratios and preview widths
            for (int i = 0; i < Segments.Count; i++)
            {
                Segments[i].Ratio = Segments[i].LengthMm / WallLengthMm;
                Segments[i].PreviewWidth = Segments[i].Ratio * PREVIEW_TOTAL_WIDTH;
            }

            _isRecalculating = false;
        }

        /// <summary>
        /// Get split ratios (cumulative) for the handler to use when splitting.
        /// Returns positions like [0.333, 0.667] for 3 equal segments.
        /// </summary>
        public double[] GetSplitRatios()
        {
            double[] ratios = new double[Segments.Count - 1];
            double cumulative = 0;
            for (int i = 0; i < Segments.Count - 1; i++)
            {
                cumulative += Segments[i].Ratio;
                ratios[i] = cumulative;
            }
            return ratios;
        }

        // ===== Tab 3: Data Loading =====
        public void LoadGroupWalls(Group group, Wall w1, Wall w2, Wall w3, Wall w4)
        {
            SelectedGroupId = group.Id;
            GroupName = group.Name;
            GroupIdText = group.Id.ToString();

            GroupWall1Id = w1.Id;
            GroupWall2Id = w2.Id;
            GroupWall3Id = w3.Id;
            GroupWall4Id = w4.Id;

            // Get actual curve lengths
            Wall1ActualLengthMm = GetWallLengthMm(w1);
            Wall2ActualLengthMm = GetWallLengthMm(w2);
            Wall3ActualLengthMm = GetWallLengthMm(w3);
            Wall4ActualLengthMm = GetWallLengthMm(w4);

            // Get wall thicknesses
            Wall1ThicknessMm = GetWallThicknessMm(w1);
            Wall2ThicknessMm = GetWallThicknessMm(w2);
            Wall3ThicknessMm = GetWallThicknessMm(w3);
            Wall4ThicknessMm = GetWallThicknessMm(w4);

            // Calculate effective panel lengths (includes perpendicular wall thicknesses)
            // W3/W4 (horizontal): effective = curve + W1 thickness + W2 thickness
            // W1/W2 (vertical):   effective = curve + W3 thickness + W4 thickness
            double w1EffectiveLength = Wall1ActualLengthMm + Wall3ThicknessMm + Wall4ThicknessMm;
            double w2EffectiveLength = Wall2ActualLengthMm + Wall3ThicknessMm + Wall4ThicknessMm;
            double w3EffectiveLength = Wall3ActualLengthMm + Wall1ThicknessMm + Wall2ThicknessMm;
            double w4EffectiveLength = Wall4ActualLengthMm + Wall1ThicknessMm + Wall2ThicknessMm;

            // Format wall info with effective lengths
            GroupWall1Info = FormatWallInfoEx("Wall 1 (Left)", w1, w1EffectiveLength);
            GroupWall2Info = FormatWallInfoEx("Wall 2 (Right)", w2, w2EffectiveLength);
            GroupWall3Info = FormatWallInfoEx("Wall 3 (Bottom)", w3, w3EffectiveLength);
            GroupWall4Info = FormatWallInfoEx("Wall 4 (Top)", w4, w4EffectiveLength);

            // Set effective lengths for split calculations
            Wall3LengthMm = w3EffectiveLength;
            Wall4LengthMm = w4EffectiveLength;

            Wall3PanelCount = 3;
            Wall4PanelCount = 3;
            IsWall3EqualSplit = false;
            IsWall4EqualSplit = false;
            HasGroupSelected = true;

            RecalculateGroupSegments(3, Wall3Segments, Wall3LengthMm);
            RecalculateGroupSegments(3, Wall4Segments, Wall4LengthMm);

            SetStatus($"Group loaded: {GroupName} - Panel lengths include wall thicknesses", true);
        }

        private string FormatWallInfoEx(string label, Wall wall, double effectiveLengthMm)
        {
            double thicknessMm = GetWallThicknessMm(wall);
            return $"{label}: {wall.Name} - Panel: {effectiveLengthMm:F0}mm (t={thicknessMm:F0}mm)";
        }

        private double GetWallLengthMm(Wall wall)
        {
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return 0;
            double lengthFt = locCurve.Curve.Length;
#if REVIT2021_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(lengthFt, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertFromInternalUnits(lengthFt, DisplayUnitType.DUT_MILLIMETERS);
#endif
        }

        private double GetWallThicknessMm(Wall wall)
        {
            double widthFt = wall.Width; // Wall.Width returns thickness in feet
#if REVIT2021_OR_GREATER
            return UnitUtils.ConvertFromInternalUnits(widthFt, UnitTypeId.Millimeters);
#else
            return UnitUtils.ConvertFromInternalUnits(widthFt, DisplayUnitType.DUT_MILLIMETERS);
#endif
        }

        partial void OnWall3PanelCountChanged(int value)
        {
            if (value < 2) Wall3PanelCount = 2;
            if (value > 20) Wall3PanelCount = 20;
            RecalculateGroupSegments(Wall3PanelCount, Wall3Segments, Wall3LengthMm);
        }

        partial void OnWall4PanelCountChanged(int value)
        {
            if (value < 2) Wall4PanelCount = 2;
            if (value > 20) Wall4PanelCount = 20;
            RecalculateGroupSegments(Wall4PanelCount, Wall4Segments, Wall4LengthMm);
        }

        partial void OnIsWall3EqualSplitChanged(bool value)
        {
            RecalculateGroupSegments(Wall3PanelCount, Wall3Segments, Wall3LengthMm);
        }

        partial void OnIsWall4EqualSplitChanged(bool value)
        {
            RecalculateGroupSegments(Wall4PanelCount, Wall4Segments, Wall4LengthMm);
        }

        private const double GROUP_PREVIEW_WIDTH = 300.0;

        public void RecalculateGroupSegments(int count, ObservableCollection<SegmentInfo> segments, double totalLengthMm)
        {
            if (totalLengthMm <= 0) return;
            if (count < 2) count = 2;

            _isRecalculating = true;

            foreach (var seg in segments)
                seg.PropertyChanged -= OnGroupSegmentPropertyChanged;

            double eachLength = Math.Round(totalLengthMm / count, 0);
            segments.Clear();
            for (int i = 0; i < count; i++)
            {
                double len = (i == count - 1)
                    ? totalLengthMm - eachLength * (count - 1)
                    : eachLength;

                var seg = new SegmentInfo
                {
                    Index = i + 1,
                    LengthMm = Math.Round(len, 0),
                    Ratio = len / totalLengthMm,
                    PreviewWidth = (len / totalLengthMm) * GROUP_PREVIEW_WIDTH
                };
                seg.PropertyChanged += OnGroupSegmentPropertyChanged;
                segments.Add(seg);
            }

            _isRecalculating = false;
        }

        private void OnGroupSegmentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_isRecalculating) return;
            if (e.PropertyName != nameof(SegmentInfo.LengthMm)) return;

            // Determine which collection this segment belongs to
            var seg = sender as SegmentInfo;
            ObservableCollection<SegmentInfo> segments;
            double totalLength;
            bool isEqual;

            if (Wall3Segments.Contains(seg))
            {
                segments = Wall3Segments;
                totalLength = Wall3LengthMm;
                isEqual = IsWall3EqualSplit;
            }
            else if (Wall4Segments.Contains(seg))
            {
                segments = Wall4Segments;
                totalLength = Wall4LengthMm;
                isEqual = IsWall4EqualSplit;
            }
            else return;

            if (isEqual) return;
            if (segments.Count < 2) return;

            _isRecalculating = true;

            int editedIdx = segments.IndexOf(seg);
            int adjustIdx = (editedIdx == segments.Count - 1)
                ? segments.Count - 2
                : segments.Count - 1;

            double sumOthers = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (i != adjustIdx)
                    sumOthers += segments[i].LengthMm;
            }

            double adjustLen = totalLength - sumOthers;
            if (adjustLen < 0) adjustLen = 0;

            segments[adjustIdx].LengthMm = Math.Round(adjustLen, 0);

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Ratio = segments[i].LengthMm / totalLength;
                segments[i].PreviewWidth = segments[i].Ratio * GROUP_PREVIEW_WIDTH;
            }

            _isRecalculating = false;
        }

        public double[] GetWall3SplitRatios()
        {
            // Convert from effective-length space to actual curve space
            // Effective = Actual + W1thickness + W2thickness
            // First W1thickness mm of effective space = overlap (before actual curve starts)
            return GetRatiosFromSegmentsWithOffset(Wall3Segments, Wall3LengthMm, Wall3ActualLengthMm, Wall1ThicknessMm);
        }

        public double[] GetWall4SplitRatios()
        {
            return GetRatiosFromSegmentsWithOffset(Wall4Segments, Wall4LengthMm, Wall4ActualLengthMm, Wall1ThicknessMm);
        }

        /// <summary>
        /// Convert split positions from effective-length space to actual wall curve ratios.
        /// The effective length = startThickness + actualLength + endThickness.
        /// Split positions in effective space need to be shifted by startThickness and then
        /// expressed as ratios of the actual curve length.
        /// </summary>
        private double[] GetRatiosFromSegmentsWithOffset(
            ObservableCollection<SegmentInfo> segments, 
            double effectiveLength, 
            double actualLength, 
            double startThicknessMm)
        {
            if (actualLength <= 0) return new double[0];

            double[] ratios = new double[segments.Count - 1];
            double cumulativeMm = 0;
            for (int i = 0; i < segments.Count - 1; i++)
            {
                cumulativeMm += segments[i].LengthMm;
                // Convert from effective space to curve space:
                // curvePosition = cumulativePosition - startThickness
                double curvePosMm = cumulativeMm - startThicknessMm;
                // Clamp to valid range on actual curve
                curvePosMm = Math.Max(0, Math.Min(curvePosMm, actualLength));
                ratios[i] = curvePosMm / actualLength;
            }
            return ratios;
        }

        private double[] GetRatiosFromSegments(ObservableCollection<SegmentInfo> segments)
        {
            double[] ratios = new double[segments.Count - 1];
            double cumulative = 0;
            for (int i = 0; i < segments.Count - 1; i++)
            {
                cumulative += segments[i].Ratio;
                ratios[i] = cumulative;
            }
            return ratios;
        }

        public void SetStatus(string msg, bool autoHide)
        {
            StatusMessage = msg;
            if (autoHide)
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                timer.Tick += (s, e) => { StatusMessage = ""; ((System.Windows.Threading.DispatcherTimer)s).Stop(); };
                timer.Start();
            }
        }
    }

    /// <summary>
    /// Represents one segment in the split preview.
    /// </summary>
    public partial class SegmentInfo : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private double _lengthMm;
        [ObservableProperty] private double _ratio;
        [ObservableProperty] private double _previewWidth;
    }
}
