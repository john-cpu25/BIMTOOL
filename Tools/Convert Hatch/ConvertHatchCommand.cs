using System;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.ConvertHatch.UI;

namespace RincoNhan.Tools.ConvertHatch
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertHatchCommand : IExternalCommand
    {
        // Keep a static reference to ensure only one instance of the window is open
        public static ConvertHatchWindow WindowInstance;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (WindowInstance != null && WindowInstance.IsLoaded)
                {
                    WindowInstance.Focus();
                }
                else
                {
                    ConvertHatchEventHandler handler = new ConvertHatchEventHandler();
                    ExternalEvent exEvent = ExternalEvent.Create(handler);
                    
                    WindowInstance = new ConvertHatchWindow(handler, exEvent);
                    
                    // Attach to Revit main window using WindowInteropHelper
                    var process = Process.GetCurrentProcess();
                    var helper = new System.Windows.Interop.WindowInteropHelper(WindowInstance)
                    {
                        Owner = process.MainWindowHandle
                    };

                    WindowInstance.Show();
                }
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
