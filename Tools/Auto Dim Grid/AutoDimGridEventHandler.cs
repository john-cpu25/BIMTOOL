using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public class AutoDimGridEventHandler : IExternalEventHandler
    {
        public AutoDimGridViewModel ViewModel { get; set; }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return;
            Document doc = uidoc.Document;

            try
            {
                // Lấy các Grid đang được chọn (trước khi pick point)
                List<Grid> grids = new List<Grid>();
                var selectedIds = uidoc.Selection.GetElementIds();

                if (selectedIds.Count > 0)
                {
                    foreach (var id in selectedIds)
                    {
                        if (doc.GetElement(id) is Grid grid)
                        {
                            grids.Add(grid);
                        }
                    }
                }
                else
                {
                    // Nếu chưa chọn, tự động lấy các Grid hiển thị trên View
                    grids = new FilteredElementCollector(doc, doc.ActiveView.Id)
                        .OfClass(typeof(Grid))
                        .Cast<Grid>()
                        .Where(g => g.Curve is Line)
                        .ToList();
                }

                if (grids.Count < 2)
                {
                    TaskDialog.Show("Error", "Cần có ít nhất 2 Grid để thực hiện dim.");
                    return;
                }

                // Chờ người dùng PickPoint
                XYZ pickPoint = uidoc.Selection.PickPoint("Chọn một điểm để xác định vị trí đặt Dim");

                using (Transaction tx = new Transaction(doc, "Auto Dim Grid"))
                {
                    tx.Start();
                    AutoDimGridLogic.CreateDimensions(doc, doc.ActiveView, grids, pickPoint, ViewModel);
                    tx.Commit();
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng nhấn ESC để hủy pick point
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "Auto Dim Grid Event Handler";
        }
    }
}
