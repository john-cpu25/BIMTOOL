using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ElevationView.UI;
using RincoNhan.Tools.ElevationView.ViewModels;

namespace RincoNhan.Tools.ElevationView
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            if (!(activeView is ViewSection))
            {
                message = "Please run this tool in a Section or Elevation view.";
                return Result.Failed;
            }

            // Initialize Handler and ViewModel
            var handler = new ElevationViewExternalEventHandler();
            var viewModel = new ElevationViewViewModel(handler, activeView);
            
            // Show UI
            var window = new ElevationViewWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }
}
