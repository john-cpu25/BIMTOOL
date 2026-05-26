using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.WallDivide.ViewModels;

namespace RincoNhan.Tools.WallDivide
{
    public class WallDivideExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public WallDivideViewModel ViewModel { get; set; }

        public WallDivideExternalEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (RequestAction == "PICK_GROUP")
            {
                PickGroupAction(uidoc);
            }
            else if (RequestAction == "PICK_WALLS")
            {
                PickWallsAction(uidoc);
            }
            else if (RequestAction == "PICK_FLOOR")
            {
                PickFloorAction(uidoc);
            }
            else if (RequestAction == "APPLY_TOP_OFFSET")
            {
                ApplyTopOffsetAction(doc);
            }
            else if (RequestAction == "DISALLOW_JOIN")
            {
                DisallowJoinAction(doc);
            }
            else if (RequestAction == "HIGHLIGHT")
            {
                HighlightAction(doc);
            }
            else if (RequestAction == "DIVIDE")
            {
                DivideAction(doc);
            }
            else if (RequestAction == "BOX_WALL")
            {
                BoxWallAction(doc);
            }
        }

        private void PickWallsAction(UIDocument uidoc)
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilterWall(), "Select Walls to divide");
                List<Wall> walls = refs.Select(r => uidoc.Document.GetElement(r) as Wall).Where(w => w != null).ToList();
                if (walls.Any())
                {
                    ViewModel.LoadWalls(walls);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void PickGroupAction(UIDocument uidoc)
        {
            try
            {
                Reference refGroup = uidoc.Selection.PickObject(ObjectType.Element, new GroupSelectionFilter(), "Select a Group containing walls");
                Group group = uidoc.Document.GetElement(refGroup) as Group;
                if (group != null)
                {
                    ViewModel.LoadGroup(group);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void PickFloorAction(UIDocument uidoc)
        {
            try
            {
                Reference refFloor = uidoc.Selection.PickObject(ObjectType.Element, new SelectionFilterFloor(), "Select a Floor to calculate Top Offset");
                Floor floor = uidoc.Document.GetElement(refFloor) as Floor;
                if (floor != null)
                {
                    ViewModel.LoadFloor(floor);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void ApplyTopOffsetAction(Document doc)
        {
            if (ViewModel.FloorId == null) return;
            double thickMm = ViewModel.FloorThickness;
            double thickFt = UnitUtils.ConvertToInternalUnits(thickMm, UnitTypeId.Millimeters);

            using (Transaction trans = new Transaction(doc, "Apply Top Offset"))
            {
                trans.Start();
                int count = 0;
                foreach (var item in ViewModel.WallItems.Where(w => w.IsSelected))
                {
                    Wall wall = doc.GetElement(item.WallId) as Wall;
                    if (wall != null)
                    {
                        Parameter topOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET);
                        if (topOffset != null && !topOffset.IsReadOnly)
                        {
                            topOffset.Set(-thickFt);
                            count++;
                        }
                    }
                }
                trans.Commit();
                ViewModel.SetStatus($"Applied Top Offset -{thickMm:F0}mm to {count} walls.", true);
            }
        }

        private void DisallowJoinAction(Document doc)
        {
            using (Transaction trans = new Transaction(doc, "Disallow Wall Join"))
            {
                trans.Start();
                int count = 0;
                foreach (var item in ViewModel.WallItems.Where(w => w.IsSelected))
                {
                    Wall wall = doc.GetElement(item.WallId) as Wall;
                    if (wall != null)
                    {
                        WallUtils.DisallowWallJoinAtEnd(wall, 0);
                        WallUtils.DisallowWallJoinAtEnd(wall, 1);
                        count++;
                    }
                }
                trans.Commit();
                ViewModel.SetStatus($"Disallowed join for {count} walls.", true);
            }
        }

        private void BoxWallAction(Document doc)
        {
            if (ViewModel.SelectedWallId == null) return;

            Element wall = doc.GetElement(ViewModel.SelectedWallId);
            if (wall == null) return;

            // Find or create a 3D view
            View3D view3d = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate);

            if (view3d == null)
            {
                TaskDialog.Show("Box Wall", "Please open a 3D view or create one first.");
                return;
            }

            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox == null) return;

            // Expand bbox slightly
            XYZ expand = new XYZ(2, 2, 2);
            bbox.Max = bbox.Max + expand;
            bbox.Min = bbox.Min - expand;

            using (Transaction trans = new Transaction(doc, "Box Wall"))
            {
                trans.Start();
                view3d.IsSectionBoxActive = true;
                view3d.SetSectionBox(bbox);
                trans.Commit();
            }

            UIDocument uidoc = new UIDocument(doc);
            uidoc.ActiveView = view3d;
            uidoc.Selection.SetElementIds(new List<ElementId> { wall.Id });
        }

        public string GetName() => "WallDivideActionHandler";

        private void HighlightAction(Document doc)
        {
            View activeView = doc.ActiveView;
            
            // Tìm Solid Fill Pattern
            FillPatternElement solidPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            Color red = new Color(255, 0, 0); 
            
            // Thiết lập Projection (cho 3D/Mặt đứng)
            ogs.SetProjectionLineColor(red);
            ogs.SetProjectionLineWeight(8); // Nét đậm
            if (solidPattern != null)
            {
                ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                ogs.SetSurfaceForegroundPatternColor(red);
                ogs.SetSurfaceForegroundPatternVisible(true);
            }

            // Thiết lập Cut (cho Mặt bằng)
            if (solidPattern != null)
            {
                ogs.SetCutForegroundPatternId(solidPattern.Id);
                ogs.SetCutForegroundPatternColor(red);
                ogs.SetCutForegroundPatternVisible(true);
            }
            ogs.SetCutLineColor(red);
            ogs.SetCutLineWeight(8);

            using (Transaction trans = new Transaction(doc, "Highlight Walls Need Divide"))
            {
                trans.Start();
                int count = 0;
                foreach (var wallItem in ViewModel.WallItems)
                {
                    // Chỉ tô màu đỏ cho những wall thực sự cần chia (> 15 tấn)
                    if (wallItem.NeedsDivision)
                    {
                        activeView.SetElementOverrides(wallItem.WallId, ogs);
                        count++;
                    }
                }
                trans.Commit();
                ViewModel.SetStatus($"Highlighted {count} walls in Red.", true);
            }
        }

        private void DivideAction(Document doc)
        {
            try
            {
                int skipped = 0;
                int divided = 0;

                using (TransactionGroup tg = new TransactionGroup(doc, "Wall Divide Tool"))
                {
                    tg.Start();

                    // 1. Divide selected walls
                    using (Transaction trans = new Transaction(doc, "Divide Walls"))
                    {
                        trans.Start();
                        foreach (var wallItem in ViewModel.WallItems.Where(w => w.IsSelected))
                        {
                            Wall wall = doc.GetElement(wallItem.WallId) as Wall;
                            if (wall == null) continue;

                            // CHẶN chia tường nếu tường nằm trong Group
                            if (wall.GroupId != ElementId.InvalidElementId)
                            {
                                skipped++;
                                continue;
                            }

                            PerformWallSplit(doc, wall, wallItem.SelectedParts);
                            divided++;
                        }
                        trans.Commit();
                    }

                    tg.Assimilate();
                }
                
                string msg = $"Completed: {divided} walls divided.";
                if (skipped > 0) msg += $" ({skipped} grouped walls skipped for safety)";
                ViewModel.SetStatus(msg, true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Division failed: " + ex.Message);
            }
            finally
            {
                ViewModel.IsProcessing = false;
            }
        }

        private List<ElementId> PerformWallSplit(Document doc, Wall wall, int parts)
        {
            List<ElementId> newIds = new List<ElementId>();
            LocationCurve locCurve = wall.Location as LocationCurve;
            Curve curve = locCurve.Curve;

            // Capture properties
            ElementId typeId = wall.WallType.Id;
            ElementId levelId = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
            double height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double offset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            bool flipped = wall.Flipped;
            bool structural = wall.StructuralUsage != StructuralWallUsage.NonBearing;

            // Calculate points
            List<XYZ> points = new List<XYZ>();
            for (int i = 0; i <= parts; i++)
                points.Add(curve.Evaluate((double)i / parts, true));

            // Create new segments
            for (int i = 0; i < parts; i++)
            {
                Line segment = Line.CreateBound(points[i], points[i + 1]);
                Wall newWall = Wall.Create(doc, segment, typeId, levelId, height, offset, flipped, structural);
                
                // Khóa không cho tự động join để tránh lệch vị trí
                WallUtils.DisallowWallJoinAtEnd(newWall, 0);
                WallUtils.DisallowWallJoinAtEnd(newWall, 1);

                // Copy additional params
                ElementId topLevel = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
                if (topLevel != ElementId.InvalidElementId)
                {
                    newWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(topLevel);
                    double topOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();
                    newWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
                }

                newIds.Add(newWall.Id);
                
                // Copy ALL instance parameters
                CopyParameters(wall, newWall);
            }

            doc.Delete(wall.Id);
            return newIds;
        }

        private void CopyParameters(Wall source, Wall target)
        {
            foreach (Parameter param in source.Parameters)
            {
                if (param.IsReadOnly || !param.HasValue) continue;

                // Skip geometry & internal constraints
                if (param.Definition.Name == "Length" || 
                    param.Definition.Name == "Area" || 
                    param.Definition.Name == "Volume") continue;

                // Thử tìm parameter tương ứng ở target
                Parameter targetParam = target.get_Parameter(param.Definition);
                if (targetParam == null) 
                    targetParam = target.LookupParameter(param.Definition.Name);

                if (targetParam != null && !targetParam.IsReadOnly)
                {
                    try {
                        switch (param.StorageType)
                        {
                            case StorageType.Double: targetParam.Set(param.AsDouble()); break;
                            case StorageType.Integer: targetParam.Set(param.AsInteger()); break;
                            case StorageType.String: targetParam.Set(param.AsString()); break;
                            case StorageType.ElementId: targetParam.Set(param.AsElementId()); break;
                        }
                    } catch { /* Skip if set fails */ }
                }
            }
        }

        private class GroupSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Group;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        private class SelectionFilterFloor : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Floor;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        private class SelectionFilterWall : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
