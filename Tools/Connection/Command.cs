using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Connection.UI;

namespace RincoNhan.Tools.Connection
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
                Document doc = uidoc.Document;

                // Create handler and event for external execution
                var handler = new ConnectionEventHandler();
                var externalEvent = ExternalEvent.Create(handler);

                // Open the tool window
                ConnectionWindow window = new ConnectionWindow(uidoc, externalEvent, handler);
                window.Show();

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
