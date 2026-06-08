using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingSchedule.Models;

namespace RincoNhan.Tools.LoadingSchedule
{
    /// <summary>
    /// External event handler that executes the legend rendering inside a Revit transaction.
    /// Supports creating a new Legend view (by duplicating an existing one) or using an existing one.
    /// </summary>
    public class LoadingScheduleHandler : IExternalEventHandler
    {
        public bool CreateNewLegend { get; set; }
        public string NewLegendName { get; set; }
        public ElementId TargetLegendId { get; set; }
        public ElementId TargetTextTypeId { get; set; }
        public List<LoadingScheduleItem> Items { get; set; }
        public Action<string> NotifyStatus { get; set; }
        public Action<string> LogMessage { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Create Loading Schedule Legend"))
            {
                trans.Start();
                try
                {
                    View targetView = null;

                    if (CreateNewLegend)
                    {
                        LogMessage?.Invoke($"Creating new legend view: \"{NewLegendName}\"...");
                        targetView = CreateLegendByDuplicate(doc, NewLegendName);
                        if (targetView == null)
                        {
                            NotifyStatus?.Invoke("Error: Could not create new Legend view. Please create an empty Legend view manually first.");
                            trans.RollBack();
                            return;
                        }
                        LogMessage?.Invoke($"✓ Legend view created: \"{targetView.Name}\" (Id={targetView.Id})");
                    }
                    else
                    {
                        if (TargetLegendId == null || TargetLegendId == ElementId.InvalidElementId)
                        {
                            NotifyStatus?.Invoke("Error: No target Legend View selected.");
                            trans.RollBack();
                            return;
                        }

                        targetView = doc.GetElement(TargetLegendId) as View;
                        if (targetView == null || targetView.ViewType != ViewType.Legend)
                        {
                            NotifyStatus?.Invoke("Error: Invalid target Legend View.");
                            trans.RollBack();
                            return;
                        }
                        LogMessage?.Invoke($"Using existing legend: \"{targetView.Name}\"");
                    }

                    if (Items == null || Items.Count == 0)
                    {
                        NotifyStatus?.Invoke("Error: No items selected for the legend.");
                        trans.RollBack();
                        return;
                    }

                    LogMessage?.Invoke($"Items to render: {Items.Count}");

                    // Validate text type
                    if (TargetTextTypeId == null || TargetTextTypeId == ElementId.InvalidElementId)
                    {
                        var fallbackType = new FilteredElementCollector(doc)
                            .OfClass(typeof(TextNoteType))
                            .FirstElementId();

                        if (fallbackType == null || fallbackType == ElementId.InvalidElementId)
                        {
                            NotifyStatus?.Invoke("Error: No TextNote type available.");
                            trans.RollBack();
                            return;
                        }

                        TargetTextTypeId = fallbackType;
                        LogMessage?.Invoke("Using fallback TextNoteType");
                    }

                    LogMessage?.Invoke("Starting renderer...");
                    XYZ origin = XYZ.Zero;

                    List<ElementId> createdElements = LoadingScheduleLegendRenderer.RenderLegend(
                        doc, targetView, Items, origin, TargetTextTypeId, LogMessage);

                    LogMessage?.Invoke($"Renderer done. Committing transaction ({createdElements.Count} elements)...");
                    trans.Commit();
                    LogMessage?.Invoke("✓ Transaction committed");

                    // Activate the target view
                    try
                    {
                        app.ActiveUIDocument.ActiveView = targetView;
                        LogMessage?.Invoke($"✓ Activated view: \"{targetView.Name}\"");
                    }
                    catch { }

                    string viewName = targetView.Name;
                    NotifyStatus?.Invoke($"Success: Legend \"{viewName}\" created with {Items.Count} rows. ({createdElements.Count} elements)");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"✗ ERROR: {ex.Message}");
                    trans.RollBack();
                    NotifyStatus?.Invoke("Error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates a new Legend view by duplicating an existing one and clearing its content.
        /// The Revit API does not have a direct View.CreateLegend() method,
        /// so we must duplicate an existing Legend view.
        /// </summary>
        private View CreateLegendByDuplicate(Document doc, string name)
        {
            // Find all legend views
            var allLegends = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .ToList();

            if (!allLegends.Any())
                return null;

            // Pick the legend with the FEWEST elements (fastest to duplicate & clean)
            View bestSource = null;
            int minCount = int.MaxValue;

            foreach (var legend in allLegends)
            {
                int count = new FilteredElementCollector(doc, legend.Id)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                if (count < minCount)
                {
                    minCount = count;
                    bestSource = legend;
                    if (count == 0) break; // Perfect — empty legend, no cleanup needed
                }
            }

            // Duplicate the lightest legend view
            ElementId newViewId = bestSource.Duplicate(ViewDuplicateOption.Duplicate);
            if (newViewId == null || newViewId == ElementId.InvalidElementId)
                return null;

            View newView = doc.GetElement(newViewId) as View;
            if (newView == null)
                return null;

            // Set the name, handling duplicates
            string finalName = name;
            int suffix = 1;
            while (true)
            {
                try
                {
                    newView.Name = finalName;
                    break;
                }
                catch
                {
                    suffix++;
                    finalName = $"{name} ({suffix})";
                    if (suffix > 100) break;
                }
            }

            // Only clear if the source had elements (skip if already empty)
            if (minCount > 0)
            {
                try
                {
                    var elementsInView = new FilteredElementCollector(doc, newView.Id)
                        .WhereElementIsNotElementType()
                        .ToElementIds();

                    if (elementsInView.Count > 0)
                        doc.Delete(elementsInView);
                }
                catch { }
            }

            return newView;
        }

        public string GetName() => "LoadingScheduleHandler";
    }
}
