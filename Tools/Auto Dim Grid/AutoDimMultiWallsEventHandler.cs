using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Auto_Dim_Grid.ViewModels;

namespace RincoNhan.Tools.Auto_Dim_Grid
{
    public class AutoDimMultiWallsEventHandler : IExternalEventHandler
    {
        public AutoDimGridViewModel ViewModel { get; set; }
        public static string ActionType { get; set; } = "DrawLine";

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                if (ActionType == "DrawLine")
                    AutoDimMultiWallsLogic.CreateDimensions(doc, uidoc, ViewModel);
                else if (ActionType == "SelectWalls")
                    AutoDimMultiWallsLogic.CreateDimensionsBySelection(doc, uidoc, ViewModel);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName()
        {
            return "AutoDimMultiWallsEventHandler";
        }
    }
}
