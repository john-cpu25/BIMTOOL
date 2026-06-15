using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CreateViewSheet.UI;
using RincoNhan.Tools.CreateViewSheet.ViewModels;

namespace RincoNhan.Tools.CreateViewSheet
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Create View & Sheet", "Please open a document first.");
                    return Result.Cancelled;
                }

                Document doc = uidoc.Document;
                if (doc == null)
                {
                    TaskDialog.Show("Create View & Sheet", "No active document found.");
                    return Result.Cancelled;
                }

                var handler = new CreateViewSheetHandler();
                var viewModel = new CreateViewSheetViewModel(handler, doc);

                var window = new CreateViewSheetWindow(viewModel);
                window.Show(); // Modeless

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message + "\n" + ex.StackTrace;
                return Result.Failed;
            }
        }
    }
}
