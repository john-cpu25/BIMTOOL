using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RincoNhan.Tools.RebarColumn.Models;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.RebarColumn.ViewModels
{
    public partial class RebarColumnViewModel : ObservableObject
    {
        private ExternalEvent _externalEvent;

        public RebarColumnViewModel(ExternalEvent externalEvent = null)
        {
            _externalEvent = externalEvent;
            UpdateCalculations();
            UpdatePreview();
        }

        // Column Geometry (read from Revit)
        [ObservableProperty]
        private string _columnName = "";

        [ObservableProperty]
        private double _columnWidth = 0; // mm

        [ObservableProperty]
        private double _columnDepth = 0; // mm

        [ObservableProperty]
        private double _columnHeight = 0; // mm

        [ObservableProperty]
        private double _cover = 30; // mm

        [ObservableProperty]
        private int _countX = 3;

        [ObservableProperty]
        private int _countY = 3;

        [ObservableProperty]
        private double _mainDiameter = 20;

        [ObservableProperty]
        private double _stirrupDiameter = 10;

        [ObservableProperty]
        private double _lapFactor = 40;

        [ObservableProperty]
        private bool _isSeismic = false;

        [ObservableProperty]
        private double _spacing1 = 100;

        [ObservableProperty]
        private double _spacing2 = 200;

        [ObservableProperty]
        private double _spacing3 = 100;

        [ObservableProperty]
        private StirrupPattern _selectedPattern = StirrupPattern.Standard;

        [ObservableProperty]
        private double _slabHeight = 300; // mm

        [ObservableProperty]
        private double _topCover = 25; // mm

        [ObservableProperty]
        private double _otherCover = 25; // mm

        public ObservableCollection<StirrupPattern> Patterns { get; } = new ObservableCollection<StirrupPattern>(
            Enum.GetValues(typeof(StirrupPattern)).Cast<StirrupPattern>()
        );

        public ObservableCollection<double> MainDiameters { get; } = new ObservableCollection<double> { 16, 18, 20, 22, 25 };
        public ObservableCollection<double> StirrupDiameters { get; } = new ObservableCollection<double> { 8, 10, 12 };

        [ObservableProperty]
        private ObservableCollection<string> _availableShapes = new ObservableCollection<string>();

        [ObservableProperty]
        private string _selectedStirrupShapeName = "";

        // Properties for UI Preview
        [ObservableProperty]
        private ObservableCollection<Point> _barPositions = new ObservableCollection<Point>();

        [ObservableProperty]
        private ObservableCollection<PointCollection> _stirrupLines = new ObservableCollection<PointCollection>();

        [ObservableProperty]
        private double _previewWidth = 200;

        [ObservableProperty]
        private double _previewHeight = 300;

        // Tab 2: Thép móng (Foundation Rebar)
        [ObservableProperty]
        private bool _enableFoundationRebar = true;

        [ObservableProperty]
        private bool _splitRebarAtFooting = false;

        [ObservableProperty]
        private bool _useStirrupSpacing = true;

        [ObservableProperty]
        private double _footingStirrupSpacing = 200;

        [ObservableProperty]
        private double _footingDepthHm = 580;

        [ObservableProperty]
        private double _footingBendLb = 300;

        [RelayCommand]
        private void DirectionUp()
        {
            MessageBox.Show("Hướng thép móng: Lên trên", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DirectionDown()
        {
            MessageBox.Show("Hướng thép móng: Xuống dưới", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DirectionLeft()
        {
            MessageBox.Show("Hướng thép móng: Sang trái", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void DirectionRight()
        {
            MessageBox.Show("Hướng thép móng: Sang phải", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Tab 3: Thép đai (Stirrups)
        [ObservableProperty]
        private bool _spacingToBeamBottom = true;

        [ObservableProperty]
        private bool _spacingToMaxTop = false;

        [ObservableProperty]
        private bool _seismicSpacingByDist = true;

        [ObservableProperty]
        private double _seismicSpacingVal = 200;

        [ObservableProperty]
        private bool _seismicSpacingByCount = false;

        [ObservableProperty]
        private int _seismicCountVal = 3;

        public ObservableCollection<int> SeismicCounts { get; } = new ObservableCollection<int> { 2, 3, 4, 5 };

        [ObservableProperty]
        private double _stirrupStartOffset = 50;

        [ObservableProperty]
        private bool _spacingUniform = false;

        [ObservableProperty]
        private bool _spacingVariable = true;

        [ObservableProperty]
        private double _spacingValA1 = 100;

        [ObservableProperty]
        private double _spacingValA2 = 200;

        [ObservableProperty]
        private bool _l1UseMinVal = true;

        [ObservableProperty]
        private double _l1MinVal = 450;

        [ObservableProperty]
        private bool _l1UseHeightDiv = true;

        [ObservableProperty]
        private double _l1HeightDivVal = 5;

        [ObservableProperty]
        private bool _l1UseColSection = true;

        [ObservableProperty]
        private bool _applyAllAdditionalStirrup = true;

        partial void OnApplyAllAdditionalStirrupChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotApplyAllAdditionalStirrup));
        }

        public bool IsNotApplyAllAdditionalStirrup => !ApplyAllAdditionalStirrup;

        [ObservableProperty]
        private double _additionalStirrupDiameter = 6;

        [ObservableProperty]
        private double _additionalStirrupSpacing = 300;

        // Tab 4: Triển khai (Detailing / Sheets Layout)
        [ObservableProperty]
        private bool _enableDetailingLayout = true;

        public ObservableCollection<string> SheetNames { get; } = new ObservableCollection<string> { "CHI TIẾT CỘT", "CHI TIẾT CỘT 1", "CHI TIẾT CỘT 2" };

        [ObservableProperty]
        private string _selectedSheetName = "CHI TIẾT CỘT";

        public ObservableCollection<string> SheetNumbers { get; } = new ObservableCollection<string> { "S4-00.05", "S4-00.06", "S4-00.07" };

        [ObservableProperty]
        private string _selectedSheetNumber = "S4-00.05";

        public ObservableCollection<string> TitleBlocks { get; } = new ObservableCollection<string> { "A1 metric", "A0 metric", "A2 metric", "A3 metric" };

        [ObservableProperty]
        private string _selectedTitleBlock = "A1 metric";

        [ObservableProperty]
        private double _crossSectionScale = 25;

        public ObservableCollection<string> ViewTemplates { get; } = new ObservableCollection<string> { "@BS-MCN COT", "@BS-MCD COT", "None" };

        [ObservableProperty]
        private string _crossSectionTemplate = "@BS-MCN COT";

        public ObservableCollection<string> ViewFamilyTypes { get; } = new ObservableCollection<string> { "@RVS-MCN Cột", "@RVS_MCD-Cột", "Section" };

        [ObservableProperty]
        private string _crossSectionFamilyType = "@RVS-MCN Cột";

        public ObservableCollection<string> DimensionTypes { get; } = new ObservableCollection<string> { "@BS-Dim A3", "@BS-Dim A1", "Linear" };

        [ObservableProperty]
        private string _crossSectionDimType = "@BS-Dim A3";

        [ObservableProperty]
        private bool _stretchCrossSectionRebar = true;

        public ObservableCollection<double> TextSizes { get; } = new ObservableCollection<double> { 1.5, 1.8, 2.0, 2.5, 3.0 };

        [ObservableProperty]
        private double _crossSectionTextSize = 1.8;

        public ObservableCollection<string> StirrupTags { get; } = new ObservableCollection<string> { "A3_P_MRA_SL&DK_BOT", "None" };

        [ObservableProperty]
        private string _crossSectionStirrupTag = "A3_P_MRA_SL&DK_BOT";

        [ObservableProperty]
        private bool _crossSectionShowTag = false;

        [ObservableProperty]
        private bool _crossSectionShowVal = true;

        public ObservableCollection<string> TypeTags { get; } = new ObservableCollection<string> { "A3_T_RT_DK&KC_MID", "None" };

        [ObservableProperty]
        private string _longSectionTypeTag = "A3_T_RT_DK&KC_MID";

        [ObservableProperty]
        private double _longSectionScale = 50;

        public ObservableCollection<string> BreakLines { get; } = new ObservableCollection<string> { "1-25", "1-50", "None" };

        [ObservableProperty]
        private string _longSectionBreakLine = "1-25";

        [ObservableProperty]
        private string _longSectionTemplate = "@BS-MCD COT";

        [ObservableProperty]
        private string _longSectionFamilyType = "@RVS_MCD-Cột";

        [ObservableProperty]
        private string _longSectionDimType = "@BS-Dim A3";

        [ObservableProperty]
        private double _longSectionTextSize = 1.8;

        public ObservableCollection<string> StandardTags { get; } = new ObservableCollection<string> { "A3_T_RT_NhomThepBS", "None" };

        [ObservableProperty]
        private string _longSectionStandardTag = "A3_T_RT_NhomThepBS";

        [ObservableProperty]
        private bool _longSectionShowTag = true;

        [ObservableProperty]
        private bool _longSectionShowVal = true;

        partial void OnColumnWidthChanged(double value)
        {
            UpdatePreviewSize();
            UpdateCalculations();
        }

        partial void OnColumnDepthChanged(double value)
        {
            UpdatePreviewSize();
            UpdateCalculations();
        }

        private void UpdatePreviewSize()
        {
            if (ColumnWidth > 0 && ColumnDepth > 0)
            {
                // Keep the larger dimension at 200, scale the other proportionally
                double ratio = ColumnWidth / ColumnDepth;
                if (ratio >= 1)
                {
                    PreviewWidth = 200;
                    PreviewHeight = 200 / ratio;
                }
                else
                {
                    PreviewHeight = 200;
                    PreviewWidth = 200 * ratio;
                }
            }
            UpdatePreview();
        }

        partial void OnCountXChanged(int value)
        {
            UpdateCalculations();
            UpdatePreview();
        }

        partial void OnCountYChanged(int value)
        {
            UpdateCalculations();
            UpdatePreview();
        }

        partial void OnSelectedPatternChanged(StirrupPattern value)
        {
            OnPropertyChanged(nameof(IsStandardPattern));
            OnPropertyChanged(nameof(IsInternalLinksPattern));
            UpdateCalculations();
            UpdatePreview();
        }

        partial void OnCoverChanged(double value) => UpdatePreview();
        
        partial void OnMainDiameterChanged(double value)
        {
            UpdateCalculations();
            UpdatePreview();
        }

        partial void OnStirrupDiameterChanged(double value) => UpdatePreview();

        partial void OnIsSeismicChanged(bool value)
        {
            OnPropertyChanged(nameof(IsUniform));
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            BarPositions.Clear();
            StirrupLines.Clear();

            if (CountX < 2) CountX = 2;
            if (CountY < 2) CountY = 2;
            
            double dx = 1.0 / (CountX - 1);
            double dy = 1.0 / (CountY - 1);

            // 1. Calculate Bar Positions (Perimeter only)
            for (int i = 0; i < CountX; i++)
            {
                for (int j = 0; j < CountY; j++)
                {
                    // Only add if it's on the boundary (i is first/last OR j is first/last)
                    if (i == 0 || i == CountX - 1 || j == 0 || j == CountY - 1)
                    {
                        BarPositions.Add(new Point(i * dx, j * dy));
                    }
                }
            }

            // 2. Calculate Stirrup Lines
            // Outer loop
            var outerLoop = new PointCollection
            {
                new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1), new Point(0, 0)
            };
            StirrupLines.Add(outerLoop);

            // Internal Patterns
            if (SelectedPattern == StirrupPattern.WithInternalLinks)
            {
                // Connect internal bars to opposite sides
                for (int i = 1; i < CountX - 1; i++)
                {
                    var link = new PointCollection { new Point(i * dx, 0), new Point(i * dx, 1) };
                    StirrupLines.Add(link);
                }
            }
            else if (SelectedPattern == StirrupPattern.CrossTies)
            {
                for (int i = 1; i < CountX - 1; i++)
                {
                    StirrupLines.Add(new PointCollection { new Point(i * dx, 0), new Point(i * dx, 1) });
                }
                for (int j = 1; j < CountY - 1; j++)
                {
                    StirrupLines.Add(new PointCollection { new Point(0, j * dy), new Point(1, j * dy) });
                }
            }
        }

        [RelayCommand]
        private void Generate()
        {
            _externalEvent?.Raise();
        }

        // Calculated properties
        public int TotalMainBars => (2 * CountX + 2 * CountY - 4) >= 4 ? (2 * CountX + 2 * CountY - 4) : 4;

        public string TotalRebarText => $"{TotalMainBars}Ø{MainDiameter:0}";

        public double TotalRebarArea => TotalMainBars * Math.PI * Math.Pow(MainDiameter / 2.0, 2) / 100.0; // cm2

        public string ColumnDimensionsText => $"S = {ColumnWidth:0}x{ColumnDepth:0}";

        public double RebarRatio
        {
            get
            {
                if (ColumnWidth <= 0 || ColumnDepth <= 0) return 0;
                double totalAreaMm2 = TotalMainBars * Math.PI * Math.Pow(MainDiameter / 2.0, 2);
                double colAreaMm2 = ColumnWidth * ColumnDepth;
                return (totalAreaMm2 / colAreaMm2) * 100.0; // %
            }
        }

        public void UpdateCalculations()
        {
            OnPropertyChanged(nameof(TotalMainBars));
            OnPropertyChanged(nameof(TotalRebarText));
            OnPropertyChanged(nameof(TotalRebarArea));
            OnPropertyChanged(nameof(RebarRatio));
            OnPropertyChanged(nameof(ColumnDimensionsText));
        }

        // Radio button binders
        public bool IsUniform
        {
            get => !IsSeismic;
            set
            {
                if (value) IsSeismic = false;
            }
        }

        public bool IsStandardPattern
        {
            get => SelectedPattern == StirrupPattern.Standard;
            set { if (value) SelectedPattern = StirrupPattern.Standard; }
        }

        public bool IsInternalLinksPattern
        {
            get => SelectedPattern == StirrupPattern.WithInternalLinks;
            set { if (value) SelectedPattern = StirrupPattern.WithInternalLinks; }
        }
    }
}
