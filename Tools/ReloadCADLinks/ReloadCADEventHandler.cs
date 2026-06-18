using System;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ReloadCADLinks
{
    public class ReloadCADEventHandler : IExternalEventHandler
    {
        public Action Action { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                Action?.Invoke();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
            }
        }

        public string GetName() => "ReloadCADEventHandler";
    }
}
