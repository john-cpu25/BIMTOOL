using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.QueryElement
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        private static QueryElementWindow _window;
        private static QueryElementEventHandler _handler;
        private static ExternalEvent _exEvent;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                _handler = new QueryElementEventHandler();
                _exEvent = ExternalEvent.Create(_handler);

                _window = new QueryElementWindow(doc, _exEvent, _handler);
                
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(_window);
                helper.Owner = uiapp.MainWindowHandle;
                
                _window.Closed += (s, e) => { _window = null; };
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
