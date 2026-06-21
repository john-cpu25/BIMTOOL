using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    [Transaction(TransactionMode.Manual)]
    public class AutoDimGridCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View currentView = doc.ActiveView;

            if (currentView.ViewType != ViewType.FloorPlan &&
                currentView.ViewType != ViewType.CeilingPlan &&
                currentView.ViewType != ViewType.EngineeringPlan &&
                currentView.ViewType != ViewType.Section &&
                currentView.ViewType != ViewType.Elevation)
            {
                message = "Vui lòng mở mặt bằng, mặt đứng hoặc mặt cắt để chạy lệnh này.";
                return Result.Failed;
            }

            try
            {
                XYZ pickPoint = uidoc.Selection.PickPoint("Chọn vị trí đặt đường Dim");

                var grids = new FilteredElementCollector(doc, currentView.Id)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .Where(g => g.Curve is Line)
                    .ToList();

                if (!grids.Any())
                {
                    TaskDialog.Show("Kết quả", "Không có trục nào trong View hiện tại.");
                    return Result.Succeeded;
                }

                using (Transaction tx = new Transaction(doc, "Auto Dim Grid"))
                {
                    tx.Start();
                    AutoDimGridLogic.CreateDimensions(doc, currentView, grids, pickPoint);
                    tx.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
