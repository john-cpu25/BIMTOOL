using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.LoadingSchedule
{
    public class DuplicateLegendHandler : IExternalEventHandler
    {
        public ElementId SourceLegendId { get; set; }
        public List<ElementId> TargetSheetIds { get; set; }
        
        public Action<int> ProgressChanged { get; set; }
        public Action<string> LogMessage { get; set; }
        public Action<string> Completed { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Duplicate Legend to Sheets"))
            {
                trans.Start();
                try
                {
                    View sourceLegend = doc.GetElement(SourceLegendId) as View;
                    if (sourceLegend == null || sourceLegend.ViewType != ViewType.Legend)
                    {
                        Completed?.Invoke("Error: Source Legend is invalid.");
                        trans.RollBack();
                        return;
                    }

                    // Find existing Viewports for this legend to get position
                    var allViewports = new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .Where(vp => vp.ViewId == sourceLegend.Id)
                        .ToList();

                    XYZ boxCenter = null;
                    if (allViewports.Any())
                    {
                        boxCenter = allViewports.First().GetBoxCenter();
                        LogMessage?.Invoke($"Found source legend viewport at {boxCenter}. Will use this position.");
                    }
                    else
                    {
                        LogMessage?.Invoke("Warning: Source legend is not placed on any sheet. Will place at default center.");
                    }

                    int total = TargetSheetIds.Count;
                    int current = 0;

                    foreach (var sheetId in TargetSheetIds)
                    {
                        current++;
                        ViewSheet sheet = doc.GetElement(sheetId) as ViewSheet;
                        if (sheet == null) continue;

                        LogMessage?.Invoke($"Processing sheet: {sheet.SheetNumber} - {sheet.Name}");
                        
                        // Duplicate the legend
                        ElementId newLegendId = sourceLegend.Duplicate(ViewDuplicateOption.WithDetailing);
                        View newLegend = doc.GetElement(newLegendId) as View;

                        // Extract the level name from sheet name (assuming format "PREFIX - LEVEL NAME")
                        string levelName = sheet.Name;
                        if (levelName.Contains("-"))
                        {
                            levelName = levelName.Substring(levelName.IndexOf("-") + 1).Trim();
                        }

                        // Rename the newly duplicated legend
                        string finalName = $"{sourceLegend.Name} - {levelName}";
                        int suffix = 1;
                        while (true)
                        {
                            try
                            {
                                newLegend.Name = finalName;
                                break;
                            }
                            catch
                            {
                                suffix++;
                                finalName = $"{sourceLegend.Name} - {sheet.Name} ({suffix})";
                                if (suffix > 100) break;
                            }
                        }

                        // Determine placement position
                        XYZ placePosition = boxCenter;

                        // If target sheet already has a legend, use its position and delete it
                        var sheetViewports = new FilteredElementCollector(doc, sheet.Id)
                            .OfClass(typeof(Viewport))
                            .Cast<Viewport>()
                            .ToList();
                        
                        var existingLegendVp = sheetViewports.FirstOrDefault(vp => 
                        {
                            View v = doc.GetElement(vp.ViewId) as View;
                            return v != null && v.ViewType == ViewType.Legend;
                        });

                        if (existingLegendVp != null && placePosition == null)
                        {
                            placePosition = existingLegendVp.GetBoxCenter();
                            LogMessage?.Invoke($"Found placeholder legend on target sheet. Using its position.");
                            doc.Delete(existingLegendVp.Id);
                        }
                        else if (existingLegendVp != null)
                        {
                            // if we have source position but there is already a legend on target sheet
                            LogMessage?.Invoke($"Removing existing legend from target sheet.");
                            doc.Delete(existingLegendVp.Id);
                        }

                        if (placePosition == null)
                        {
                            placePosition = XYZ.Zero; // Absolute fallback
                        }

                        // Create Viewport
                        if (Viewport.CanAddViewToSheet(doc, sheet.Id, newLegend.Id))
                        {
                            Viewport.Create(doc, sheet.Id, newLegend.Id, placePosition);
                            LogMessage?.Invoke($"✓ Placed {newLegend.Name} on {sheet.SheetNumber}");
                        }
                        else
                        {
                            LogMessage?.Invoke($"✗ Cannot place legend on {sheet.SheetNumber}");
                        }

                        int percentage = (int)((current / (double)total) * 100);
                        ProgressChanged?.Invoke(percentage);
                    }

                    trans.Commit();
                    Completed?.Invoke($"Success: Duplicated legend to {total} sheet(s).");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"✗ ERROR: {ex.Message}");
                    if (trans.HasStarted()) trans.RollBack();
                    Completed?.Invoke("Error: " + ex.Message);
                }
            }
        }

        public string GetName() => "DuplicateLegendHandler";
    }
}
