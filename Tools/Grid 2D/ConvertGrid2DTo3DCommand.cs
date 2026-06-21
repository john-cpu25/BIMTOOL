using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.Grid_2D
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertGrid2DTo3DCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View currentView = doc.ActiveView;

            // Kiểm tra xem view hiện tại có hỗ trợ hiển thị grid 2D/3D không
            if (currentView.ViewType != ViewType.FloorPlan &&
                currentView.ViewType != ViewType.CeilingPlan &&
                currentView.ViewType != ViewType.Elevation &&
                currentView.ViewType != ViewType.Section &&
                currentView.ViewType != ViewType.EngineeringPlan)
            {
                message = "Công cụ này chỉ hoạt động trên mặt bằng, mặt đứng, mặt cắt.";
                return Result.Failed;
            }

            try
            {
                // Lấy tất cả các lưới trục hiển thị trong view hiện tại
                var grids = new FilteredElementCollector(doc, currentView.Id)
                    .OfClass(typeof(Grid))
                    .Cast<Grid>()
                    .ToList();

                if (!grids.Any())
                {
                    TaskDialog.Show("Kết quả", "Không tìm thấy lưới trục nào trong view hiện tại.");
                    return Result.Succeeded;
                }

                int count = 0;

                using (Transaction tx = new Transaction(doc, "Convert Grids to 3D"))
                {
                    tx.Start();

                    foreach (var grid in grids)
                    {
                        bool changed = false;

                        try
                        {
                            // Chuyển đổi đầu 0 sang 3D (Model)
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End0, currentView) == DatumExtentType.ViewSpecific)
                            {
                                grid.SetDatumExtentType(DatumEnds.End0, currentView, DatumExtentType.Model);
                                changed = true;
                            }

                            // Chuyển đổi đầu 1 sang 3D (Model)
                            if (grid.GetDatumExtentTypeInView(DatumEnds.End1, currentView) == DatumExtentType.ViewSpecific)
                            {
                                grid.SetDatumExtentType(DatumEnds.End1, currentView, DatumExtentType.Model);
                                changed = true;
                            }
                        }
                        catch
                        {
                            // Bỏ qua nếu không thể chuyển đổi
                        }

                        if (changed)
                        {
                            count++;
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Kết quả", $"Đã chuyển {count} lưới trục sang 3D trong view hiện tại.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
