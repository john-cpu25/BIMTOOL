using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.SmartLinkCad
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SmartLinkCadCommand : IExternalCommand
    {
        private static SmartLinkCadWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                Document doc = uiApp.ActiveUIDocument.Document;

                // If window exists and is still open, just bring it to front
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                // Create handler and window
                var handler = new SmartLinkCadEventHandler();
                _window = new SmartLinkCadWindow(doc, handler);
                _window.Closed += (s, e) => _window = null;
                _window.Show(); // Modeless

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("RincoNhan - Error", $"An error occurred:\n{ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
