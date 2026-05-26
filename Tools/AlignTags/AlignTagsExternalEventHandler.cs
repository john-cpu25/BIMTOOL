using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.AlignTags
{
    public class AlignTagsExternalEventHandler : IExternalEventHandler
    {
        public string Action { get; set; }
        public AlignTagsViewModel ViewModel { get; set; }
        public ElementId ReferenceTagId { get; set; }
        public List<ElementId> TargetTagIds { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            try
            {
                if (Action == "PickReference")
                {
                    Reference refObj = uiDoc.Selection.PickObject(ObjectType.Element, new TagSelectionFilter(), "Select Reference Tag");
                    if (refObj != null)
                    {
                        ViewModel.UpdateSelection(refObj.ElementId, null);
                    }
                }
                else if (Action == "PickTargets")
                {
                    IList<Reference> refs = uiDoc.Selection.PickObjects(ObjectType.Element, new TagSelectionFilter(), "Select Target Tags (Window select or multiple pick)");
                    if (refs != null && refs.Any())
                    {
                        ViewModel.UpdateSelection(null, refs.Select(r => r.ElementId).ToList());
                    }
                }
                else if (Action == "AlignHorizontal" || Action == "AlignVertical")
                {
                    AlignTags(doc, view, Action == "AlignHorizontal");
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled selection, ignore
                if (ViewModel != null) ViewModel.StatusMessage = "Operation cancelled by user.";
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Align Tags Error", ex.Message);
            }
        }

        private void AlignTags(Document doc, View view, bool isHorizontal)
        {
            if (ReferenceTagId == null || TargetTagIds == null || !TargetTagIds.Any()) return;

            IndependentTag refTag = doc.GetElement(ReferenceTagId) as IndependentTag;
            if (refTag == null) return;

            XYZ refPoint = refTag.TagHeadPosition;
            XYZ viewRight = view.RightDirection;
            XYZ viewUp = view.UpDirection;

            using (Transaction trans = new Transaction(doc, isHorizontal ? "Align Tags Horizontal" : "Align Tags Vertical"))
            {
                trans.Start();

                foreach (ElementId id in TargetTagIds)
                {
                    if (id == ReferenceTagId) continue;

                    IndependentTag targetTag = doc.GetElement(id) as IndependentTag;
                    if (targetTag == null) continue;

                    XYZ targetPoint = targetTag.TagHeadPosition;
                    XYZ displacement = XYZ.Zero;

                    if (isHorizontal)
                    {
                        // Align Horizontal: Match the 'Vertical' coordinate (Y in Plan, Z in Section)
                        // Project the vector (ref - target) onto the view's Up direction
                        double dist = (refPoint - targetPoint).DotProduct(viewUp);
                        displacement = viewUp * dist;
                    }
                    else
                    {
                        // Align Vertical: Match the 'Horizontal' coordinate (X in Plan, X in Section)
                        // Project the vector (ref - target) onto the view's Right direction
                        double dist = (refPoint - targetPoint).DotProduct(viewRight);
                        displacement = viewRight * dist;
                    }

                    if (!displacement.IsAlmostEqualTo(XYZ.Zero))
                    {
                        targetTag.TagHeadPosition = targetPoint + displacement;
                    }
                }

                trans.Commit();
            }

            if (ViewModel != null) ViewModel.StatusMessage = "Alignment complete.";
        }

        public string GetName() => "AlignTagsEventHandler";
    }

    public class TagSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is IndependentTag || elem is TextNote;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
