using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.CreateSectionWall.ViewModels;

namespace RincoNhan.Tools.CreateSectionWall
{
    public class CreateSectionWallExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public CreateSectionWallViewModel ViewModel { get; set; }

        public CreateSectionWallExternalEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (RequestAction == "PICK_AND_CREATE")
            {
                PickAndCreateSections(uidoc, doc);
            }
        }

        private void PickAndCreateSections(UIDocument uidoc, Document doc)
        {
            try
            {
                // Let user pick multiple walls
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, new SelectionFilterWall(), "Select Walls to create section views");
                List<Wall> walls = refs.Select(r => doc.GetElement(r) as Wall).Where(w => w != null).ToList();

                if (!walls.Any())
                {
                    ViewModel.StatusMessage = "No walls selected.";
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                // Find Section ViewFamilyType RINCO_ELE_WALL
                ViewFamilyType sectionVft = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section && x.Name == "RINCO_ELE_WALL");

                if (sectionVft == null)
                {
                    // Fallback
                    sectionVft = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);
                }

                if (sectionVft == null)
                {
                    TaskDialog.Show("Error", "No Section ViewFamilyType found in the document.");
                    ViewModel.RequestShowWindow?.Invoke();
                    return;
                }

                // Fetch view templates
                View templateEW = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.IsTemplate && v.Name == "RINCO - ELV - EW");

                View templateNS = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .FirstOrDefault(v => v.IsTemplate && v.Name == "RINCO - ELV - NS");

                int createdCount = 0;

                // Pre-fetch existing view names to ensure we create unique names
                HashSet<string> existingNames = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Select(v => v.Name)
                    .ToHashSet();

                int currentNumber = 1;

                using (Transaction trans = new Transaction(doc, "Create Wall Sections"))
                {
                    trans.Start();

                    foreach (Wall wall in walls)
                    {
                        LocationCurve locCurve = wall.Location as LocationCurve;
                        if (locCurve == null) continue;

                        Curve curve = locCurve.Curve;
                        if (curve == null || !(curve is Line)) continue;

                        XYZ start = curve.GetEndPoint(0);
                        XYZ end = curve.GetEndPoint(1);
                        double length = start.DistanceTo(end);

                        XYZ wallDir = (end - start).Normalize();
                        if (ViewModel.FlipDirection)
                        {
                            wallDir = -wallDir;
                        }

                        XYZ up = XYZ.BasisZ;
                        XYZ right = wallDir.CrossProduct(up).Normalize();

                        XYZ mid = (start + end) / 2.0;

                        Transform t = Transform.Identity;
                        t.Origin = mid;
                        t.BasisX = wallDir;
                        t.BasisY = up;
                        t.BasisZ = right;

#if REVIT2021_OR_GREATER
                        double heightFt = UnitUtils.ConvertToInternalUnits(ViewModel.Height, UnitTypeId.Millimeters);
                        double offsetFt = UnitUtils.ConvertToInternalUnits(ViewModel.Offset, UnitTypeId.Millimeters);
#else
                        double heightFt = UnitUtils.ConvertToInternalUnits(ViewModel.Height, DisplayUnitType.DUT_MILLIMETERS);
                        double offsetFt = UnitUtils.ConvertToInternalUnits(ViewModel.Offset, DisplayUnitType.DUT_MILLIMETERS);
#endif

                        double thickness = wall.Width;
                        double totalDepth = thickness + offsetFt;

                        BoundingBoxXYZ bb = new BoundingBoxXYZ();
                        bb.Transform = t;

                        // Min and Max in local coordinates
                        bb.Min = new XYZ(-length / 2.0, 0, -totalDepth / 2.0);
                        bb.Max = new XYZ(length / 2.0, heightFt, totalDepth / 2.0);

                        ViewSection section = ViewSection.CreateSection(doc, sectionVft.Id, bb);

                        // Assign View Template based on wall direction
                        if (Math.Abs(wallDir.Y) >= Math.Abs(wallDir.X))
                        {
                            // Wall is primarily along Y axis
                            if (templateEW != null)
                            {
                                section.ViewTemplateId = templateEW.Id;
                            }
                        }
                        else
                        {
                            // Wall is primarily along X axis
                            if (templateNS != null)
                            {
                                section.ViewTemplateId = templateNS.Id;
                            }
                        }

                        // Auto-increment naming
                        while (true)
                        {
                            string candidateName = $"ELEVATION {ViewModel.SelectedPrefix}{currentNumber}";
                            if (!existingNames.Contains(candidateName))
                            {
                                try
                                {
                                    section.Name = candidateName;
                                    existingNames.Add(candidateName);
                                    currentNumber++;
                                    break;
                                }
                                catch
                                {
                                    // If Revit still rejects it for some reason, just try the next number
                                }
                            }
                            currentNumber++;
                        }

                        createdCount++;
                    }

                    trans.Commit();
                }

                ViewModel.StatusMessage = $"Created {createdCount} section view(s).";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.StatusMessage = "Operation cancelled.";
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Failed to create sections: " + ex.Message);
                ViewModel.StatusMessage = "Error occurred.";
            }
            finally
            {
                // Show window again after picking
                ViewModel.RequestShowWindow?.Invoke();
            }
        }

        public string GetName() => "CreateSectionWallEventHandler";

        private class SelectionFilterWall : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Wall;
            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
