using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

using RincoNhan.Tools.StairDetail.UI;

namespace RincoNhan.Tools.StairDetail;

[Transaction(TransactionMode.Manual)]
public class StairDetail : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIApplication application = commandData.Application;
            UIDocument uidoc = application.ActiveUIDocument;
            Document document = uidoc.Document;

            if (uidoc.ActiveView == null)
            {
                TaskDialog.Show("Error", "Vui lòng mở một view trước.");
                return Result.Cancelled;
            }

            // Get all rebar bar types sorted by name
            List<Element> rebarTypes = new FilteredElementCollector(document)
                .OfClass(typeof(RebarBarType))
                .WhereElementIsElementType()
                .OrderBy(e => e.Name)
                .ToList();

            // Show WPF dialog
            var window = new StairDetailWindow(rebarTypes);

            System.Windows.Interop.WindowInteropHelper helper =
                new System.Windows.Interop.WindowInteropHelper(window);
            helper.Owner = application.MainWindowHandle;

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true)
                return Result.Cancelled;

            // Get values from ViewModel
            var vm = window.DataContext as ViewModels.StairDetailViewModel;
            if (vm == null)
                return Result.Cancelled;

            // Validate input
            if (!int.TryParse(vm.ThepChuSpacing, out int thepChuSpacing))
            {
                TaskDialog.Show("Error", "Sai giá trị khoảng rải thép chính!");
                return Result.Failed;
            }
            if (!int.TryParse(vm.ThepPhuSpacing, out int thepPhuSpacing))
            {
                TaskDialog.Show("Error", "Sai giá trị khoảng rải thép phụ!");
                return Result.Failed;
            }

            // Run the logic
            var logic = new StairDetailLogic();
            return logic.Run(
                commandData,
                vm.SelectedThepChu,
                vm.SelectedThepPhu,
                thepChuSpacing,
                thepPhuSpacing,
                vm.IncludeRebarLandingTop,
                vm.IncludeRebarLandingBot);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Error", ex.ToString());
            message = ex.Message;
            return Result.Failed;
        }
    }
}
