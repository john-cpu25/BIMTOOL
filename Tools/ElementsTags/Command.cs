using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ElementsTags.UI;

namespace RincoNhan.Tools.ElementsTags
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uidoc = uiApp.ActiveUIDocument;

                ElementsTagsWindow window = new ElementsTagsWindow(uidoc);
                
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiApp.MainWindowHandle;

                window.Show();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", "An error occurred:\n\n" + ex.ToString());
                return Result.Failed;
            }
        }
    }
}
