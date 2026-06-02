using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.LoadingHatch.UI;

namespace RincoNhan.Tools.LoadingHatch
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
                
                // Only allow running in a view
                if (uidoc.ActiveView == null)
                {
                    TaskDialog.Show("Error", "Please open a view first.");
                    return Result.Cancelled;
                }

                LoadingHatchWindow window = new LoadingHatchWindow(doc, uidoc.ActiveView);
                window.ShowDialog();

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
