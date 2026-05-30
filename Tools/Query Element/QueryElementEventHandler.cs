using System;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.QueryElement
{
    public class QueryElementEventHandler : IExternalEventHandler
    {
        public Action<UIApplication> Action { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Action?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "An error occurred while switching views:\n" + ex.Message);
            }
            finally
            {
                Action = null; // Clear action after execution
            }
        }

        public string GetName()
        {
            return "QueryElement EventHandler";
        }
    }
}
