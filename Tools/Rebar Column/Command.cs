using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RincoNhan.Tools.RebarColumn.UI;
using RincoNhan.Tools.RebarColumn.ViewModels;

namespace RincoNhan.Tools.RebarColumn
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Select Column
            FamilyInstance column = null;
            try
            {
                Reference hasSelected = uidoc.Selection.PickObject(ObjectType.Element, new ColumnSelectionFilter(), "Select a Column");
                column = doc.GetElement(hasSelected) as FamilyInstance;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (column == null)
            {
                message = "Please select a Column.";
                return Result.Failed;
            }

            // 2. Extract Column Geometry
            double colWidth = 0, colDepth = 0, colHeight = 0;
            string colName = column.Symbol.FamilyName + " : " + column.Symbol.Name;

            // Try common parameter names for width (b) and depth (h)
            string[] widthNames = { "b", "B", "Width" };
            string[] depthNames = { "h", "H", "Depth" };

            foreach (var name in widthNames)
            {
                Parameter p = column.Symbol.LookupParameter(name) ?? column.LookupParameter(name);
                if (p != null && p.HasValue) { colWidth = p.AsDouble() * 304.8; break; } // feet to mm
            }
            foreach (var name in depthNames)
            {
                Parameter p = column.Symbol.LookupParameter(name) ?? column.LookupParameter(name);
                if (p != null && p.HasValue) { colDepth = p.AsDouble() * 304.8; break; } // feet to mm
            }

            // Fallback to bounding box
            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            if (bbox != null)
            {
                if (colWidth == 0) colWidth = (bbox.Max.X - bbox.Min.X) * 304.8;
                if (colDepth == 0) colDepth = (bbox.Max.Y - bbox.Min.Y) * 304.8;
                colHeight = (bbox.Max.Z - bbox.Min.Z) * 304.8;
            }

            // 3. Initialize Handler and ViewModel
            var handler = new RebarColumnExternalEventHandler();
            var eventHandler = ExternalEvent.Create(handler);
            
            var viewModel = new RebarColumnViewModel(eventHandler);

            // Retrieve loaded Rebar Shapes from Revit Document
            var rebarShapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .OrderBy(s => s.Name)
                .Select(s => s.Name)
                .ToList();

            foreach (var shapeName in rebarShapes)
            {
                viewModel.AvailableShapes.Add(shapeName);
            }

            // Set a smart default shape selection
            viewModel.SelectedStirrupShapeName = rebarShapes.FirstOrDefault(name => name.Contains("Shape 1") || name.Contains("Shape_1") || name.Contains("1")) 
                                                 ?? rebarShapes.FirstOrDefault() 
                                                 ?? "";

            // Set column geometry on ViewModel
            viewModel.ColumnName = colName;
            viewModel.ColumnWidth = Math.Round(colWidth, 0);
            viewModel.ColumnDepth = Math.Round(colDepth, 0);
            viewModel.ColumnHeight = Math.Round(colHeight, 0);

            handler.Doc = doc;
            handler.SelectedColumn = column;
            handler.ViewModel = viewModel;

            // 4. Show UI
            var window = new RebarColumnWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
    }

    public class ColumnSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category == null) return false;
            return elem.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_Columns || 
                   elem.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralColumns;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
