using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.FillRegionManager
{
    [Transaction(TransactionMode.Manual)]
    public class FillRegionManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                Document doc = uiapp.ActiveUIDocument.Document;

                // Collect all FilledRegionType
                var fillRegionTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .ToList();

                List<FillRegionModel> models = new List<FillRegionModel>();

                foreach (var frt in fillRegionTypes)
                {
                    var model = new FillRegionModel
                    {
                        Id = frt.Id,
                        TypeName = frt.Name
                    };

                    // Get Type Mark
                    Parameter typeMarkParam = frt.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                    if (typeMarkParam != null)
                    {
                        model.TypeMark = typeMarkParam.AsString();
                    }

                    // Get Hatch Name
                    string hatchStr = "";
                    try
                    {
                        ElementId fgId = frt.ForegroundPatternId;
                        if (fgId != ElementId.InvalidElementId)
                        {
                            var fpe = doc.GetElement(fgId) as FillPatternElement;
                            if (fpe != null)
                            {
                                hatchStr = fpe.Name;
                            }
                        }
                        
                        // Optionally get Background Pattern too
                        ElementId bgId = frt.BackgroundPatternId;
                        if (bgId != ElementId.InvalidElementId)
                        {
                            var fpeBg = doc.GetElement(bgId) as FillPatternElement;
                            if (fpeBg != null)
                            {
                                if (!string.IsNullOrEmpty(hatchStr)) hatchStr += " / ";
                                hatchStr += fpeBg.Name;
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(hatchStr))
                    {
                        hatchStr = "<None>";
                    }
                    
                    model.HatchName = hatchStr;
                    model.HatchPreview = HatchRenderer.GetHatchPreview(doc, frt);

                    if (model.TypeName.ToUpper().Contains("RINCO_FR_LP"))
                    {
                        model.Group = "LOADING";
                    }
                    else
                    {
                        model.Group = "OTHER";
                    }

                    models.Add(model);
                }

                // Sort by Name
                models = models.OrderBy(m => m.TypeName).ToList();

                // Show Window
                FillRegionManagerWindow window = new FillRegionManagerWindow(models);
                
                // Attach to Revit window
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var helper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = process.MainWindowHandle
                };

                window.Show();

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
