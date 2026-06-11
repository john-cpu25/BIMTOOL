using System;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.Connection
{
    public class ConnectionEventHandler : IExternalEventHandler
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
                TaskDialog.Show("Error", $"Error executing task: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "ConnectionEventHandler";
        }
    }
}
