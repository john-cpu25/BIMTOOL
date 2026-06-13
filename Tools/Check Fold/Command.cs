using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CheckFold.UI;

namespace RincoNhan.Tools.CheckFold
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc.ActiveView == null)
                {
                    TaskDialog.Show("Error", "Please open a view first.");
                    return Result.Cancelled;
                }

                // Create handler for ExternalEvent (thread-safe Revit API access)
                var handler = new CheckFoldHandler();

                // Modeless window
                CheckFoldWindow window = new CheckFoldWindow(handler);

                System.Windows.Interop.WindowInteropHelper helper =
                    new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

                window.Show(); // Modeless: Show() instead of ShowDialog()

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
