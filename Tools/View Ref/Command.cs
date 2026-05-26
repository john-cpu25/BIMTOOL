using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ViewRef.UI;
using RincoNhan.Tools.ViewRef.ViewModels;

namespace RincoNhan.Tools.ViewRef
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            // Collect View Reference Types
            // This category is only available and useful for ReferenceViewer.Create (Revit 2024+)
            var viewRefTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .WherePasses(new ElementCategoryFilter(BuiltInCategory.OST_ReferenceViewerSymbol))
                .Cast<ElementType>()
                .OrderByDescending(t => t.Name.Contains("RINCO"))
                .ThenBy(t => t.Name)
                .ToList();

            var typesCollection = new ObservableCollection<ElementType>(viewRefTypes);

            // Initialize Handler and ViewModel
            var handler = new ViewRefExternalEventHandler();
            var viewModel = new ViewRefViewModel(handler, typesCollection);
            
            // Show UI
            var window = new ViewRefWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }
}
