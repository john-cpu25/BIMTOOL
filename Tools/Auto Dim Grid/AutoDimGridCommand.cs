using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.Auto_Dim_Grid.UI;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

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

                var gridHandler = new AutoDimGridEventHandler();
                ExternalEvent gridEvent = ExternalEvent.Create(gridHandler);

                var wallHandler = new AutoDimWallEventHandler();
                ExternalEvent wallEvent = ExternalEvent.Create(wallHandler);

                var multiWallsHandler = new AutoDimMultiWallsEventHandler();
                ExternalEvent multiWallsEvent = ExternalEvent.Create(multiWallsHandler);

                var viewModel = new AutoDimGridViewModel(doc);
                gridHandler.ViewModel = viewModel;
                wallHandler.ViewModel = viewModel;
                multiWallsHandler.ViewModel = viewModel;

                var window = new AutoDimGridWindow(viewModel, gridEvent, wallEvent, multiWallsEvent);

                // Set owner to Revit main window
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                window.Show();

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
