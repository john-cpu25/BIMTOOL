using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace RincoNhan.Core
{
    public static class RibbonManager
    {
        private static string _assemblyPath = Assembly.GetExecutingAssembly().Location;
        private static string _assemblyDir = Path.GetDirectoryName(_assemblyPath);
        internal static string ResourcesPath = Path.Combine(Directory.GetParent(_assemblyDir).FullName, "Resources");

        public static void SetupRibbon(UIControlledApplication application)
        {
            string tabName = "RincoNhan";

            // Create Ribbon Tab
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Ignore if tab already exists
            }

            // Define Panels
            RibbonPanel generalPanel = GetOrCreatePanel(application, tabName, "General");
            RibbonPanel layoutPanel = GetOrCreatePanel(application, tabName, "Layout");
            RibbonPanel excelPanel = GetOrCreatePanel(application, tabName, "Excel");
            RibbonPanel familyPanel = GetOrCreatePanel(application, tabName, "Family");
            RibbonPanel loadingPanel = GetOrCreatePanel(application, tabName, "Loading");
            RibbonPanel mtoPanel = GetOrCreatePanel(application, tabName, "MTO");
            RibbonPanel checkPanel = GetOrCreatePanel(application, tabName, "Check");
            RibbonPanel rebarPanel = GetOrCreatePanel(application, tabName, "Rebar");
            RibbonPanel linkPanel = GetOrCreatePanel(application, tabName, "Link");
            RibbonPanel wallPanel = GetOrCreatePanel(application, tabName, "Wall");
            RibbonPanel dimPanel = GetOrCreatePanel(application, tabName, "Dim");

            // === GENERAL Panel ===
            AddAddSharedParamButton(generalPanel);
            AddClashDetectionButton(generalPanel);
            AddFilterButton(generalPanel);
            AddCreateLevelButton(generalPanel);
            AddConnectionButton(generalPanel);
            AddJoinElementsButton(generalPanel);

            // === DIM Panel ===
            AddAlignDimButton(dimPanel);
            AddAutoDimGridButton(dimPanel);

            // === LAYOUT Panel ===
            AddCreateViewSheetButton(layoutPanel);
            AddAutoViewSheetButton(layoutPanel);
            AddDuplicateSheetSplitButton(layoutPanel);

            // === EXCEL Panel ===
            AddExportExcelButton(excelPanel);
            AddImportEXtoLegendButton(excelPanel);
            // === FAMILY Panel ===
            AddFamilyDataSplitButton(familyPanel);
            // === LOADING Panel ===
            AddLoadingHatchButton(loadingPanel);
            AddLoadingScheduleButton(loadingPanel);
            AddConvertHatchButton(loadingPanel);

            // === MTO Panel ===
            AddMtoGroupBarButton(mtoPanel);
            AddMtoQueryButton(mtoPanel);
            AddMtoSmartTagButton(mtoPanel);

            // === CHECK Panel ===
            AddQueryElementButton(checkPanel);
            AddAlignTagsButton(checkPanel);
            AddElementsTagsButton(checkPanel);
            AddCheckFoldButton(checkPanel);

            // === REBAR Panel ===
            AddRebarColumnButton(rebarPanel);
            AddStairDetailButton(rebarPanel);

            // === LINK Panel ===
            PushButtonData pbdOverrideCad = GetOverrideCadButtonData();
            PushButtonData pbdReloadCAD = GetReloadCADButtonData();
            PushButtonData pbdSmartLinkCad = GetSmartLinkCadButtonData();
            PushButtonData pbdSmartLinkRevit = GetSmartLinkRevitButtonData();

            linkPanel.AddStackedItems(pbdOverrideCad, pbdReloadCAD);
            linkPanel.AddStackedItems(pbdSmartLinkCad, pbdSmartLinkRevit);

            // === WALL Panel ===
            AddViewRefButton(wallPanel);
            AddWallDivideButton(wallPanel);
            AddCreateSectionWallButton(wallPanel);
            AddInterlockingWallButton(wallPanel);
            AddElevationViewButton(wallPanel);
        }
        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(tabName))
            {
                if (panel.Name == panelName) return panel;
            }
            return application.CreateRibbonPanel(tabName, panelName);
        }

        private static void AddFilterButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdFilter",
                "Advanced\nFilter",
                _assemblyPath,
                "RincoNhan.Tools.Filter.Command"
            );
            btnData.ToolTip = "Advanced object filter for project elements.";
            
            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("Filter.png");
            pb.Image = LoadIcon("Filter.png", 16);
        }

        private static void AddAlignDimButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdAlignDim",
                "Align\nDim",
                _assemblyPath,
                "RincoNhan.Tools.Align_Dim.AlignDimCommand"
            );
            btnData.ToolTip = "Align a dimension at a specific distance from a grid.";
            
            // Using a default icon or a specific one if provided
            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("AlignDim.png") ?? LoadIcon("AlignTags.png");
            pb.Image = LoadIcon("AlignDim.png", 16) ?? LoadIcon("AlignTags.png", 16);
        }

        private static void AddAutoDimGridButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdAutoDimension",
                "Auto\nDimension",
                _assemblyPath,
                "RincoNhan.Tools.Auto_Dim_Grid.AutoDimGridCommand"
            );
            btnData.ToolTip = "Auto dimension grids, walls and doors.";
            
            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("AutoDimGrid.png") ?? LoadIcon("AlignTags.png");
            pb.Image = LoadIcon("AutoDimGrid.png", 16) ?? LoadIcon("AlignTags.png", 16);
        }

        private static PushButtonData GetReloadCADButtonData()
        {
            PushButtonData btnData = new PushButtonData(
                "cmdReloadCADLinks",
                "\u2800\u2800",
                _assemblyPath,
                "RincoNhan.Tools.ReloadCADLinks.ReloadCADCommand"
            );
            btnData.ToolTip = "Reload CAD Links\nBatch reload CAD links with path overriding.";

            btnData.LargeImage = LoadIcon("ReloadCAD.png");
            btnData.Image = LoadIcon("ReloadCAD.png", 16);
            
            return btnData;
        }

        private static void AddJoinElementsButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdJoinElements",
                "Join\nElements",
                _assemblyPath,
                "RincoNhan.Tools.JoinElements.Command"
            );
            btnData.ToolTip = "Batch Join, Unjoin, or Switch geometry of elements.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("JoinElements.png");
            pb.Image = LoadIcon("JoinElements.png", 16);
        }

        private static void AddElementsTagsButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdElementsTags",
                "Elements\nTags",
                _assemblyPath,
                "RincoNhan.Tools.ElementsTags.Command"
            );
            btnData.ToolTip = "Batch tag elements and fix misplaced tags.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ElementsTags.png");
            pb.Image = LoadIcon("ElementsTags.png", 16);
        }

        private static void AddAlignTagsButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdAlignTags",
                "Align\nTags",
                _assemblyPath,
                "RincoNhan.Tools.AlignTags.AlignTagsCommand"
            );
            btnData.ToolTip = "Align heads of multiple tags based on a reference tag coordinate.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("AlignTags.png");
            pb.Image = LoadIcon("AlignTags.png", 16);
        }

        private static BitmapImage LoadIcon(string iconName, int size = 32)
        {
            string iconPath = Path.Combine(ResourcesPath, iconName);
            if (!File.Exists(iconPath)) return null;

            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(iconPath, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (size > 0)
                {
                    image.DecodePixelWidth = size;
                    image.DecodePixelHeight = size;
                }
                image.EndInit();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static void AddImportEXtoLegendButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdImportEXtoLegend",
                "Import\nExcel",
                _assemblyPath,
                "RincoNhan.Tools.ImportEXtoLegend.Command"
            );
            btnData.ToolTip = "Import and update Excel tables in Legend views.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ImportExcel.png");
            pb.Image = LoadIcon("ImportExcel.png", 16);
        }

        private static void AddWallDivideButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdWallDivide",
                "Wall\nDivide",
                _assemblyPath,
                "RincoNhan.Tools.WallDivide.Command"
            );
            btnData.ToolTip = "Divide wall panels based on weight and dimension constraints.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("WallDivide.png");
            pb.Image = LoadIcon("WallDivide.png", 16);
        }

        private static void AddElevationViewButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdElevationView",
                "Elevation\nView",
                _assemblyPath,
                "RincoNhan.Tools.ElevationView.Command"
            );
            btnData.ToolTip = "Optimize elevation view crops and level lines.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ElevationView.png");
            pb.Image = LoadIcon("ElevationView.png", 16);
        }

        private static void AddCreateSectionWallButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdCreateSectionWall",
                "Section\nWall",
                _assemblyPath,
                "RincoNhan.Tools.CreateSectionWall.Command"
            );
            btnData.ToolTip = "Generate section views parallel to selected walls.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("SectionWall.png");
            pb.Image = LoadIcon("SectionWall.png", 16);
        }

        private static void AddViewRefButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdViewRef",
                "View\nRef",
                _assemblyPath,
                "RincoNhan.Tools.ViewRef.Command"
            );
            btnData.ToolTip = "Place View Reference tags on walls.";

            // Add icon if it exists, otherwise it will just show text
            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ViewRef.png");
            pb.Image = LoadIcon("ViewRef.png", 16);
        }

        private static void AddRebarColumnButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdRebarColumn",
                "Rebar\nColumn",
                _assemblyPath,
                "RincoNhan.Tools.RebarColumn.Command"
            );
            btnData.ToolTip = "Generate column reinforcement with dynamic preview.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("RebarColumn.png");
            pb.Image = LoadIcon("RebarColumn.png", 16);
        }

        private static void AddStairDetailButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdStairDetail",
                "Stair\nDetail",
                _assemblyPath,
                "RincoNhan.Tools.StairDetail.StairDetail"
            );
            btnData.ToolTip = "Automatically place rebar, dimensions, and tags for stair sections.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("StairDetail.png") ?? LoadIcon("RebarColumn.png");
            pb.Image = LoadIcon("StairDetail.png", 16) ?? LoadIcon("RebarColumn.png", 16);
        }

        private static void AddCreateLevelButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdCreateLevel",
                "Create\nLevel",
                _assemblyPath,
                "RincoNhan.Tools.CreateLevel.Command"
            );
            btnData.ToolTip = "Create Revit levels from an Excel file with floor-to-floor heights.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("CreateLevel.png");
            pb.Image = LoadIcon("CreateLevel.png", 16);
        }

        private static void AddCreateViewSheetButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdCreateViewSheet",
                "Create View\n& Sheet",
                _assemblyPath,
                "RincoNhan.Tools.CreateViewSheet.Command"
            );
            btnData.ToolTip = "Create Views & Sheets.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("CreateViewSheet.png") ?? LoadIcon("CreateLevel.png");
            pb.Image = LoadIcon("CreateViewSheet.png", 16) ?? LoadIcon("CreateLevel.png", 16);
        }

        private static void AddAutoViewSheetButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdAutoViewSheet",
                "Auto View\n& Sheet",
                _assemblyPath,
                "RincoNhan.Tools.AutoViewSheet.AutoViewSheetCommand"
            );
            btnData.ToolTip = "Automatically generate views, sheets, apply scopes, and align views in one click.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            // Trying to use a generic icon like CreateLevel.png if specific one doesn't exist
            pb.LargeImage = LoadIcon("CreateLevel.png");
            pb.Image = LoadIcon("CreateLevel.png", 16);
        }

        private static PushButtonData GetSmartLinkCadButtonData()
        {
            PushButtonData btnData = new PushButtonData(
                "cmdSmartLinkCad",
                "\u2800\u2800\u2800",
                _assemblyPath,
                "RincoNhan.Tools.SmartLinkCad.SmartLinkCadCommand"
            );
            btnData.ToolTip = "Smart Link Cad\nBatch override graphics of CAD files and layers.";

            btnData.LargeImage = LoadIcon("Smart Link Cad.png");
            btnData.Image = LoadIcon("Smart Link Cad.png", 16);
            
            return btnData;
        }

        private static PushButtonData GetSmartLinkRevitButtonData()
        {
            PushButtonData btnData = new PushButtonData(
                "cmdSmartLinkRevit",
                "\u2800\u2800\u2800\u2800",
                _assemblyPath,
                "RincoNhan.Tools.SmartLinkRevit.SmartLinkRevitCommand"
            );
            btnData.ToolTip = "Smart Link RVT\nBatch apply RVT Link Display Settings across multiple host views.";

            btnData.LargeImage = LoadIcon("Smart Link Revit.png");
            btnData.Image = LoadIcon("Smart Link Revit.png", 16);
            
            return btnData;
        }

        private static void AddQueryElementButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdQueryElement",
                "Query\nElement",
                _assemblyPath,
                "RincoNhan.Tools.QueryElement.Command"
            );
            btnData.ToolTip = "Query the location of Views, Legends, and Groups.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("Query Element.png");
            pb.Image = LoadIcon("Query Element.png", 16);
        }

        private static void AddMtoSmartTagButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdMtoSmartTag",
                "MTO\nSmart Tag",
                _assemblyPath,
                "RincoNhan.Tools.MtoSmartTag.Command"
            );
            btnData.ToolTip = "Tag Reinforcement Distribution detail items near their insertion point.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("MtoSmartTag.png");
            pb.Image = LoadIcon("MtoSmartTag.png", 16);
        }

        private static void AddMtoQueryButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdMtoQuery",
                "MTO\nQuery",
                _assemblyPath,
                "RincoNhan.Tools.MTOQuery.Command"
            );
            btnData.ToolTip = "Query and summarize Reinforcement Distribution and ZBar detail items in the current view.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("MtoQuery.png");
            pb.Image = LoadIcon("MtoQuery.png", 16);
        }

        private static void AddMtoGroupBarButton(RibbonPanel panel)
        {
            SplitButtonData sbData = new SplitButtonData("cmdMtoGroupBarSplit", "Group\nBar");
            SplitButton sb = panel.AddItem(sbData) as SplitButton;

            // Group Bar
            PushButtonData btnGroupBar = new PushButtonData(
                "cmdMtoGroupBar",
                "Group\nBar",
                _assemblyPath,
                "RincoNhan.Tools.MtoGroupBar.Command"
            );
            btnGroupBar.ToolTip = "Group lapped reinforcement bars automatically.";
            PushButton pbGroupBar = sb.AddPushButton(btnGroupBar);
            pbGroupBar.LargeImage = LoadIcon("MtoGroupBar.png");
            pbGroupBar.Image = LoadIcon("MtoGroupBar.png", 16);

            // Align Dist
            PushButtonData btnAlignDist = new PushButtonData(
                "cmdAlignDistribution",
                "Align\nDistribution",
                _assemblyPath,
                "RincoNhan.Tools.MtoGroupBar.AlignDistributionCommand"
            );
            btnAlignDist.ToolTip = "Align selected distribution symbols to a main rebar or lap sign.";
            PushButton pbAlignDist = sb.AddPushButton(btnAlignDist);
            pbAlignDist.LargeImage = LoadIcon("AlignTags.png") ?? LoadIcon("MtoGroupBar.png");
            pbAlignDist.Image = LoadIcon("AlignTags.png", 16) ?? LoadIcon("MtoGroupBar.png", 16);
        }

        private static void AddFamilyDataSplitButton(RibbonPanel panel)
        {
            SplitButtonData sbData = new SplitButtonData("cmdFamilyDataSplit", "Family\nData");
            SplitButton sb = panel.AddItem(sbData) as SplitButton;

            // Export Family JSON
            PushButtonData btnExportFamily = new PushButtonData(
                "cmdExportFamilyData",
                "Export\nFamily JSON",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ExportFamilyDataCommand"
            );
            btnExportFamily.ToolTip = "Export Family's Lines, Ref Planes, and Dimensions to JSON.";
            PushButton pbExportFamily = sb.AddPushButton(btnExportFamily);
            pbExportFamily.LargeImage = LoadIcon("ExportFamilyJSON.png");
            pbExportFamily.Image = LoadIcon("ExportFamilyJSON.png", 16);

            // Import Family JSON
            PushButtonData btnImportFamily = new PushButtonData(
                "cmdImportFamilyData",
                "Import\nFamily JSON",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ImportFamilyDataCommand"
            );
            btnImportFamily.ToolTip = "Import Lines and Ref Planes from JSON into current Family.";
            PushButton pbImportFamily = sb.AddPushButton(btnImportFamily);
            pbImportFamily.LargeImage = LoadIcon("ImportFamilyJSON.png");
            pbImportFamily.Image = LoadIcon("ImportFamilyJSON.png", 16);

            // Export Type Data
            PushButtonData btnExportType = new PushButtonData(
                "cmdExportFamilyTypeData",
                "Export\nType Data",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ExportFamilyTypeDataCommand"
            );
            btnExportType.ToolTip = "Export Family Types and Parameters to JSON (run in Project env).";
            PushButton pbExportType = sb.AddPushButton(btnExportType);
            pbExportType.LargeImage = LoadIcon("ExportTypeData.png");
            pbExportType.Image = LoadIcon("ExportTypeData.png", 16);

            // Import Type Data
            PushButtonData btnImportType = new PushButtonData(
                "cmdImportFamilyTypeData",
                "Import\nType Data",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ImportFamilyTypeDataCommand"
            );
            btnImportType.ToolTip = "Import Family Types and Parameters from JSON (run in Family env).";
            PushButton pbImportType = sb.AddPushButton(btnImportType);
            pbImportType.LargeImage = LoadIcon("ImportTypeData.png");
            pbImportType.Image = LoadIcon("ImportTypeData.png", 16);

            // Export Shared Param
            PushButtonData btnExportSharedParam = new PushButtonData(
                "cmdExportSharedParam",
                "Export\nShared Param",
                _assemblyPath,
                "RincoNhan.Tools.ExportSharedParameters.ExportSharedParamCommand"
            );
            btnExportSharedParam.ToolTip = "Export all Shared Parameters in the document to a TXT or Excel file.";
            PushButton pbExportSharedParam = sb.AddPushButton(btnExportSharedParam);
            pbExportSharedParam.LargeImage = LoadIcon("ExportSharedParam.png");
            pbExportSharedParam.Image = LoadIcon("ExportSharedParam.png", 16);
        }

        private static void AddAddSharedParamButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdAddSharedParam",
                "Add\nShared Param",
                _assemblyPath,
                "RincoNhan.Tools.AddSharedParameters.AddSharedParamCommand"
            );
            btnData.ToolTip = "Batch add Shared Parameters to the current Family.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("AddSharedParam.png");
            pb.Image = LoadIcon("AddSharedParam.png", 16);
        }

        // Removed individual Family buttons

        private static void AddLoadingHatchButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdLoadingHatch",
                "Loading\nHatch",
                _assemblyPath,
                "RincoNhan.Tools.LoadingHatch.Command"
            );
            btnData.ToolTip = "Query and summarize Filled Regions in the current view.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("LoadingHatch.png");
            pb.Image = LoadIcon("LoadingHatch.png", 16);
        }

        private static void AddExportExcelButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdExportExcel",
                "Export\nSchedules",
                _assemblyPath,
                "RincoNhan.Tools.ExportExcel.Command"
            );
            btnData.ToolTip = "Export all schedules to an Excel file.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("Export Excel.png");
            pb.Image = LoadIcon("Export Excel.png", 16);
        }
        private static void AddInterlockingWallButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdInterlockingWall",
                "Interlocking\nWall",
                _assemblyPath,
                "RincoNhan.Tools.InterlockingWall.Command"
            );
            btnData.ToolTip = "Join two walls or split a wall panel at a specific position.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("InterlockingWall.png");
            pb.Image = LoadIcon("InterlockingWall.png", 16);
        }

        private static void AddLoadingScheduleButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdLoadingSchedule",
                "Loading\nSchedule",
                _assemblyPath,
                "RincoNhan.Tools.LoadingSchedule.Command"
            );
            btnData.ToolTip = "Generate Loading Schedule legend table in a Legend View.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("Loading Schedule.png");
            pb.Image = LoadIcon("Loading Schedule.png", 16);
        }
        private static void AddConnectionButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdSteelConnection",
                "Steel\nConnection",
                _assemblyPath,
                "RincoNhan.Tools.Connection.Command"
            );
            btnData.ToolTip = "Create Steel Connections (Beam to Beam).";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("SteelConnection.png");
            pb.Image = LoadIcon("SteelConnection.png", 16);
        }

        private static void AddClashDetectionButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdClashDetection",
                "Clash\nDetection",
                _assemblyPath,
                "RincoNhan.Tools.ClashDetection.Command"
            );
            btnData.ToolTip = "Detect clashes between elements in the model.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ClashDetection.png");
            pb.Image = LoadIcon("ClashDetection.png", 16);
        }

        private static PushButtonData GetOverrideCadButtonData()
        {
            PushButtonData btnData = new PushButtonData(
                "cmdOverrideCad",
                "\u2800",
                _assemblyPath,
                "RincoNhan.Tools.OverrideCad.Command"
            );
            btnData.ToolTip = "Override CAD\nOverride CAD link display settings.";

            btnData.LargeImage = LoadIcon("OverrideCad.png");
            btnData.Image = LoadIcon("OverrideCad.png", 16);
            
            return btnData;
        }
        private static void AddCheckFoldButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdCheckFold",
                "Check\nFold",
                Assembly.GetExecutingAssembly().Location,
                "RincoModeling.Tools.CheckFold.Command"
            );
            btnData.ToolTip = "Check fold slab thickness and 2D step annotations.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("CheckFold.png");
            pb.Image = LoadIcon("CheckFold.png", 16);
        }
        private static void AddConvertHatchSplitButton(RibbonPanel panel)
        {
            SplitButtonData sbData = new SplitButtonData("cmdConvertHatchSplit", "Convert\nHatch");
            SplitButton sb = panel.AddItem(sbData) as SplitButton;

            // Export Hatch
            PushButtonData btnExportHatch = new PushButtonData(
                "cmdExportHatch",
                "Export\nHatch",
                _assemblyPath,
                "RincoNhan.Tools.ConvertHatch.ExportHatchCommand"
            );
            btnExportHatch.ToolTip = "Export Filled Regions to JSON.";
            PushButton pbExportHatch = sb.AddPushButton(btnExportHatch);
            pbExportHatch.LargeImage = LoadIcon("ExportHatch.png") ?? LoadIcon("LoadingHatch.png");
            pbExportHatch.Image = LoadIcon("ExportHatch.png", 16) ?? LoadIcon("LoadingHatch.png", 16);

            // Import Hatch
            PushButtonData btnImportHatch = new PushButtonData(
                "cmdImportHatch",
                "Import\nHatch",
                _assemblyPath,
                "RincoNhan.Tools.ConvertHatch.ImportHatchCommand"
            );
            btnImportHatch.ToolTip = "Import Filled Regions from JSON.";
            PushButton pbImportHatch = sb.AddPushButton(btnImportHatch);
            pbImportHatch.LargeImage = LoadIcon("ImportHatch.png") ?? LoadIcon("LoadingHatch.png");
            pbImportHatch.Image = LoadIcon("ImportHatch.png", 16) ?? LoadIcon("LoadingHatch.png", 16);
        }

        private static void AddConvertHatchButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdConvertHatch",
                "Convert\nHatch",
                _assemblyPath,
                "RincoNhan.Tools.ConvertHatch.ConvertHatchCommand"
            );
            btnData.ToolTip = "Convert Hatch Tool.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ConvertHatch.png") ?? LoadIcon("ExportHatch.png");
            pb.Image = LoadIcon("ConvertHatch.png", 16) ?? LoadIcon("ExportHatch.png", 16);
        }

        private static void AddDuplicateSheetSplitButton(RibbonPanel panel)
        {
            SplitButtonData sbData = new SplitButtonData("cmdDuplicateSheetSplit", "Duplicate\nSheet");
            SplitButton sb = panel.AddItem(sbData) as SplitButton;

            // Empty Sheet
            PushButtonData btnEmpty = new PushButtonData(
                "cmdDuplicateEmptySheet",
                "Duplicate Empty Sheet",
                _assemblyPath,
                "RincoNhan.Tools.DuplicateSheet.DuplicateEmptySheetCommand"
            );
            btnEmpty.ToolTip = "Duplicate selected sheets without their views.";
            PushButton pbEmpty = sb.AddPushButton(btnEmpty);
            pbEmpty.LargeImage = LoadIcon("Duplicate Sheet.png") ?? LoadIcon("CreateViewSheet.png");
            pbEmpty.Image = LoadIcon("Duplicate Sheet.png", 16) ?? LoadIcon("CreateViewSheet.png", 16);

            // With Detailing
            PushButtonData btnWithDetailing = new PushButtonData(
                "cmdDuplicateWithDetailing",
                "Duplicate with Sheet Detailing",
                _assemblyPath,
                "RincoNhan.Tools.DuplicateSheet.DuplicateWithDetailingCommand"
            );
            btnWithDetailing.ToolTip = "Duplicate selected sheets along with their views.";
            PushButton pbWithDetailing = sb.AddPushButton(btnWithDetailing);
            pbWithDetailing.LargeImage = LoadIcon("Duplicate Sheet.png") ?? LoadIcon("CreateViewSheet.png");
            pbWithDetailing.Image = LoadIcon("Duplicate Sheet.png", 16) ?? LoadIcon("CreateViewSheet.png", 16);
        }
    }
}
