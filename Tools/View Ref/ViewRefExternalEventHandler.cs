using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.ViewRef.ViewModels;

namespace RincoNhan.Tools.ViewRef
{
    public class ViewRefExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public ViewRefViewModel ViewModel { get; set; }

        public ViewRefExternalEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (RequestAction == "PICK_AND_PLACE")
            {
                PickAndPlaceViewRefs(uidoc, doc);
            }
        }

        private void PickAndPlaceViewRefs(UIDocument uidoc, Document doc)
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilterWall(), "Select Walls to place View References");
                List<Wall> walls = refs.Select(r => doc.GetElement(r) as Wall).Where(w => w != null).ToList();

                if (!walls.Any())
                {
                    ViewModel.StatusMessage = "No walls selected.";
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                int placedCount = 0;
                int skippedCount = 0;
                
                var symbolsToPlace = new List<ElementType>();
                if (ViewModel.IsNSelected && ViewModel.SelectedNFamily != null) symbolsToPlace.Add(ViewModel.SelectedNFamily);
                if (ViewModel.IsWSelected && ViewModel.SelectedWFamily != null) symbolsToPlace.Add(ViewModel.SelectedWFamily);
                if (ViewModel.IsESelected && ViewModel.SelectedEFamily != null) symbolsToPlace.Add(ViewModel.SelectedEFamily);
                if (ViewModel.IsSSelected && ViewModel.SelectedSFamily != null) symbolsToPlace.Add(ViewModel.SelectedSFamily);

                if (!symbolsToPlace.Any())
                {
                    ViewModel.StatusMessage = "Warning: No View Reference type selected.";
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                // Check for ReferenceViewer class via reflection (Added in Revit 2024)
                Type refViewerType = typeof(Element).Assembly.GetType("Autodesk.Revit.DB.ReferenceViewer");
                if (refViewerType == null)
                {
                    TaskDialog.Show("RincoNhan", "Native View Reference API is only available in Revit 2024 or newer.");
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                var createMethod = refViewerType.GetMethod("Create", new Type[] { typeof(Document), typeof(ElementId), typeof(ElementId), typeof(XYZ), typeof(ElementId) });
                if (createMethod == null)
                {
                    TaskDialog.Show("RincoNhan", "Could not find ReferenceViewer.Create method in this version of Revit.");
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                using (Transaction trans = new Transaction(doc, "Place View References"))
                {
                    trans.Start();

                    foreach (Wall wall in walls)
                    {
                        // Skip walls inside groups
                        if (wall.GroupId != ElementId.InvalidElementId)
                        {
                            skippedCount++;
                            continue;
                        }

                        LocationCurve locCurve = wall.Location as LocationCurve;
                        if (locCurve == null) continue;

                        Curve curve = locCurve.Curve;
                        if (curve == null || !(curve is Line)) continue;

                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        
                        // 3. Calculate MidPoint and Direction
                        XYZ midPoint = (start + end) / 2.0;
                        XYZ direction = (end - start).Normalize();
                        
                        // Calculate Perpendicular (outward vector)
                        XYZ perpendicular = new XYZ(-direction.Y, direction.X, 0);

                        // 5. Check if wall is flipped, ensure consistent outward direction
                        if (wall.Flipped)
                        {
                            perpendicular = -perpendicular;
                        }

                        // 4. Get wall thickness (offset)
                        double offset = wall.Width / 2.0;

                        // 6. Compute placement point
                        XYZ placementPoint = midPoint + perpendicular * offset;

                        // 7. Place selected View References (Native)
                        try
                        {
                            foreach (var sym in symbolsToPlace)
                            {
                                // Invoke ReferenceViewer.Create(doc, viewId, targetViewId, location, typeId)
                                Element viewRef = createMethod.Invoke(null, new object[] { doc, doc.ActiveView.Id, ElementId.InvalidElementId, placementPoint, sym.Id }) as Element;
                                
                                if (viewRef != null)
                                {
                                    // 8. Rotate tag to align with wall direction
                                    double angle = Math.Atan2(direction.Y, direction.X);
                                    
                                    // Create a vertical axis at the placement point
                                    Line axis = Line.CreateBound(placementPoint, placementPoint + XYZ.BasisZ);
                                    
                                    ElementTransformUtils.RotateElement(doc, viewRef.Id, axis, angle);
                                    placedCount++;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore placement errors for specific walls
                        }
                    }

                    trans.Commit();
                }

                ViewModel.StatusMessage = $"Placed {placedCount} view references. " + (skippedCount > 0 ? $"Skipped {skippedCount} walls in groups." : "");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.StatusMessage = "Operation cancelled.";
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Failed to place view references: " + ex.Message);
                ViewModel.StatusMessage = "Error occurred.";
            }
            finally
            {
                ViewModel.RequestShowWindow?.Invoke();
            }
        }

        public string GetName() => "ViewRefExternalEventHandler";

        private class SelectionFilterWall : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
