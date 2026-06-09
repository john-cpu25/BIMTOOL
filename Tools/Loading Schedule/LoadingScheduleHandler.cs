using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingSchedule.Models;
using RincoNhan.Tools.LoadingSchedule.Services;

namespace RincoNhan.Tools.LoadingSchedule
{
    /// <summary>
    /// External event handler that clones a template legend's row structure
    /// into a target legend view, one row per hatch type from the current view.
    ///
    /// Flow:
    /// 1. Parse template legend → header + data row elements
    /// 2. Create/select target legend view
    /// 3. Copy header elements to target
    /// 4. For each hatch type: copy data row with Y-offset, swap FilledRegionType
    /// 5. Redraw vertical lines spanning full table height
    /// </summary>
    public class LoadingScheduleHandler : IExternalEventHandler
    {
        // ── Properties set by the UI before Raise() ──────────────────────
        public bool CreateNewLegend { get; set; }
        public string NewLegendName { get; set; }
        public ElementId TargetLegendId { get; set; }
        public ElementId TemplateLegendId { get; set; }
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
                    // ── 1. Get template view and parse it ────────────────
                    View templateView = doc.GetElement(TemplateLegendId) as View;
                    if (templateView == null || templateView.ViewType != ViewType.Legend)
                    {
                        NotifyStatus?.Invoke("Error: Invalid template Legend view.");
                        trans.RollBack();
                        return;
                    }

                    LogMessage?.Invoke($"Parsing template: \"{templateView.Name}\"...");
                    var parser = new LegendTemplateParser();
                    if (!parser.Parse(doc, templateView, LogMessage))
                    {
                        NotifyStatus?.Invoke("Error: Failed to parse template. " + parser.Summary);
                        trans.RollBack();
                        return;
                    }

                    // ── 2. Get or create target view ─────────────────────
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

                        // Clear existing elements in target
                        ClearViewElements(doc, targetView);
                    }

                    if (Items == null || Items.Count == 0)
                    {
                        NotifyStatus?.Invoke("Error: No items selected for the legend.");
                        trans.RollBack();
                        return;
                    }

                    LogMessage?.Invoke($"Items to render: {Items.Count}");

                    // ── 3. Copy header elements ──────────────────────────
                    int headerCopied = 0;
                    if (parser.HeaderElementIds.Count > 0)
                    {
                        LogMessage?.Invoke($"Copying {parser.HeaderElementIds.Count} header elements...");
                        try
                        {
                            var copiedHeaderIds = ElementTransformUtils.CopyElements(
                                templateView,
                                parser.HeaderElementIds,
                                targetView,
                                Transform.Identity,
                                new CopyPasteOptions());
                            headerCopied = copiedHeaderIds?.Count ?? 0;
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke($"  Warning: Header copy error: {ex.Message}");
                        }
                        LogMessage?.Invoke($"✓ Header copied: {headerCopied} elements");
                    }

                    // ── 4. Copy data rows ────────────────────────────────
                    int totalDataElements = 0;

                    for (int i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];
                        double offsetY = -parser.RowHeight * i;

                        LogMessage?.Invoke($"  Row {i + 1}/{Items.Count}: \"{item.TypeName}\" (offset Y={offsetY:F4})...");

                        try
                        {
                            var transform = Transform.CreateTranslation(new XYZ(0, offsetY, 0));

                            var copiedIds = ElementTransformUtils.CopyElements(
                                templateView,
                                parser.DataRowElementIds,
                                targetView,
                                transform,
                                new CopyPasteOptions());

                            if (copiedIds != null && copiedIds.Count > 0)
                            {
                                totalDataElements += copiedIds.Count;

                                // Find FilledRegion(s) among copied elements and swap type
                                int swapped = 0;
                                foreach (var copiedId in copiedIds)
                                {
                                    var elem = doc.GetElement(copiedId);
                                    if (elem is FilledRegion fr)
                                    {
                                        fr.ChangeTypeId(item.FilledRegionTypeId);
                                        swapped++;
                                    }
                                }
                                LogMessage?.Invoke($"    ✓ Copied {copiedIds.Count} elements, swapped {swapped} hatch type(s)");
                            }
                            else
                            {
                                LogMessage?.Invoke($"    ✗ Copy returned 0 elements");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage?.Invoke($"    ✗ Error: {ex.Message}");
                        }
                    }

