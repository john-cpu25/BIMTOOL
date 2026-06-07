using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.InterlockingWall.ViewModels;

namespace RincoNhan.Tools.InterlockingWall
{
    public class InterlockingWallExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public InterlockingWallViewModel ViewModel { get; set; }

        public InterlockingWallExternalEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (RequestAction == "PICK_TWO_WALLS")
            {
                PickTwoWallsAction(uidoc);
            }
            else if (RequestAction == "JOIN_WALLS")
            {
                JoinWallsAction(doc);
            }
            else if (RequestAction == "PICK_WALL_TO_SPLIT")
            {
                PickWallToSplitAction(uidoc);
            }
            else if (RequestAction == "SPLIT_WALL")
            {
                SplitWallAction(doc);
            }
            else if (RequestAction == "PICK_GROUP")
            {
                PickGroupAction(uidoc);
            }
            else if (RequestAction == "EXECUTE_GROUP")
            {
                ExecuteGroupAction(doc);
            }
        }

        private void PickTwoWallsAction(UIDocument uidoc)
        {
            try
            {
                IList<Reference> refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select exactly 2 Walls to join");

                List<Wall> walls = refs
                    .Select(r => uidoc.Document.GetElement(r) as Wall)
                    .Where(w => w != null)
                    .ToList();

                if (walls.Count >= 2)
                {
                    ViewModel.LoadTwoWalls(walls[0], walls[1]);
                }
                else if (walls.Count == 1)
                {
                    ViewModel.SetStatus("Please select at least 2 walls.", false);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void JoinWallsAction(Document doc)
        {
            if (ViewModel.Wall1Id == null || ViewModel.Wall2Id == null) return;

            try
            {
                Wall wall1 = doc.GetElement(ViewModel.Wall1Id) as Wall;
                Wall wall2 = doc.GetElement(ViewModel.Wall2Id) as Wall;

                if (wall1 == null || wall2 == null)
                {
                    TaskDialog.Show("Merge Walls", "One or both walls no longer exist in the document.");
                    return;
                }

                // Check walls are same type
                if (wall1.WallType.Id != wall2.WallType.Id)
                {
                    TaskDialog.Show("Merge Walls", "Cannot merge: both walls must be the same Wall Type.\n\n"
                        + "Wall 1: " + wall1.Name + "\nWall 2: " + wall2.Name);
                    return;
                }

                // Check walls are not in groups
                if (wall1.GroupId != ElementId.InvalidElementId || wall2.GroupId != ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Merge Walls", "Cannot merge walls that belong to a Group.");
                    return;
                }

                // Determine bottom wall and top wall by comparing Base Constraint elevation
                ElementId base1Id = wall1.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
                ElementId base2Id = wall2.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();

                Level level1 = doc.GetElement(base1Id) as Level;
                Level level2 = doc.GetElement(base2Id) as Level;

                if (level1 == null || level2 == null)
                {
                    TaskDialog.Show("Merge Walls", "Cannot determine wall levels.");
                    return;
                }

                double elev1 = level1.Elevation + wall1.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
                double elev2 = level2.Elevation + wall2.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();

                Wall bottomWall, topWall;
                if (elev1 <= elev2)
                {
                    bottomWall = wall1;
                    topWall = wall2;
                }
                else
                {
                    bottomWall = wall2;
                    topWall = wall1;
                }

                // Capture Top Constraint and Top Offset from the TOP wall
                ElementId topConstraint = topWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
                double topOffset = topWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();

                using (Transaction trans = new Transaction(doc, "Rinco - Merge Walls"))
                {
                    trans.Start();

                    // Set bottom wall's Top Constraint = top wall's Top Constraint
                    if (topConstraint != ElementId.InvalidElementId)
                    {
                        bottomWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(topConstraint);
                        bottomWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
                    }

                    // Delete the top wall
                    doc.Delete(topWall.Id);

                    trans.Commit();
                }

                // Show success notification
                Level topLevel = doc.GetElement(topConstraint) as Level;
                string topLevelName = topLevel != null ? topLevel.Name : "N/A";
                Level baseLevelObj = doc.GetElement(bottomWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()) as Level;
                string baseLevelName = baseLevelObj != null ? baseLevelObj.Name : "N/A";

                TaskDialog.Show("Merge Walls - Success",
                    "Successfully merged 2 walls into 1.\n\n"
                    + "Result: Base=" + baseLevelName + " → Top=" + topLevelName + "\n"
                    + "Top wall deleted. Bottom wall extended.");

                // Reset selection state
                ViewModel.HasTwoWalls = false;
                ViewModel.SetStatus("Merged 2 walls into 1 successfully.", true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Merge Walls - Error",
                    "Merge failed:\n\n" + ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private void PickWallToSplitAction(UIDocument uidoc)
        {
            try
            {
                Reference refWall = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new WallSelectionFilter(),
                    "Select a Wall to split");

                Wall wall = uidoc.Document.GetElement(refWall) as Wall;
                if (wall != null)
                {
                    ViewModel.LoadWallToSplit(wall);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void SplitWallAction(Document doc)
        {
            if (ViewModel.SplitWallId == null) return;

            try
            {
                Wall wall = doc.GetElement(ViewModel.SplitWallId) as Wall;
                if (wall == null)
                {
                    TaskDialog.Show("Split Wall", "Wall no longer exists in the document.");
                    return;
                }

                // Check if wall is in a group
                if (wall.GroupId != ElementId.InvalidElementId)
                {
                    TaskDialog.Show("Split Wall", "Cannot split a wall that belongs to a Group.");
                    return;
                }

                // Get split ratios from ViewModel (cumulative positions)
                double[] splitRatios = ViewModel.GetSplitRatios();

                using (TransactionGroup tg = new TransactionGroup(doc, "Rinco - Split Wall"))
                {
                    tg.Start();

                    using (Transaction trans = new Transaction(doc, "Split Wall into " + (splitRatios.Length + 1) + " segments"))
                    {
                        trans.Start();

                        List<ElementId> newIds = PerformWallSplit(doc, wall, splitRatios);

                        trans.Commit();

                        TaskDialog.Show("Split Wall - Success",
                            "Successfully split wall into " + newIds.Count + " segments.\n\n"
                            + "DisallowJoin applied at all segment ends.");
                    }

                    tg.Assimilate();
                }

                ViewModel.HasWallToSplit = false;
                ViewModel.SetStatus("Wall split into " + (splitRatios.Length + 1) + " segments.", true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Split Wall - Error",
                    "Split failed:\n\n" + ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        /// <summary>
        /// Split a wall into N segments at the given cumulative ratios.
        /// e.g. ratios = [0.333, 0.667] creates 3 segments.
        /// </summary>
        private List<ElementId> PerformWallSplit(Document doc, Wall wall, double[] splitRatios)
        {
            List<ElementId> newIds = new List<ElementId>();
            LocationCurve locCurve = wall.Location as LocationCurve;
            Curve curve = locCurve.Curve;

            // Capture properties BEFORE delete
            ElementId typeId = wall.WallType.Id;
            ElementId levelId = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId();
            double height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double offset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            bool flipped = wall.Flipped;
            bool structural = wall.StructuralUsage != Autodesk.Revit.DB.Structure.StructuralWallUsage.NonBearing;
            ElementId topLevel = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
            double topOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();

            // Calculate all split points along the curve
            List<XYZ> points = new List<XYZ>();
            points.Add(curve.Evaluate(0, true)); // start

            foreach (double ratio in splitRatios)
            {
                double clampedRatio = Math.Max(0.01, Math.Min(0.99, ratio));
                points.Add(curve.Evaluate(clampedRatio, true));
            }

            points.Add(curve.Evaluate(1, true)); // end

            // Delete original wall
            doc.Delete(wall.Id);

            // Create N new wall segments
            for (int i = 0; i < points.Count - 1; i++)
            {
                Line segLine = Line.CreateBound(points[i], points[i + 1]);
                Wall newWall = Wall.Create(doc, segLine, typeId, levelId, height, offset, flipped, structural);

                // DisallowJoin at both ends
                WallUtils.DisallowWallJoinAtEnd(newWall, 0);
                WallUtils.DisallowWallJoinAtEnd(newWall, 1);

                // Apply top constraint
                if (topLevel != ElementId.InvalidElementId)
                {
                    newWall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(topLevel);
                    newWall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
                }

                newIds.Add(newWall.Id);
            }

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

                Parameter targetParam = target.get_Parameter(param.Definition);
                if (targetParam == null)
                    targetParam = target.LookupParameter(param.Definition.Name);

                if (targetParam != null && !targetParam.IsReadOnly)
                {
                    try
                    {
                        switch (param.StorageType)
                        {
                            case StorageType.Double: targetParam.Set(param.AsDouble()); break;
                            case StorageType.Integer: targetParam.Set(param.AsInteger()); break;
                            case StorageType.String: targetParam.Set(param.AsString()); break;
                            case StorageType.ElementId: targetParam.Set(param.AsElementId()); break;
                        }
                    }
                    catch { /* Skip if set fails */ }
                }
            }
        }

        public string GetName() => "InterlockingWallActionHandler";

        private void PickGroupAction(UIDocument uidoc)
        {
            try
            {
                Reference refGroup = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new GroupSelectionFilter(),
                    "Select a Group containing 4 walls");

                Group group = uidoc.Document.GetElement(refGroup) as Group;
                if (group == null)
                {
                    TaskDialog.Show("Group Wall", "Selected element is not a Group.");
                    return;
                }

                // Get member elements
                IList<ElementId> memberIds = group.GetMemberIds();
                List<Wall> walls = memberIds
                    .Select(id => uidoc.Document.GetElement(id))
                    .OfType<Wall>()
                    .ToList();

                if (walls.Count != 4)
                {
                    TaskDialog.Show("Group Wall",
                        $"Group must contain exactly 4 walls.\nFound: {walls.Count} walls and {memberIds.Count - walls.Count} other elements.");
                    return;
                }

                // Classify walls by orientation
                List<Wall> horizontal = new List<Wall>();
                List<Wall> vertical = new List<Wall>();

                foreach (var wall in walls)
                {
                    LocationCurve loc = wall.Location as LocationCurve;
                    if (loc == null) continue;
                    XYZ dir = (loc.Curve.GetEndPoint(1) - loc.Curve.GetEndPoint(0)).Normalize();
                    if (Math.Abs(dir.X) >= Math.Abs(dir.Y))
                        horizontal.Add(wall);
                    else
                        vertical.Add(wall);
                }

                if (horizontal.Count != 2 || vertical.Count != 2)
                {
                    TaskDialog.Show("Group Wall",
                        $"Expected 2 horizontal + 2 vertical walls.\nFound: {horizontal.Count} horizontal, {vertical.Count} vertical.");
                    return;
                }

                // Sort: vertical by X (left=Wall1, right=Wall2)
                vertical.Sort((a, b) =>
                {
                    double ax = ((LocationCurve)a.Location).Curve.Evaluate(0.5, true).X;
                    double bx = ((LocationCurve)b.Location).Curve.Evaluate(0.5, true).X;
                    return ax.CompareTo(bx);
                });

                // Sort: horizontal by Y (bottom=Wall3, top=Wall4)
                horizontal.Sort((a, b) =>
                {
                    double ay = ((LocationCurve)a.Location).Curve.Evaluate(0.5, true).Y;
                    double by = ((LocationCurve)b.Location).Curve.Evaluate(0.5, true).Y;
                    return ay.CompareTo(by);
                });

                Wall w1 = vertical[0];    // Left
                Wall w2 = vertical[1];    // Right
                Wall w3 = horizontal[0];  // Bottom
                Wall w4 = horizontal[1];  // Top

                ViewModel.LoadGroupWalls(group, w1, w2, w3, w4);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        private void ExecuteGroupAction(Document doc)
        {
            if (ViewModel.SelectedGroupId == null) return;

            try
            {
                Group group = doc.GetElement(ViewModel.SelectedGroupId) as Group;
                if (group == null)
                {
                    TaskDialog.Show("Group Wall", "Group no longer exists in the document.");
                    return;
                }

                using (TransactionGroup tg = new TransactionGroup(doc, "Rinco - Group Wall Split"))
                {
                    tg.Start();

                    List<ElementId> allNewWallIds = new List<ElementId>();

                    using (Transaction t1 = new Transaction(doc, "Ungroup and Split"))
                    {
                        t1.Start();

                        // Ungroup to get individual walls
                        ICollection<ElementId> memberIds = group.UngroupMembers();

                        // Identify walls by stored IDs
                        Wall wall1 = doc.GetElement(ViewModel.GroupWall1Id) as Wall;
                        Wall wall2 = doc.GetElement(ViewModel.GroupWall2Id) as Wall;
                        Wall wall3 = doc.GetElement(ViewModel.GroupWall3Id) as Wall;
                        Wall wall4 = doc.GetElement(ViewModel.GroupWall4Id) as Wall;

                        if (wall1 == null || wall2 == null || wall3 == null || wall4 == null)
                        {
                            TaskDialog.Show("Group Wall", "One or more walls no longer exist after ungrouping.");
                            t1.RollBack();
                            tg.RollBack();
                            return;
                        }

                        // Keep Wall 1 and Wall 2
                        allNewWallIds.Add(wall1.Id);
                        allNewWallIds.Add(wall2.Id);

                        // Split Wall 3
                        double[] wall3Ratios = ViewModel.GetWall3SplitRatios();
                        List<ElementId> wall3Segments = PerformWallSplit(doc, wall3, wall3Ratios);
                        allNewWallIds.AddRange(wall3Segments);

                        // Split Wall 4
                        double[] wall4Ratios = ViewModel.GetWall4SplitRatios();
                        List<ElementId> wall4Segments = PerformWallSplit(doc, wall4, wall4Ratios);
                        allNewWallIds.AddRange(wall4Segments);

                        t1.Commit();
                    }

                    // Create new group from all walls
                    using (Transaction t2 = new Transaction(doc, "Create New Group"))
                    {
                        t2.Start();

                        Group newGroup = doc.Create.NewGroup(allNewWallIds);

                        t2.Commit();

                        int totalWalls = allNewWallIds.Count;
                        TaskDialog.Show("Group Wall - Success",
                            $"Successfully created new group with {totalWalls} walls.\n\n"
                            + $"Wall 1 (Left): kept\n"
                            + $"Wall 2 (Right): kept\n"
                            + $"Wall 3 (Bottom): split into {ViewModel.Wall3PanelCount} segments\n"
                            + $"Wall 4 (Top): split into {ViewModel.Wall4PanelCount} segments");
                    }

                    tg.Assimilate();
                }

                ViewModel.HasGroupSelected = false;
                ViewModel.SetStatus($"Group wall split completed. Created {ViewModel.Wall3PanelCount + ViewModel.Wall4PanelCount + 2} walls.", true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Group Wall - Error",
                    "Operation failed:\n\n" + ex.Message + "\n\n" + ex.StackTrace);
            }
        }

        private class WallSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        private class GroupSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Group;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
