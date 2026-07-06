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
            return true;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class CmdAutoPTFromCAD : IExternalCommand
    {
        private class Interval
        {
            public double Start;
            public double End;
        }

        private List<Line> MergeCollinearLines(List<Curve> curves)
        {
            List<List<Line>> groups = new List<List<Line>>();
            double angleTolerance = 0.087; // ~5 degrees

            foreach (Curve c in curves)
            {
                if (!(c is Line line)) continue;
                if (line.Length < 2.0) continue; // Ignore lines shorter than ~600mm (noise like anchor symbols, dimension ticks)

                bool added = false;
                foreach (List<Line> group in groups)
                {
                    Line firstInGroup = group[0];
                    XYZ dirGroup = firstInGroup.Direction;
                    XYZ dirLine = line.Direction;

                    if (dirGroup.CrossProduct(dirLine).GetLength() < angleTolerance)
                    {
                        XYZ p = line.GetEndPoint(0);
                        XYZ p0 = firstInGroup.GetEndPoint(0);
                        double dist = (p - p0).CrossProduct(dirGroup).GetLength();

                        if (dist < 0.05) // 0.05 feet = ~15mm transverse tolerance (strictly prevent merging parallel cables)
                        {
                            group.Add(line);
                            added = true;
                            break;
                        }
                    }
                }
                if (!added)
                {
                    groups.Add(new List<Line> { line });
                }
            }

            List<Line> mergedLines = new List<Line>();
            foreach (List<Line> group in groups)
            {
                XYZ origin = group[0].GetEndPoint(0);
                XYZ dir = group[0].Direction;
                
                List<Interval> intervals = new List<Interval>();
                foreach(Line l in group)
                {
                    double p1 = (l.GetEndPoint(0) - origin).DotProduct(dir);
                    double p2 = (l.GetEndPoint(1) - origin).DotProduct(dir);
                    intervals.Add(new Interval { Start = Math.Min(p1, p2), End = Math.Max(p1, p2) });
                }
                
                intervals = intervals.OrderBy(x => x.Start).ToList();
                
                List<Interval> mergedIntervals = new List<Interval>();
                Interval current = intervals[0];
                
                for (int i = 1; i < intervals.Count; i++)
                {
                    Interval next = intervals[i];
                    if (next.Start <= current.End + 6.5) // 2000mm gap tolerance
                    {
                        current.End = Math.Max(current.End, next.End);
                    }
                    else
                    {
                        mergedIntervals.Add(current);
                        current = next;
                    }
                }
                mergedIntervals.Add(current);
                
                foreach(Interval iv in mergedIntervals)
                {
                    XYZ ptStart = GetPointAtProjection(group, origin, dir, iv.Start);
                    XYZ ptEnd = GetPointAtProjection(group, origin, dir, iv.End);
                    if (ptStart.DistanceTo(ptEnd) > 0.1)
                    {
                        mergedLines.Add(Line.CreateBound(ptStart, ptEnd));
                    }
                }
            }

            return mergedLines;
        }

        private XYZ GetPointAtProjection(List<Line> group, XYZ origin, XYZ dir, double targetProj)
        {
            XYZ bestPt = origin + dir * targetProj;
            double minDiff = double.MaxValue;
            foreach (Line l in group)
            {
                XYZ p1 = l.GetEndPoint(0);
                XYZ p2 = l.GetEndPoint(1);
                double proj1 = (p1 - origin).DotProduct(dir);
                double proj2 = (p2 - origin).DotProduct(dir);
                
                if (Math.Abs(proj1 - targetProj) < minDiff)
                {
                    minDiff = Math.Abs(proj1 - targetProj);
                    bestPt = p1;
                }
                if (Math.Abs(proj2 - targetProj) < minDiff)
                {
                    minDiff = Math.Abs(proj2 - targetProj);
                    bestPt = p2;
                }
            }
            return bestPt;
        }



        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Prompt user to select a line on the CAD layer
                Reference refCadLine = uidoc.Selection.PickObject(ObjectType.PointOnElement, new CadLinkSelectionFilter(), "Pick a line on the CAD layer to generate PT");
                if (refCadLine == null) return Result.Cancelled;

                ImportInstance cadInstance = doc.GetElement(refCadLine) as ImportInstance;
                if (cadInstance == null) return Result.Cancelled;

                GeometryObject geoObj = cadInstance.GetGeometryObjectFromReference(refCadLine);
                if (geoObj == null)
                {
                    TaskDialog.Show("Error", "Could not determine the selected geometry.");
                    return Result.Failed;
                }

                GraphicsStyle gsSelected = doc.GetElement(geoObj.GraphicsStyleId) as GraphicsStyle;
                if (gsSelected == null || gsSelected.GraphicsStyleCategory == null)
                {
                    TaskDialog.Show("Error", "Selected element does not have a valid layer.");
                    return Result.Failed;
                }

                string selectedLayer = gsSelected.GraphicsStyleCategory.Name;

                // 2. Show UI to select heights and other settings
                AutoPTWindow window = new AutoPTWindow(selectedLayer);
                window.ShowDialog();

                if (window.IsCancelled)
                    return Result.Cancelled;

                GeometryElement geomElem = cadInstance.get_Geometry(new Options());
                if (geomElem == null) return Result.Failed;
                double defaultHp = window.HighPoint;
                double defaultLp = window.LowPoint;
                double chairSpacing = window.ChairSpacing;

                // 4. Extract paths from the selected layer
                List<Curve> allLayerCurves = new List<Curve>();
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
                                if (g is Curve curve)
                                {
                                    allLayerCurves.Add(curve);
                                }
                                else if (g is PolyLine polyLine)
                                {
                                    IList<XYZ> coords = polyLine.GetCoordinates();
                                    for(int i = 0; i < coords.Count - 1; i++)
                                    {
                                        allLayerCurves.Add(Line.CreateBound(coords[i], coords[i + 1]));
                                    }
                                }
                            }
                        }
                    }
                }

                if (allLayerCurves.Count == 0)
                {
                    TaskDialog.Show("Error", $"No valid curves found on layer: {selectedLayer}");
                    return Result.Failed;
                }

                // 5. Get PT Marker Family Symbol
                FamilySymbol markerSymbol = null;
                if (window.GeneratePTMarker)
                {
                    markerSymbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(x => x.FamilyName.Contains("PT Drape Marker") || x.Name.Contains("PT Drape Marker"));

                    if (markerSymbol == null)
                    {
                        TaskDialog.Show("Error", "Could not find Family 'PT Drape Marker' in the document.");
                        return Result.Failed;
                    }
                }

                FamilySymbol ptRincoSymbol = null;
                if (window.GeneratePTRinco)
                {
                    ptRincoSymbol = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(x => x.FamilyName.Contains("PT [Rinco]") || x.Name.Contains("PT [Rinco]"));

                    if (ptRincoSymbol == null)
                    {
                        TaskDialog.Show("Error", "Could not find Family 'PT [Rinco]' in the document.");
                        return Result.Failed;
                    }
                }

                int totalMarkers = 0;

                // 6. Generate markers
                List<Line> mergedLayerLines = MergeCollinearLines(allLayerCurves);
                using (Transaction t = new Transaction(doc, "Auto PT from CAD"))
                {
                    t.Start();
                    
                    if (markerSymbol != null && !markerSymbol.IsActive)
                        markerSymbol.Activate();

                    if (ptRincoSymbol != null && !ptRincoSymbol.IsActive)
                        ptRincoSymbol.Activate();

                    // Tính toán cao độ Z thực tế của view hiện tại để đặt Family
                    double viewZ = 0;
                    if (uidoc.ActiveView.SketchPlane != null)
                    {
                        viewZ = uidoc.ActiveView.SketchPlane.GetPlane().Origin.Z;
                    }
                    else if (uidoc.ActiveView.GenLevel != null)
                    {
                        viewZ = uidoc.ActiveView.GenLevel.Elevation;
                    }


                    foreach (Curve curve in mergedLayerLines)
                    {
                        double pathLengthFeet = curve.Length;
                        if (pathLengthFeet < 0.1) continue;

                        XYZ p1 = curve.GetEndPoint(0);
                        XYZ p2 = curve.GetEndPoint(1);
                        // Tạo đoạn thẳng bám đúng View Plane Z
                        Line flatL = Line.CreateBound(new XYZ(p1.X, p1.Y, viewZ), new XYZ(p2.X, p2.Y, viewZ));

                        if (window.GeneratePTRinco && ptRincoSymbol != null)
                        {
                            try
                            {
                                FamilyInstance ptInst = doc.Create.NewFamilyInstance(flatL, ptRincoSymbol, uidoc.ActiveView);
                            }
                            catch { }
                        }

                        if (!window.GeneratePTMarker) continue;

                        var hp1 = new PTHighPoint(0, defaultHp, "end");
                        var hp2 = new PTHighPoint(pathLengthFeet * 304.8, defaultHp, "end");

                        PTProfileCalculator.PTProfile(hp1, hp2, defaultLp,
                            out double[] S1, out double[] S2, out double[] S3,
                            out PTPoint inf1, out PTPoint inf2, out PTPoint lowPt);

                        double lengthMm = pathLengthFeet * 304.8;
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
                            
                            double targetLengthFeet = xMm / 304.8;
                            
                            double normalizedParam = targetLengthFeet / curve.Length;
                            if (normalizedParam < 0) normalizedParam = 0;
                            if (normalizedParam > 1) normalizedParam = 1;
                            
                            XYZ markerLocRaw = curve.Evaluate(normalizedParam, true);
                            Transform der = curve.ComputeDerivatives(normalizedParam, true);
                            XYZ tangent = der.BasisX.Normalize();
                            
                            XYZ markerLoc = new XYZ(markerLocRaw.X, markerLocRaw.Y, viewZ);

                            double angle = Math.Atan2(tangent.Y, tangent.X);
                            if (angle < 0) angle += 2 * Math.PI;

                            try
                            {
                                FamilyInstance marker = null;
                                if (markerSymbol.Family.FamilyPlacementType == FamilyPlacementType.ViewBased)
                                    marker = doc.Create.NewFamilyInstance(markerLoc, markerSymbol, uidoc.ActiveView);
                                else if (uidoc.ActiveView.GenLevel != null)
                                    marker = doc.Create.NewFamilyInstance(markerLoc, markerSymbol, uidoc.ActiveView.GenLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                else
                                    marker = doc.Create.NewFamilyInstance(markerLoc, markerSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                
                                Line axis = Line.CreateBound(markerLoc, markerLoc + XYZ.BasisZ);
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
                            catch (Exception ex) 
                            {
                                TaskDialog.Show("Marker Error", "Could not place PT Drape Marker: " + ex.Message);
                            }
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

                    t.Commit();
                }

                TaskDialog.Show("Success", $"Successfully generated {totalMarkers} PT markers across {mergedLayerLines.Count} lines.");
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
