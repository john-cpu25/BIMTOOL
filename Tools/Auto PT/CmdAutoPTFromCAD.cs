using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.AutoPT
{
    public class CadLinkSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is ImportInstance;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdAutoPTFromCAD : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Prompt user to select CAD link
                Reference refCad = uidoc.Selection.PickObject(ObjectType.Element, new CadLinkSelectionFilter(), "Select CAD Link containing PT layout");
                if (refCad == null) return Result.Cancelled;

                ImportInstance cadInstance = doc.GetElement(refCad) as ImportInstance;
                if (cadInstance == null) return Result.Cancelled;

                // 2. Get all layers in CAD link
                HashSet<string> layerNames = new HashSet<string>();
                GeometryElement geomElem = cadInstance.get_Geometry(new Options());
                if (geomElem != null)
                {
                    foreach (GeometryObject geomObj in geomElem)
                    {
                        if (geomObj is GeometryInstance geomInst)
                        {
                            GeometryElement instGeom = geomInst.GetInstanceGeometry();
                            foreach (GeometryObject g in instGeom)
                            {
                                if (g is Curve || g is PolyLine)
                                {
                                    GraphicsStyle gs = doc.GetElement(g.GraphicsStyleId) as GraphicsStyle;
                                    if (gs != null && gs.GraphicsStyleCategory != null)
                                    {
                                        layerNames.Add(gs.GraphicsStyleCategory.Name);
                                    }
                                }
                            }
                        }
                    }
                }

                if (layerNames.Count == 0)
                {
                    TaskDialog.Show("Error", "No valid line layers found in the CAD link.");
                    return Result.Failed;
                }

                List<string> sortedLayers = layerNames.ToList();
                sortedLayers.Sort();

                // 3. Show UI to select layer and heights
                AutoPTWindow window = new AutoPTWindow(sortedLayers);
                window.ShowDialog();

                if (window.IsCancelled)
                    return Result.Cancelled;

                string selectedLayer = window.SelectedLayer;
                double defaultHp = window.HighPoint;
                double defaultLp = window.LowPoint;
                double chairSpacing = window.ChairSpacing;

                // 4. Extract paths from the selected layer
                List<List<XYZ>> paths = new List<List<XYZ>>();
                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        foreach (GeometryObject g in instGeom)
                        {
                            GraphicsStyle gs = doc.GetElement(g.GraphicsStyleId) as GraphicsStyle;
                            if (gs != null && gs.GraphicsStyleCategory != null && gs.GraphicsStyleCategory.Name == selectedLayer)
                            {
                                List<XYZ> pts = new List<XYZ>();
                                if (g is Curve curve)
                                {
                                    pts.Add(curve.GetEndPoint(0));
                                    pts.Add(curve.GetEndPoint(1));
                                }
                                else if (g is PolyLine polyLine)
                                {
                                    pts.AddRange(polyLine.GetCoordinates());
                                }

                                if (pts.Count > 1)
                                {
                                    paths.Add(pts);
                                }
                            }
                        }
                    }
                }

                if (paths.Count == 0)
                {
                    TaskDialog.Show("Error", $"No valid paths found on layer: {selectedLayer}");
                    return Result.Failed;
                }

                // 5. Get PT Marker Family Symbol
                FamilySymbol markerSymbol = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(x => x.FamilyName.Contains("PT Drape Marker") || x.Name.Contains("PT Drape Marker"));

                if (markerSymbol == null)
                {
                    TaskDialog.Show("Error", "Could not find Family 'PT Drape Marker' in the document.");
                    return Result.Failed;
                }

                int totalMarkers = 0;

                // 6. Generate markers
                using (Transaction t = new Transaction(doc, "Auto PT from CAD"))
                {
                    t.Start();
                    
                    if (!markerSymbol.IsActive)
                        markerSymbol.Activate();

                    foreach (var pts in paths)
                    {
                        if (pts.Count < 2) continue;

                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            XYZ p1 = pts[i];
                            XYZ p2 = pts[i + 1];

                            string condition1 = (i == 0) ? "end" : "continuous";
                            string condition2 = (i == pts.Count - 2) ? "end" : "continuous";

                            double length = p1.DistanceTo(p2);
                            if (length < 0.1) continue;

                            var hp1 = new PTHighPoint(0, defaultHp, condition1);
                            var hp2 = new PTHighPoint(length * 304.8, defaultHp, condition2); // Revit internal to mm

                            PTProfileCalculator.PTProfile(hp1, hp2, defaultLp,
                                out double[] S1, out double[] S2, out double[] S3,
                                out PTPoint inf1, out PTPoint inf2, out PTPoint lowPt);

                            double lengthMm = length * 304.8;
                            int gapNumber = 1;
                            if (lengthMm >= 8500)
                                gapNumber = (int)Math.Round(lengthMm / chairSpacing);
                            else if (lengthMm >= 4950 && lengthMm < 8500)
                                gapNumber = (int)Math.Round(lengthMm / (0.9 * chairSpacing));
                            else
                                gapNumber = (int)Math.Round(lengthMm / (0.85 * chairSpacing));

                            if (gapNumber == 0) gapNumber = 1;
                            if (gapNumber % 2 == 1 && Math.Abs(hp1.Y - hp2.Y) < 0.1 && hp1.Condition == hp2.Condition)
                                gapNumber++;

                            double gapLengthMm = lengthMm / gapNumber;
                            double minPtHeightFound = Math.Max(hp1.Y, hp2.Y);
                            double minPtXDelta = hp2.X;
                            double minPtXSelect = 0;

                            List<FamilyInstance> markerList = new List<FamilyInstance>();
                            List<double> markerXList = new List<double>();
                            
                            double angle = 0;
                            if (Math.Abs(p2.X - p1.X) < 0.001)
                                angle = Math.PI / 2.0;
                            else
                                angle = Math.Atan((p2.Y - p1.Y) / (p2.X - p1.X));
                            if (angle > 0) angle += Math.PI;

                            for (int j = 0; j <= gapNumber; j++)
                            {
                                double xMm = gapLengthMm * j;
                                double yMm = 0;

                                if (S1 == null && S3 == null)
                                {
                                    if (S2 != null && S2.Length == 3) yMm = PTProfileCalculator.QuadraticYValue(S2, xMm);
                                    else if (S2 != null && S2.Length == 2) yMm = PTProfileCalculator.LinearYValue(S2, xMm);
                                }
                                else if (S1 != null && S3 == null)
                                {
                                    if (inf1 != null && xMm <= inf1.X) yMm = PTProfileCalculator.QuadraticYValue(S1, xMm);
                                    else yMm = PTProfileCalculator.QuadraticYValue(S2, xMm);
                                }
                                else if (S1 == null && S3 != null)
                                {
                                    if (inf2 != null && xMm < inf2.X) yMm = PTProfileCalculator.QuadraticYValue(S2, xMm);
                                    else yMm = PTProfileCalculator.QuadraticYValue(S3, xMm);
                                }
                                else if (S1 != null && S3 != null)
                                {
                                    if (inf1 != null && xMm <= inf1.X) yMm = PTProfileCalculator.QuadraticYValue(S1, xMm);
                                    else if (inf1 != null && inf2 != null && xMm > inf1.X && xMm < inf2.X) yMm = PTProfileCalculator.QuadraticYValue(S2, xMm);
                                    else yMm = PTProfileCalculator.QuadraticYValue(S3, xMm);
                                }

                                yMm = PTProfileCalculator.RoundNearest5(yMm);

                                if (defaultLp > 0 && lowPt != null)
                                {
                                    if (yMm < minPtHeightFound) minPtHeightFound = yMm;
                                    if (Math.Abs(xMm - lowPt.X) < minPtXDelta)
                                    {
                                        minPtXDelta = Math.Abs(xMm - lowPt.X);
                                        minPtXSelect = xMm;
                                    }
                                }

                                string markerType = "intermediate";
                                if (Math.Abs(xMm - hp1.X) < 0.1) markerType = hp1.Condition;
                                else if (Math.Abs(xMm - hp2.X) < 0.1) markerType = hp2.Condition;

                                markerXList.Add(xMm);
                                
                                double markerX = p1.X + (xMm / lengthMm) * (p2.X - p1.X);
                                double markerY = p1.Y + (xMm / lengthMm) * (p2.Y - p1.Y);

                                XYZ markerLoc = new XYZ(markerX, markerY, 0);
                                FamilyInstance marker = doc.Create.NewFamilyInstance(markerLoc, markerSymbol, uidoc.ActiveView);
                                
                                Line axis = Line.CreateBound(markerLoc, new XYZ(markerX, markerY, 1));
                                ElementTransformUtils.RotateElement(doc, marker.Id, axis, angle);

                                if (markerType == "end")
                                {
                                    yMm += 10;
                                    marker.LookupParameter("Centre Line Point")?.Set(1);
                                    marker.LookupParameter("Main Point")?.Set(0);
                                    marker.LookupParameter("Intermediate Point")?.Set(0);
                                }
                                else if (markerType == "continuous")
                                {
                                    marker.LookupParameter("Centre Line Point")?.Set(0);
                                    marker.LookupParameter("Main Point")?.Set(1);
                                    marker.LookupParameter("Intermediate Point")?.Set(0);
                                }
                                else if (markerType == "intermediate")
                                {
                                    marker.LookupParameter("Centre Line Point")?.Set(0);
                                    marker.LookupParameter("Main Point")?.Set(0);
                                    marker.LookupParameter("Intermediate Point")?.Set(1);
                                }

                                marker.LookupParameter("PT Drape Height")?.Set(yMm);
                                markerList.Add(marker);
                                totalMarkers++;
                            }

                            if (defaultLp > 0)
                            {
                                for (int j = 0; j < markerList.Count; j++)
                                {
                                    if (Math.Abs(minPtXSelect - markerXList[j]) < 0.001)
                                    {
                                        markerList[j].LookupParameter("Centre Line Point")?.Set(0);
                                        markerList[j].LookupParameter("Main Point")?.Set(1);
                                        markerList[j].LookupParameter("Intermediate Point")?.Set(0);
                                        
                                        var drapeParam = markerList[j].LookupParameter("PT Drape Height");
                                        if (drapeParam != null && drapeParam.AsInteger() != (int)defaultLp)
                                        {
                                            drapeParam.Set(defaultLp);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    t.Commit();
                }

                TaskDialog.Show("Success", $"Successfully generated {totalMarkers} PT markers across {paths.Count} path elements.");
                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message + "\n" + ex.StackTrace);
                return Result.Failed;
            }
        }
    }
}
