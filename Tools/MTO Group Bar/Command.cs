using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Collections.ObjectModel;
using RincoNhan.Tools.MtoGroupBar.Models;
using RincoNhan.Tools.MtoGroupBar.UI;

namespace RincoNhan.Tools.MtoGroupBar
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        // Keep a static reference to the window so we don't open multiples
        public static MtoGroupBarWindow CurrentWindow { get; set; }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            try
            {
                // 1. Collect Rebars
                var rebars = new FilteredElementCollector(doc, activeView.Id)
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.SuperComponent == null)
                    .Where(fi => fi.Symbol != null && fi.Symbol.Family != null)
                    .Where(fi => fi.Symbol.Family.Name.Contains("Reo__Reinforcement_DistributionAdjustable[Rinco]"))
                    .ToList();

                // 2. Collect Laps
                var laps = new FilteredElementCollector(doc, activeView.Id)
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.SuperComponent == null)
                    .Where(fi => fi.Symbol != null && fi.Symbol.Family != null)
                    .Where(fi => fi.Symbol.Family.Name.Contains("RINCO_LAPSIGHN") || fi.Name.Contains("RINCO_LAPSIGHN"))
                    .ToList();

                if (rebars.Count == 0 || laps.Count == 0)
                {
                    TaskDialog.Show("MTO Group Bar", "Không tìm thấy đủ Rebar hoặc Lap trong View hiện tại.");
                    return Result.Succeeded;
                }

                // 3. Build Adjacency List for Rebars
                Dictionary<ElementId, List<ElementId>> adj = new Dictionary<ElementId, List<ElementId>>();
                foreach (var rebar in rebars)
                {
                    adj[rebar.Id] = new List<ElementId>();
                }

                foreach (var lap in laps)
                {
                    List<FamilyInstance> intersectedRebars = new List<FamilyInstance>();

                    foreach (var rebar in rebars)
                    {
                        if (Intersect(lap, rebar, activeView))
                        {
                            intersectedRebars.Add(rebar);
                        }
                    }

                    // Connect all rebars that intersect this lap
                    for (int i = 0; i < intersectedRebars.Count; i++)
                    {
                        for (int j = i + 1; j < intersectedRebars.Count; j++)
                        {
                            ElementId id1 = intersectedRebars[i].Id;
                            ElementId id2 = intersectedRebars[j].Id;

                            if (!adj[id1].Contains(id2)) adj[id1].Add(id2);
                            if (!adj[id2].Contains(id1)) adj[id2].Add(id1);
                        }
                    }
                }

                // 4. Find Connected Components and Update Parameter
                using (Transaction tx = new Transaction(doc, "MTO Group Bar"))
                {
                    tx.Start();

                    HashSet<ElementId> visited = new HashSet<ElementId>();
                    int groupIndex = 1;
                    ObservableCollection<MtoGroupItem> groupItems = new ObservableCollection<MtoGroupItem>();

                    foreach (var rebar in rebars)
                    {
                        if (!visited.Contains(rebar.Id))
                        {
                            List<ElementId> component = new List<ElementId>();
                            Queue<ElementId> queue = new Queue<ElementId>();
                            
                            queue.Enqueue(rebar.Id);
                            visited.Add(rebar.Id);

                            while (queue.Count > 0)
                            {
                                ElementId curr = queue.Dequeue();
                                component.Add(curr);

                                if (adj.ContainsKey(curr))
                                {
                                    foreach (var neighbor in adj[curr])
                                    {
                                        if (!visited.Contains(neighbor))
                                        {
                                            visited.Add(neighbor);
                                            queue.Enqueue(neighbor);
                                        }
                                    }
                                }
                            }

                            if (component.Count > 1) // Only group if there is more than 1 connected rebar
                            {
                                string groupName = "Group " + groupIndex;
                                foreach (var id in component)
                                {
                                    Element elem = doc.GetElement(id);
                                    Parameter param = elem.LookupParameter("Blank Text");
                                    if (param != null && !param.IsReadOnly)
                                    {
                                        param.Set(groupName);
                                    }
                                }
                                
                                groupItems.Add(new MtoGroupItem 
                                {
                                    GroupName = groupName,
                                    Count = component.Count,
                                    ElementIds = component
                                });

                                groupIndex++;
                            }
                            else
                            {
                                // Independent rebar - optionally clear the Blank Text
                                Element elem = doc.GetElement(component[0]);
                                Parameter param = elem.LookupParameter("Blank Text");
                                if (param != null && !param.IsReadOnly)
                                {
                                    param.Set("");
                                }
                            }
                        }
                    }

                    tx.Commit();

                    if (groupItems.Count > 0)
                    {
                        if (CurrentWindow != null && CurrentWindow.IsLoaded)
                        {
                            CurrentWindow.Focus();
                        }
                        else
                        {
                            MtoGroupBarEventHandler handler = new MtoGroupBarEventHandler();
                            ExternalEvent exEvent = ExternalEvent.Create(handler);

                            CurrentWindow = new MtoGroupBarWindow(groupItems, exEvent, handler);
                            CurrentWindow.Show();
                        }
                    }
                    else
                    {
                        TaskDialog.Show("MTO Group Bar", "Không tìm thấy nhóm thép nối nào.");
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private bool Intersect(FamilyInstance lap, FamilyInstance rebar, View activeView)
        {
            BoundingBoxXYZ bb1 = lap.get_BoundingBox(activeView);
            BoundingBoxXYZ bb2 = rebar.get_BoundingBox(activeView);
            
            if (bb1 == null || bb2 == null) return false;
            
            // Tolerance to handle slight inaccuracies
            double tol = 0.05; 
            
            bool overlapX = bb1.Min.X <= bb2.Max.X + tol && bb1.Max.X >= bb2.Min.X - tol;
            bool overlapY = bb1.Min.Y <= bb2.Max.Y + tol && bb1.Max.Y >= bb2.Min.Y - tol;
            bool overlapZ = bb1.Min.Z <= bb2.Max.Z + tol && bb1.Max.Z >= bb2.Min.Z - tol;

            if (!(overlapX && overlapY && overlapZ)) return false;

            // Check if they are parallel using their Location (Rotation or Curve Direction)
            double rotLap = GetRotation(lap);
            double rotRebar = GetRotation(rebar);

            // Normalize to [0, PI)
            rotLap = rotLap % Math.PI;
            if (rotLap < 0) rotLap += Math.PI;
            
            rotRebar = rotRebar % Math.PI;
            if (rotRebar < 0) rotRebar += Math.PI;

            double rotDiff = Math.Abs(rotLap - rotRebar);
            double angTol = 0.1; // Roughly 5.7 degrees tolerance

            // If the difference is not close to 0 or PI, they are not parallel
            if (rotDiff > angTol && Math.Abs(rotDiff - Math.PI) > angTol)
            {
                return false;
            }

            return true;
        }

        private double GetRotation(FamilyInstance fi)
        {
            if (fi.Location is LocationPoint lp)
            {
                return lp.Rotation;
            }
            else if (fi.Location is LocationCurve lc && lc.Curve is Line line)
            {
                XYZ dir = line.Direction;
                return Math.Atan2(dir.Y, dir.X);
            }
            return 0;
        }
    }
}
