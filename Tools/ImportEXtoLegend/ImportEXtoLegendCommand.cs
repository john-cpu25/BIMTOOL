using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ImportEXtoLegend
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var handler = new ImportEXtoLegendHandler();
                var viewModel = new ImportEXtoLegendViewModel(handler, commandData.Application.ActiveUIDocument.Document);
                
                ImportEXtoLegendWindow window = new ImportEXtoLegendWindow(viewModel);
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
