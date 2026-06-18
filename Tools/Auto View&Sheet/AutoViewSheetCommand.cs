using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.AutoViewSheet.UI;
using RincoNhan.Tools.AutoViewSheet.ViewModels;

namespace RincoNhan.Tools.AutoViewSheet
{
    [Transaction(TransactionMode.Manual)]
    public class AutoViewSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var doc = commandData.Application.ActiveUIDocument.Document;
                
                var handler = new AutoViewSheetHandler();
                var viewModel = new AutoViewSheetViewModel(handler, doc);
                
                var window = new AutoViewSheetWindow(viewModel);
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
