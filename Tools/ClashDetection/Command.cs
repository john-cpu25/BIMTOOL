using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ClashDetection.UI;
using RincoNhan.Tools.ClashDetection.ViewModels;

namespace RincoNhan.Tools.ClashDetection
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            // Initialize Handler and ViewModel
            var handler = new ClashDetectionExternalEventHandler();
            var viewModel = new ClashDetectionViewModel(handler, doc);
            
            // Show UI
            var window = new ClashDetectionWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }
}
