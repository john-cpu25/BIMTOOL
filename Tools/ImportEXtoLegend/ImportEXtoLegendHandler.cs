using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ImportEXtoLegend.Models;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    public class ImportEXtoLegendHandler : IExternalEventHandler
    {
        public bool IsReload { get; set; }
        public string ExcelFilePath { get; set; }
        public string WorksheetName { get; set; }
        public ElementId ChosenTitleblockId { get; set; }
        public bool IsSplitEnabled { get; set; }
        public ElementId TargetLegendId { get; set; }
        public ElementId TargetTextTypeId { get; set; }
        public double RowSpacingMultiplier { get; set; } = 1.0;
        public ExcelTableData TableData { get; set; }

        public Action<string> NotifyStatus { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            View view = doc.ActiveView;

            using (Transaction trans = new Transaction(doc, "Import Excel to Legend"))
            {
                trans.Start();
                try
                {
                    double maxHeight = 0;
                    if (IsSplitEnabled && ChosenTitleblockId != null)
                    {
                        var titleblock = doc.GetElement(ChosenTitleblockId) as FamilySymbol;
                        if (titleblock != null)
                        {
                            Parameter hParam = titleblock.get_Parameter(BuiltInParameter.SHEET_HEIGHT);
                            if (hParam != null) maxHeight = hParam.AsDouble() - 0.1;
                            else maxHeight = 2.0;
                        }
                    }

                    View targetView = null;

                    if (IsReload)
                    {
                        // If reloading, it MUST be an active Legend view that has data
                        if (view.ViewType != ViewType.Legend)
                        {
                            NotifyStatus?.Invoke("Please open the imported Legend view to reload it.");
                            trans.RollBack();
                            return;
                        }
                        
                        targetView = view;
                        var (savedPath, savedWs, savedIds) = StorageSchema.ReadData(doc, targetView.Id);
                        if (string.IsNullOrEmpty(savedPath))
                        {
                            NotifyStatus?.Invoke("No existing imported table found in this view to reload.");
                            trans.RollBack();
                            return;
                        }

                        ExcelFilePath = savedPath;
                        WorksheetName = savedWs;

                        // Delete old elements
                        foreach (var id in savedIds)
                        {
                            try { doc.Delete(id); } catch { }
                        }
                    }
                    else
                    {
                        // ADD TO LEGEND: Use user-selected Legend
                        if (TargetLegendId == null)
                        {
                            NotifyStatus?.Invoke("No target Legend View provided.");
                            trans.RollBack();
                            return;
                        }

                        targetView = doc.GetElement(TargetLegendId) as View;
                        if (targetView == null || targetView.ViewType != ViewType.Legend)
                        {
                            NotifyStatus?.Invoke("Invalid target Legend View.");
                            trans.RollBack();
                            return;
                        }

                        // Existing view found. Check if it has an old import and clear it.
                        var (savedPath, savedWs, savedIds) = StorageSchema.ReadData(doc, targetView.Id);
                        if (!string.IsNullOrEmpty(savedPath))
                        {
                            foreach (var id in savedIds) { try { doc.Delete(id); } catch { } }
                        }
                    }

                    // Get the data to render
                    ExcelTableData dataToRender = TableData;
                    if (dataToRender == null)
                    {
                        dataToRender = ExcelReader.ReadTable(ExcelFilePath, WorksheetName);
                    }

                    if (dataToRender == null || dataToRender.Cells.Count == 0 || dataToRender.MaxRow == 0)
                    {
                        NotifyStatus?.Invoke("Could not read any data from " + WorksheetName);
                        trans.RollBack();
                        return;
                    }

                    XYZ origin = XYZ.Zero;

                    // Render onto the targetView
                    List<ElementId> createdElements = LegendRenderer.RenderTable(doc, targetView, dataToRender, origin, maxHeight, TargetTextTypeId, RowSpacingMultiplier);

                    // Save data for reload
                    StorageSchema.SaveData(doc, targetView.Id, ExcelFilePath, WorksheetName, createdElements);

                    trans.Commit();
                    
                    // Activate the view if we created/updated it and it's not currently active (cannot do inside transaction)
                    if (!IsReload && targetView.Id != doc.ActiveView.Id)
                    {
                        app.ActiveUIDocument.ActiveView = targetView;
                    }

                    NotifyStatus?.Invoke("Success: Table imported/reloaded successfully.");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    NotifyStatus?.Invoke("Error: " + ex.Message);
                }
            }
        }

        public string GetName() => "ImportEXtoLegend";
    }
}
