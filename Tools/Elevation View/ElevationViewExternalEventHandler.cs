using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ElevationView.ViewModels;

namespace RincoNhan.Tools.ElevationView
{
    public class ElevationViewExternalEventHandler : IExternalEventHandler
    {
        public ElevationViewViewModel ViewModel { get; set; }
        public string RequestAction { get; set; } = "UPDATE";

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            if (!(activeView is ViewSection))
            {
                TaskDialog.Show("Elevation View", "Please run this tool in a Section or Elevation view.");
                return;
            }

            try
            {
                if (RequestAction == "PICK_WALL" || RequestAction == "PICK_WALLS")
                {
                    try 
                    {
                        System.Collections.Generic.List<Wall> walls = new System.Collections.Generic.List<Wall>();
                        
                        if (RequestAction == "PICK_WALL")
                        {
                            Reference refObj = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, new WallSelectionFilter(), "Select reference wall");
                            Wall wall = doc.GetElement(refObj) as Wall;
                            if (wall != null) walls.Add(wall);
                        }
                        else
                        {
                            System.Collections.Generic.IList<Reference> refs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, new WallSelectionFilter(), "Select multiple reference walls");
                            foreach (Reference r in refs)
                            {
                                Wall wall = doc.GetElement(r) as Wall;
                                if (wall != null) walls.Add(wall);
                            }
                        }

                        if (walls.Count > 0)
                        {
                            var bounds = ElevationViewLogic.GetWallsBoundariesInView(walls, activeView);
                            ViewModel.RefWallLeftX = bounds.minX;
                            ViewModel.RefWallRightX = bounds.maxX;
                            ViewModel.RefWallWidth = (bounds.maxX - bounds.minX) * 304.8;
                            ViewModel.StatusMessage = walls.Count == 1 ? "Reference wall picked." : $"{walls.Count} walls picked.";
                            
                            // Immediately update crop once walls are picked
                            using (Transaction trans = new Transaction(doc, "Adjust Elevation View"))
                            {
                                trans.Start();
                                ElevationViewLogic.UpdateCropByReference(activeView, ViewModel.InitialCropBox, bounds.minX, bounds.maxX, 
                                    ViewModel.OffsetLeft, ViewModel.OffsetRight, ViewModel.OffsetTop, ViewModel.OffsetBottom);
                                trans.Commit();
                            }
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        ViewModel.StatusMessage = "Selection cancelled.";
                    }
                    finally
                    {
                        ViewModel.RequestShowWindow?.Invoke();
                    }
                }
                else
                {
                    using (Transaction trans = new Transaction(doc, "Adjust Elevation View"))
                    {
                        trans.Start();

                        if (RequestAction == "UPDATE_CROP")
                        {
                            if (ViewModel.HasRefWall)
                            {
                                ElevationViewLogic.UpdateCropByReference(activeView, ViewModel.InitialCropBox, ViewModel.RefWallLeftX, ViewModel.RefWallRightX, 
                                    ViewModel.OffsetLeft, ViewModel.OffsetRight, ViewModel.OffsetTop, ViewModel.OffsetBottom);
                            }
                            else
                            {
                                ElevationViewLogic.UpdateCropBox(activeView, ViewModel.InitialCropBox, 
                                    ViewModel.OffsetLeft, ViewModel.OffsetRight, ViewModel.OffsetTop, ViewModel.OffsetBottom);
                            }
                        }
                        else if (RequestAction == "ALIGN_LEVELS")
                        {
                            ElevationViewLogic.AlignLevels(activeView, ViewModel.LevelHeadOffset, ViewModel.LevelTailOffset);
                        }

                        trans.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName() => "ElevationViewEventHandler";
    }

    public class WallSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Wall;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
