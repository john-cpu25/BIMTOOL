using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CreateSectionWall.UI;
using RincoNhan.Tools.CreateSectionWall.ViewModels;

namespace RincoNhan.Tools.CreateSectionWall
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Initialize Handler and ViewModel
            var handler = new CreateSectionWallExternalEventHandler();
            var viewModel = new CreateSectionWallViewModel(handler);
            
            // Show UI
            var window = new CreateSectionWallWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }
}
