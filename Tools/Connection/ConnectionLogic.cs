using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.Connection
{
    public static class ConnectionLogic
    {
        public static void CreateBeamToBeamConnection(UIDocument uidoc, ElementId connectionTypeId)
        {
            Document doc = uidoc.Document;

            try
            {
                // 1. Select Main Beam
                Reference mainBeamRef = uidoc.Selection.PickObject(ObjectType.Element, new StructuralFramingFilter(), "Select Main Beam (Dầm chính)");
                if (mainBeamRef == null) return;

                // 2. Select Secondary Beams
                IList<Reference> secondaryBeamRefs = uidoc.Selection.PickObjects(ObjectType.Element, new StructuralFramingFilter(), "Select Secondary Beam(s) (Dầm phụ). Press ESC to finish.");
                if (secondaryBeamRefs == null || secondaryBeamRefs.Count == 0) return;

                using (Transaction t = new Transaction(doc, "Create Beam to Beam Connection"))
                {
                    t.Start();

                    foreach (var secRef in secondaryBeamRefs)
                    {
                        List<ElementId> connectionElements = new List<ElementId>
                        {
                            mainBeamRef.ElementId,
                            secRef.ElementId
                        };

                        try
                        {
                            StructuralConnectionHandler.Create(doc, connectionElements, connectionTypeId);
                        }
                        catch (Exception ex)
                        {
                            // In case this specific connection fails, we can log it or show a message.
                            // But usually we just let it skip to the next one if it fails.
                            TaskDialog.Show("Error", $"Cannot create connection for beam {secRef.ElementId}:\n{ex.Message}");
                        }
                    }

                    t.Commit();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed ESC, do nothing
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }
    }

    public class StructuralFramingFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category != null && elem.Category.Id == new ElementId((int)BuiltInCategory.OST_StructuralFraming))
            {
                return true;
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
