using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Filter.UI;

namespace RincoNhan.Tools.Filter
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                FilterWindow window = new FilterWindow(uiApp.ActiveUIDocument);
                
                // GÃ¡n MainWindow cá»§a Revit lÃ m Owner cá»§a UI nÃ y (TrÃ¡nh lá»—i liÃªn Ä‘á»›i / crash)
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = commandData.Application.MainWindowHandle;

                window.Show();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Lá»—i Add-in", "CÃ³ lá»—i xáº£y ra:\n\n" + ex.ToString());
                return Result.Failed;
            }
        }
    }
}
