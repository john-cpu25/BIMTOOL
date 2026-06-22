using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public class AutoDimWallEventHandler : IExternalEventHandler
    {
        public AutoDimGridViewModel ViewModel { get; set; }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return;
            Document doc = uidoc.Document;

            try
            {
                // Chọn một bức tường
                Reference wallRef = uidoc.Selection.PickObject(ObjectType.Element, new WallSelectionFilter(), "Chọn một bức tường để đo kích thước");
                Wall wall = doc.GetElement(wallRef) as Wall;
                if (wall == null) return;

                // Chọn một điểm để xác định vị trí đặt Dim
                XYZ pickPoint = uidoc.Selection.PickPoint("Chọn một điểm để xác định hướng đặt Dim (vuông góc với tường)");

                using (Transaction tx = new Transaction(doc, "Auto Dim Wall"))
                {
                    tx.Start();
                    AutoDimWallLogic.CreateDimensions(doc, doc.ActiveView, wall, pickPoint, ViewModel);
                    tx.Commit();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng nhấn ESC để hủy
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Auto Dim Wall Event Handler";
        }
    }

    public class WallSelectionFilter : ISelectionFilter
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
