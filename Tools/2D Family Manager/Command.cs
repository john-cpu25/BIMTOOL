using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.TwoDFamilyManager.UI;
using RincoNhan.Tools.TwoDFamilyManager.ViewModels;

namespace RincoNhan.Tools.TwoDFamilyManager
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        // Keep a static reference to ensure only one instance of the window is open
        public static FamilyManagerWindow Instance { get; private set; }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (Instance != null && Instance.IsLoaded)
                {
                    Instance.Focus();
                    return Result.Succeeded;
                }

                Document doc = commandData.Application.ActiveUIDocument.Document;

                // Initialize the Event Handler
                var handler = new FamilyManagerEventHandler();
                ExternalEvent exEvent = ExternalEvent.Create(handler);

                // Initialize ViewModel
                var viewModel = new FamilyManagerViewModel(doc, exEvent, handler);

                // Initialize Window
                Instance = new FamilyManagerWindow
                {
                    DataContext = viewModel
                };

                // Show the window modelessly
                Instance.Show();

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
