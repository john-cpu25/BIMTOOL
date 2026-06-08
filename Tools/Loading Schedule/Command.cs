using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingSchedule.UI;

namespace RincoNhan.Tools.LoadingSchedule
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        private static LoadingScheduleWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc.Document;
                View activeView = uidoc.ActiveView;

                if (activeView == null)
                {
                    TaskDialog.Show("Loading Schedule", "Please open a view first.");
                    return Result.Cancelled;
                }

                if (activeView.ViewType == ViewType.Legend)
                {
                    TaskDialog.Show("Loading Schedule",
                        "Please open the model view that contains filled regions (not a Legend view).\n" +
                        "The tool will read hatch types from the current view and create the legend in a Legend view.");
                    return Result.Cancelled;
                }

                // Singleton: if window already open, bring to front
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                _window = new LoadingScheduleWindow(doc, activeView);
                _window.Closed += (s, e) => _window = null;
                _window.Show();

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
