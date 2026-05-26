using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace RincoNhan
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Register Assembly Resolver to handle library conflicts (System.Text.Json, etc.)
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Khá»Ÿi táº¡o Ribbon UI thÃ´ng qua RibbonManager
            RincoNhan.Core.RibbonManager.SetupRibbon(application);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            return Result.Succeeded;
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                // Try to find any unresolved assembly in our plugin folder
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string targetPath = Path.Combine(assemblyDir, assemblyName);

                if (File.Exists(targetPath))
                {
                    return Assembly.LoadFrom(targetPath);
                }
            }
            catch
            {
                // Silently fail to let other resolvers try
            }
            return null;
        }
    }
}
