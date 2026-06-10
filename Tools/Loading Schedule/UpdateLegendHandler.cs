using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingSchedule.Models;
using RincoNhan.Tools.LoadingSchedule.Services;

namespace RincoNhan.Tools.LoadingSchedule
{
    public class UpdateLegendHandler : IExternalEventHandler
    {
        public ElementId TemplateLegendId { get; set; }
        
        public class UpdateJob
        {
            public ElementId TargetLegendId { get; set; }
            public ElementId SourceViewId { get; set; }
        }

        public List<UpdateJob> Jobs { get; set; } = new List<UpdateJob>();

        public Action<int> ProgressChanged { get; set; }
        public Action<string> LogMessage { get; set; }
        public Action<string> Completed { get; set; }

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            using (Transaction trans = new Transaction(doc, "Update Legends"))
            {
                trans.Start();
                try
                {
                    View templateView = doc.GetElement(TemplateLegendId) as View;
                    if (templateView == null || templateView.ViewType != ViewType.Legend)
                    {
                        Completed?.Invoke("Error: Invalid template Legend view.");
                        trans.RollBack();
                        return;
                    }

                    LogMessage?.Invoke($"Parsing template: \"{templateView.Name}\"...");
                    var parser = new LegendTemplateParser();
                    if (!parser.Parse(doc, templateView, LogMessage))
                    {
                        Completed?.Invoke("Error: Failed to parse template. " + parser.Summary);
                        trans.RollBack();
                        return;
                    }

                    int total = Jobs.Count;
                    int current = 0;

                    foreach (var job in Jobs)
                    {
                        current++;
                        if (job.TargetLegendId == null || job.TargetLegendId == ElementId.InvalidElementId || 
                            job.SourceViewId == null || job.SourceViewId == ElementId.InvalidElementId)
                        {
                            LogMessage?.Invoke($"Skipping job {current}: Missing Legend or View.");
                            continue;
                        }

                        View targetView = doc.GetElement(job.TargetLegendId) as View;
                        View sourceView = doc.GetElement(job.SourceViewId) as View;

                        if (targetView == null || sourceView == null) continue;

                        LogMessage?.Invoke($"--- Updating Legend '{targetView.Name}' based on View '{sourceView.Name}' ---");

                        // Find unique hatches in sourceView
                        var filledRegions = new FilteredElementCollector(doc, sourceView.Id)
                            .OfClass(typeof(FilledRegion))
                            .Cast<FilledRegion>()
                            .ToList();

                        var grouped = filledRegions
                            .GroupBy(fr => fr.GetTypeId())
                            .OrderBy(g =>
                            {
                                var t = doc.GetElement(g.Key) as FilledRegionType;
                                if (t != null)
                                {
                                    var typeMarkParam = t.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                                    string typeMarkStr = typeMarkParam?.AsString() ?? "";
                                    if (int.TryParse(typeMarkStr, out int num))
                                        return num.ToString("D5");
                                    return typeMarkStr;
                                }
                                return "";
                            })
                            .Select(g => g.Key)
                            .ToList();

                        if (!grouped.Any())
                        {
                            LogMessage?.Invoke("  No hatches found in the view. Clearing legend...");
                        }
                        else
                        {
                            LogMessage?.Invoke($"  Found {grouped.Count} unique hatch types.");
                        }

                        // Clear existing elements in target legend
                        var elements = new FilteredElementCollector(doc, targetView.Id)
                            .WhereElementIsNotElementType()
                            .ToList();

                        var idsToDelete = new List<ElementId>();
                        foreach (var e in elements)
                        {
                            if (e is DetailCurve || e is TextNote || e is FilledRegion || e is IndependentTag || e is FamilyInstance)
                            {
                                idsToDelete.Add(e.Id);
                            }
                        }

                        if (idsToDelete.Count > 0)
                        {
                            doc.Delete(idsToDelete);
                        }

                        // Draw new layout
                        if (grouped.Any())
                        {
                            // Copy header
                            if (parser.HeaderElementIds.Count > 0)
                            {
                                ElementTransformUtils.CopyElements(templateView, parser.HeaderElementIds, targetView, Transform.Identity, new CopyPasteOptions());
                            }

                            // Copy data rows
                            for (int i = 0; i < grouped.Count; i++)
                            {
                                var typeId = grouped[i];
                                double offsetY = -parser.RowHeight * i;
                                var transform = Transform.CreateTranslation(new XYZ(0, offsetY, 0));

                                var copiedIds = ElementTransformUtils.CopyElements(templateView, parser.DataRowElementIds, targetView, transform, new CopyPasteOptions());

                                if (copiedIds != null && copiedIds.Count > 0)
                                {
                                    foreach (var copiedId in copiedIds)
                                    {
                                        var elem = doc.GetElement(copiedId);
                                        if (elem is FilledRegion fr)
                                        {
                                            fr.ChangeTypeId(typeId);
                                        }
                                    }
                                }
                            }

                            // Draw vertical lines
                            if (parser.VerticalLineXPositions.Count > 0)
                            {
                                double vLineTop = parser.TableTopY;
                                double vLineBottom = parser.HeaderBottomY - parser.RowHeight * grouped.Count;

                                foreach (double x in parser.VerticalLineXPositions)
                                {
                                    var p1 = new XYZ(x, vLineTop, 0);
                                    var p2 = new XYZ(x, vLineBottom, 0);
                                    var line = doc.Create.NewDetailCurve(targetView, Line.CreateBound(p1, p2));
                                    if (parser.VerticalLineStyle != null) line.LineStyle = parser.VerticalLineStyle;
                                }
                                
                                // Bottom line
                                var pLeft = new XYZ(parser.TableLeftX, vLineBottom, 0);
                                var pRight = new XYZ(parser.TableLeftX + parser.TableWidth, vLineBottom, 0);
                                var bottomLine = doc.Create.NewDetailCurve(targetView, Line.CreateBound(pLeft, pRight));
                                if (parser.VerticalLineStyle != null) bottomLine.LineStyle = parser.VerticalLineStyle;
                            }
                        }

                        int percentage = (int)((current / (double)total) * 100);
                        ProgressChanged?.Invoke(percentage);
                    }

                    trans.Commit();
                    Completed?.Invoke($"Success: Updated {total} legend(s).");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"✗ ERROR: {ex.Message}");
                    if (trans.HasStarted()) trans.RollBack();
                    Completed?.Invoke("Error: " + ex.Message);
                }
            }
        }

        public string GetName() => "UpdateLegendHandler";
    }
}
