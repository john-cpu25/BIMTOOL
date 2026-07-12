using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.TwoDFamilyManager
{
    public class FamilyManagerEventHandler : IExternalEventHandler
    {
        public FamilySymbol SymbolToPlace { get; set; }

        public void Execute(UIApplication app)
        {
            if (SymbolToPlace == null) return;

            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Hide the window to give focus back to Revit and prevent re-entrancy crashes
            Command.Instance?.Hide();

            try
            {
                // Ensure the symbol is active before placing
                if (!SymbolToPlace.IsActive)
                {
                    using (Transaction t = new Transaction(doc, "Activate Family Symbol"))
                    {
                        t.Start();
                        SymbolToPlace.Activate();
                        doc.Regenerate();
                        t.Commit();
                    }
                }

                uidoc.PromptForFamilyInstancePlacement(SymbolToPlace);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled the placement, which is normal
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Could not place the family: " + ex.Message);
            }
            finally
            {
                // Show the window again after placement is done
                Command.Instance?.Show();
            }
        }

        public string GetName()
        {
            return "2D Family Manager Event Handler";
        }
    }
}
