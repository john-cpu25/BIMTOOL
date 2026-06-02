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
        private static string _resourcesPath = Path.Combine(Directory.GetParent(_assemblyDir).FullName, "Resources");

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
            // RibbonPanel modelingPanel = GetOrCreatePanel(application, tabName, "Modeling");

            // Add Buttons to General Panel
            AddFilterButton(generalPanel);
            AddReloadCADButton(generalPanel);
            AddJoinElementsButton(generalPanel);
            AddElementsTagsButton(generalPanel);
            AddAlignTagsButton(generalPanel);
            AddImportEXtoLegendButton(generalPanel);
            AddWallDivideButton(generalPanel);
            AddElevationViewButton(generalPanel);
            AddCreateSectionWallButton(generalPanel);
            AddViewRefButton(generalPanel);
            AddRebarColumnButton(generalPanel);
            AddCreateLevelButton(generalPanel);
            AddSmartLinkCadButton(generalPanel);
            AddSmartLinkRevitButton(generalPanel);
            AddQueryElementButton(generalPanel);
            AddMtoQueryButton(generalPanel);
            AddMtoGroupBarButton(generalPanel);
            AddMtoSmartTagButton(generalPanel);
            AddExportSharedParamButton(generalPanel);
            AddAddSharedParamButton(generalPanel);
            AddExportFamilyDataButton(generalPanel);
            AddImportFamilyDataButton(generalPanel);
            AddExportFamilyTypeDataButton(generalPanel);
            AddImportFamilyTypeDataButton(generalPanel);
            AddExportExcelButton(generalPanel);
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

        private static void AddReloadCADButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdReloadCADLinks",
                "Reload\nCAD Links",
                _assemblyPath,
                "RincoNhan.Tools.ReloadCADLinks.ReloadCADCommand"
            );
            btnData.ToolTip = "Batch reload CAD links with path overriding.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
            pb.LargeImage = LoadIcon("ReloadCAD.png");
            pb.Image = LoadIcon("ReloadCAD.png", 16);
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
            string iconPath = Path.Combine(_resourcesPath, iconName);
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

        private static void AddSmartLinkCadButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdSmartLinkCad",
                "Smart\nLink Cad",
                _assemblyPath,
                "RincoNhan.Tools.SmartLinkCad.SmartLinkCadCommand"
            );
            btnData.ToolTip = "Batch override graphics of CAD files and layers.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
        }

        private static void AddSmartLinkRevitButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdSmartLinkRevit",
                "Smart\nLink RVT",
                _assemblyPath,
                "RincoNhan.Tools.SmartLinkRevit.SmartLinkRevitCommand"
            );
            btnData.ToolTip = "Batch apply RVT Link Display Settings across multiple host views.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
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
        }

        private static void AddMtoGroupBarButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdMtoGroupBar",
                "MTO\nGroup Bar",
                _assemblyPath,
                "RincoNhan.Tools.MtoGroupBar.Command"
            );
            btnData.ToolTip = "Group lapped reinforcement bars automatically.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
        }

        private static void AddExportSharedParamButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdExportSharedParam",
                "Export\nShared Param",
                _assemblyPath,
                "RincoNhan.Tools.ExportSharedParameters.ExportSharedParamCommand"
            );
            btnData.ToolTip = "Export all Shared Parameters in the document to a TXT or Excel file.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
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
        }

        private static void AddExportFamilyDataButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdExportFamilyData",
                "Export\nFamily JSON",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ExportFamilyDataCommand"
            );
            btnData.ToolTip = "Export Family's Lines, Ref Planes, and Dimensions to JSON.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
        }

        private static void AddImportFamilyDataButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdImportFamilyData",
                "Import\nFamily JSON",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ImportFamilyDataCommand"
            );
            btnData.ToolTip = "Import Lines and Ref Planes from JSON into current Family.";

            PushButton pb = panel.AddItem(btnData) as PushButton;
        }

        private static void AddExportFamilyTypeDataButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdExportFamilyTypeData",
                "Export\nType Data",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ExportFamilyTypeDataCommand"
            );
            btnData.ToolTip = "Export Family Types and Parameters to JSON (run in Project env).";

            PushButton pb = panel.AddItem(btnData) as PushButton;
        }

        private static void AddImportFamilyTypeDataButton(RibbonPanel panel)
        {
            PushButtonData btnData = new PushButtonData(
                "cmdImportFamilyTypeData",
                "Import\nType Data",
                _assemblyPath,
                "RincoNhan.Tools.ExportFamilyData.ImportFamilyTypeDataCommand"
            );
            btnData.ToolTip = "Import Family Types and Parameters from JSON (run in Family env).";

            PushButton pb = panel.AddItem(btnData) as PushButton;
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
            pb.LargeImage = LoadIcon("ExportExcel.png");
            pb.Image = LoadIcon("ExportExcel.png", 16);
        }
    }
}
