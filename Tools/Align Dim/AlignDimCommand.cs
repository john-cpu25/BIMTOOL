using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.Align_Dim.UI;
using RincoNhan.Tools.Align_Dim.ViewModels;

namespace RincoNhan.Tools.Align_Dim
{
    public class GridSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Grid;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    public class DimensionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Dimension;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignDimCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            var viewModel = new AlignDimViewModel(doc);
            var window = new AlignDimWindow(viewModel);

            // Show dialog to user
            window.ShowDialog();

            // Check if user clicked Apply
            if (viewModel.DialogResult)
            {
                try
                {
                    // Prompt user to pick a grid
                    Reference gridRef = uidoc.Selection.PickObject(ObjectType.Element, new GridSelectionFilter(), "Please select a Grid.");
                    Grid grid = doc.GetElement(gridRef) as Grid;

                    // Prompt user to pick a dimension
                    Reference dimRef = uidoc.Selection.PickObject(ObjectType.Element, new DimensionSelectionFilter(), "Please select a Dimension.");
                    Dimension dim = doc.GetElement(dimRef) as Dimension;

                    if (viewModel.SelectedTabIndex == 0)
                    {
                        // Tab 1: By Distance
                        AlignDimLogic.AlignDimensionToGrid(doc, dim, grid, viewModel.Distance);
                    }
                    else if (viewModel.SelectedTabIndex == 1)
                    {
                        // Tab 2: Match Views
                        var targetViewIds = viewModel.TargetViews
                            .Where(v => v.IsSelected)
                            .Select(v => v.ViewId)
                            .ToList();
                        AlignDimLogic.MatchDimensionsInViews(doc, dim, grid, targetViewIds);
                    }
                    
                    return Result.Succeeded;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User pressed Esc
                    return Result.Cancelled;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    return Result.Failed;
                }
            }

            return Result.Cancelled;
        }
    }
}
