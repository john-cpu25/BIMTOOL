using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.AddSharedParameters
{
    [Transaction(TransactionMode.Manual)]
    public class AddSharedParamCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var app = commandData.Application.Application;
            var doc = commandData.Application.ActiveUIDocument.Document;

            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Error", "This tool can only be used in a Family Document environment.");
                return Result.Failed;
            }

            try
            {
                var vm = new ViewModels.AddSharedParamViewModel(doc, app);
                var window = new UI.AddSharedParamWindow(vm);
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
