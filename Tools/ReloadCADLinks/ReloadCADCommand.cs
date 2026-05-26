using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ReloadCADLinks
{
    /// <summary>
    /// Revit External Command that opens the Reload CAD Links window.
    /// Collects all linked CAD files and presents them for batch reload.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ReloadCADCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document doc = uiDoc.Document;

                // Collect all CAD link types from the document
                List<CADLinkInfo> cadLinks = CollectCADLinks(doc);

                if (cadLinks.Count == 0)
                {
                    TaskDialog.Show("Reload CAD Links",
                        "No CAD links found in the current project.");
                    return Result.Cancelled;
                }

                // Show the reload window
                ReloadCADWindow window = new ReloadCADWindow(cadLinks, doc);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Error", $"An error occurred:\n{ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Collects all CAD files (Links and Imports) from the Revit document
        /// </summary>
        private List<CADLinkInfo> CollectCADLinks(Document doc)
        {
            List<CADLinkInfo> links = new List<CADLinkInfo>();

            // Collect all CAD link types from the document
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var cadLinkTypes = collector
                .OfClass(typeof(CADLinkType))
                .Cast<CADLinkType>()
                .ToList();

            foreach (var cadLinkType in cadLinkTypes)
            {
                try
                {
                    // Get external file reference
                    ExternalFileReference extRef = cadLinkType.GetExternalFileReference();
                    bool isLink = extRef != null;

                    string filePath = "";
                    string fileName = cadLinkType.Name;
                    LinkedFileStatus status = LinkedFileStatus.Loaded;
                    bool isCloud = false;

                    if (isLink)
                    {
                        ModelPath modelPath = extRef.GetAbsolutePath();
                        isCloud = modelPath.ServerPath || modelPath.CloudPath;
                        filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                        
                        // Try to get a cleaner file name from path
                        try { fileName = Path.GetFileName(filePath); } catch { }
                        if (string.IsNullOrEmpty(fileName)) fileName = cadLinkType.Name;

                        status = extRef.GetLinkedFileStatus();

                        // Check if file exists on disk (ONLY for local/network paths)
                        if (!isCloud)
                        {
                            bool fileExists = File.Exists(filePath);
                            if (!fileExists && status != LinkedFileStatus.NotFound)
                            {
                                status = LinkedFileStatus.NotFound;
                            }
                        }
                    }
                    else
                    {
                        // This is an Import (embedded in the project)
                        filePath = "Imported - Not reloadable";
                    }

                    var linkInfo = new CADLinkInfo
                    {
                        FileName = fileName,
                        FilePath = filePath,
                        Status = isLink ? CADLinkInfo.GetStatusText(status) : "Imported",
                        StatusIcon = isLink ? CADLinkInfo.GetStatusIcon(status) : "📥",
                        TypeId = cadLinkType.Id,
                        LinkedStatus = status,
                        IsReloadable = isLink,
                        IsCloudPath = isCloud,
                        IsSelected = isLink && (isCloud || status != LinkedFileStatus.NotFound)
                    };

                    links.Add(linkInfo);
                }
                catch (Exception)
                {
                    // Skip problematic entries
                    continue;
                }
            }

            // Sort by file name
            return links.OrderBy(l => l.FileName).ToList();
        }
    }
}
