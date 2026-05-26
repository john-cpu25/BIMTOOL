using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CreateLevel.UI;
using RincoNhan.Tools.CreateLevel.ViewModels;

namespace RincoNhan.Tools.CreateLevel
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var handler = new CreateLevelHandler();
                var viewModel = new CreateLevelViewModel(handler, commandData.Application.ActiveUIDocument.Document);

                var window = new CreateLevelWindow(viewModel);
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