                    // ── 5. Draw vertical lines ───────────────────────────
                    int vLinesDrawn = 0;
                    if (parser.VerticalLineXPositions.Count > 0)
                    {
                        double vLineTop = parser.TableTopY;
                        double vLineBottom = parser.HeaderBottomY - parser.RowHeight * Items.Count;

                        LogMessage?.Invoke($"Drawing {parser.VerticalLineXPositions.Count} vertical lines (Y: {vLineTop:F4} → {vLineBottom:F4})...");

                        foreach (double x in parser.VerticalLineXPositions)
                        {
                            try
                            {
                                var p1 = new XYZ(x, vLineTop, 0);
                                var p2 = new XYZ(x, vLineBottom, 0);
                                var line = doc.Create.NewDetailCurve(targetView, Line.CreateBound(p1, p2));

                                if (parser.VerticalLineStyle != null)
                                    line.LineStyle = parser.VerticalLineStyle;

                                vLinesDrawn++;
                            }
                            catch (Exception ex)
                            {
                                LogMessage?.Invoke($"  Warning: VLine at X={x:F4}: {ex.Message}");
                            }
                        }
                        LogMessage?.Invoke($"✓ Vertical lines drawn: {vLinesDrawn}");
                    }

                    // ── 6. Draw bottom border line ───────────────────────
                    try
                    {
                        double bottomY = parser.HeaderBottomY - parser.RowHeight * Items.Count;
                        var pLeft = new XYZ(parser.TableLeftX, bottomY, 0);
                        var pRight = new XYZ(parser.TableLeftX + parser.TableWidth, bottomY, 0);
                        var bottomLine = doc.Create.NewDetailCurve(targetView, Line.CreateBound(pLeft, pRight));

                        if (parser.VerticalLineStyle != null)
                            bottomLine.LineStyle = parser.VerticalLineStyle;

                        LogMessage?.Invoke($"✓ Bottom border drawn at Y={bottomY:F4}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"  Warning: Bottom border: {ex.Message}");
                    }

                    // ── 7. Commit ────────────────────────────────────────
                    int totalElements = headerCopied + totalDataElements + vLinesDrawn + 1;
                    LogMessage?.Invoke($"Committing transaction ({totalElements} elements total)...");
                    trans.Commit();
                    LogMessage?.Invoke("✓ Transaction committed");

                    // Activate the target view
                    try
                    {
                        app.ActiveUIDocument.ActiveView = targetView;
                        LogMessage?.Invoke($"✓ Activated view: \"{targetView.Name}\"");
                    }
                    catch { }

                    NotifyStatus?.Invoke($"Success: Legend \"{targetView.Name}\" created with {Items.Count} rows. ({totalElements} elements)");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"✗ ERROR: {ex.Message}");
                    if (trans.HasStarted()) trans.RollBack();
                    NotifyStatus?.Invoke("Error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Creates a new Legend view by duplicating an existing one and clearing its content.
        /// </summary>
        private View CreateLegendByDuplicate(Document doc, string name)
        {
            var allLegends = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.ViewType == ViewType.Legend && !v.IsTemplate)
                .ToList();

            if (!allLegends.Any())
                return null;

            // Pick the legend with the fewest elements
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
                    if (count == 0) break;
                }
            }

            ElementId newViewId = bestSource.Duplicate(ViewDuplicateOption.Duplicate);
            if (newViewId == null || newViewId == ElementId.InvalidElementId)
                return null;

            View newView = doc.GetElement(newViewId) as View;
            if (newView == null)
                return null;

            // Set the name
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

            // Clear elements
            ClearViewElements(doc, newView);

            return newView;
        }

        /// <summary>
        /// Removes all non-type elements from a view.
        /// </summary>
        private void ClearViewElements(Document doc, View view)
        {
            try
            {
                var elements = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .ToList();

                var idsToDelete = new List<ElementId>();
                foreach (var e in elements)
                {
                    // Only delete specific 2D detailing elements to prevent accidental View deletion
                    if (e is DetailCurve || 
                        e is TextNote || 
                        e is FilledRegion || 
                        e is IndependentTag || 
                        e is FamilyInstance)
                    {
                        idsToDelete.Add(e.Id);
                    }
                }

                if (idsToDelete.Count > 0)
                {
                    LogMessage?.Invoke($"  Clearing {idsToDelete.Count} existing elements from target...");
                    doc.Delete(idsToDelete);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"  Warning: Clear failed: {ex.Message}");
            }
        }

        public string GetName() => "LoadingScheduleHandler";
    }
}
