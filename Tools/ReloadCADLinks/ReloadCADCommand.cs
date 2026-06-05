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
            var processedTypeIds = new HashSet<string>();

            // 1. Collect CADLinkType elements (these are the type definitions for linked CADs)
            var cadLinkTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(CADLinkType))
                .Cast<CADLinkType>()
                .ToList();

            foreach (var cadLinkType in cadLinkTypes)
            {
                string typeIdStr = cadLinkType.Id.ToString();
                if (processedTypeIds.Contains(typeIdStr)) continue;
                processedTypeIds.Add(typeIdStr);

                // Always create entry - never skip a file
                string fileName = cadLinkType.Name ?? "Unknown CAD";
                string filePath = "";
                LinkedFileStatus linkedStatus = LinkedFileStatus.Loaded;
                bool isReloadable = false;
                bool isCloud = false;
                string status = "Unknown";
                string statusIcon = "⚠️";

                try
                {
                    ExternalFileReference extRef = null;
                    try { extRef = cadLinkType.GetExternalFileReference(); } catch { }

                    if (extRef != null)
                    {
                        isReloadable = true;

                        // Get model path safely
                        ModelPath modelPath = null;
                        try { modelPath = extRef.GetAbsolutePath(); } catch { }

                        if (modelPath != null)
                        {
                            // Check cloud path safely - each property individually
                            try { isCloud = modelPath.ServerPath; } catch { }
                            if (!isCloud)
                            {
                                try { isCloud = modelPath.CloudPath; } catch { }
                            }

                            // Convert to visible path
                            try
                            {
                                filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                            }
                            catch { }
                        }

                        // Get file name from path
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            try
                            {
                                string fn = Path.GetFileName(filePath);
                                if (!string.IsNullOrEmpty(fn)) fileName = fn;
                            }
                            catch { }
                        }

                        // Get linked file status
                        try { linkedStatus = extRef.GetLinkedFileStatus(); } catch { }

                        // Check local file exists
                        if (!isCloud && !string.IsNullOrEmpty(filePath) && filePath != cadLinkType.Name)
                        {
                            try
                            {
                                if (!File.Exists(filePath) && linkedStatus != LinkedFileStatus.NotFound)
                                    linkedStatus = LinkedFileStatus.NotFound;
                            }
                            catch { }
                        }

                        status = CADLinkInfo.GetStatusText(linkedStatus);
                        statusIcon = CADLinkInfo.GetStatusIcon(linkedStatus);
                    }
                    else
                    {
                        // extRef is null - check if it's a cloud link (Autodesk Docs / BIM 360)
                        // Cloud links don't have ExternalFileReference, but have ExternalResourceReferences
                        bool foundCloudRef = false;
                        try
                        {
                            var extResRefs = cadLinkType.GetExternalResourceReferences();
                            if (extResRefs != null && extResRefs.Count > 0)
                            {
                                foundCloudRef = true;
                                isReloadable = true;
                                isCloud = true;
                                linkedStatus = LinkedFileStatus.Loaded;

                                // Try to get path info from the resource reference
                                foreach (var kvp in extResRefs)
                                {
                                    try
                                    {
                                        var refInfo = kvp.Value.GetReferenceInformation();
                                        if (refInfo != null)
                                        {
                                            // Try known keys for path information
                                            string pathValue = null;
                                            foreach (var info in refInfo)
                                            {
                                                string key = info.Key.ToLowerInvariant();
                                                if (key.Contains("path") || key.Contains("folder") || key.Contains("location"))
                                                {
                                                    if (string.IsNullOrEmpty(pathValue))
                                                        pathValue = info.Value;
                                                    else
                                                        pathValue = pathValue + "/" + info.Value;
                                                }
                                            }
                                            if (!string.IsNullOrEmpty(pathValue))
                                            {
                                                filePath = pathValue;
                                            }
                                        }

                                        // Try InSessionPath as fallback
                                        if (string.IsNullOrEmpty(filePath))
                                        {
                                            try
                                            {
                                                string inSession = kvp.Value.InSessionPath;
                                                if (!string.IsNullOrEmpty(inSession))
                                                    filePath = inSession;
                                            }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                    break; // Only need first reference
                                }

                                if (string.IsNullOrEmpty(filePath))
                                    filePath = "Cloud: " + fileName;

                                status = "Loaded";
                                statusIcon = "☁️";
                            }
                        }
                        catch { }

                        // If not cloud, it's truly imported
                        if (!foundCloudRef)
                        {
                            filePath = "Imported - Not reloadable";
                            status = "Imported";
                            statusIcon = "📥";
                        }
                    }
                }
                catch
                {
                    // Even if everything fails, still add the entry
                    if (string.IsNullOrEmpty(filePath)) filePath = cadLinkType.Name;
                    status = "Error";
                    statusIcon = "⚠️";
                }

                links.Add(new CADLinkInfo
                {
                    FileName = fileName,
                    FilePath = string.IsNullOrEmpty(filePath) ? cadLinkType.Name : filePath,
                    Status = status,
                    StatusIcon = statusIcon,
                    TypeId = cadLinkType.Id,
                    LinkedStatus = linkedStatus,
                    IsReloadable = isReloadable,
                    IsCloudPath = isCloud,
                    IsSelected = isReloadable && (isCloud || linkedStatus != LinkedFileStatus.NotFound)
                });
            }

            // 2. Also find linked ImportInstance elements — their types may be CADLinkType
            //    that were missed by the collector above
            var importInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .ToList();

            foreach (var imp in importInstances)
            {
                try
                {
                    ElementId typeId = imp.GetTypeId();
                    if (typeId == null || typeId == ElementId.InvalidElementId) continue;
                    string typeIdStr = typeId.ToString();
                    if (processedTypeIds.Contains(typeIdStr)) continue;
                    processedTypeIds.Add(typeIdStr);

                    Element typeElem = doc.GetElement(typeId);
                    if (typeElem == null) continue;

                    // Check if it's a CADLinkType
                    CADLinkType cadType = typeElem as CADLinkType;
                    if (cadType != null)
                    {
                        ExternalFileReference extRef = null;
                        try { extRef = cadType.GetExternalFileReference(); } catch { }
                        bool isLink = extRef != null;

                        string filePath = "";
                        string fileName = cadType.Name;
                        LinkedFileStatus status = LinkedFileStatus.Loaded;
                        bool isCloud = false;

                        if (isLink)
                        {
                            ModelPath modelPath = extRef.GetAbsolutePath();
                            isCloud = modelPath.ServerPath || modelPath.CloudPath;
                            filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                            try { fileName = Path.GetFileName(filePath); } catch { }
                            if (string.IsNullOrEmpty(fileName)) fileName = cadType.Name;
                            status = extRef.GetLinkedFileStatus();

                            if (!isCloud && !File.Exists(filePath) && status != LinkedFileStatus.NotFound)
                                status = LinkedFileStatus.NotFound;
                        }
                        else
                        {
                            filePath = "Imported - Not reloadable";
                        }

                        links.Add(new CADLinkInfo
                        {
                            FileName = fileName,
                            FilePath = filePath,
                            Status = isLink ? CADLinkInfo.GetStatusText(status) : "Imported",
                            StatusIcon = isLink ? CADLinkInfo.GetStatusIcon(status) : "📥",
                            TypeId = cadType.Id,
                            LinkedStatus = status,
                            IsReloadable = isLink,
                            IsCloudPath = isCloud,
                            IsSelected = isLink && (isCloud || status != LinkedFileStatus.NotFound)
                        });
                    }
                    else
                    {
                        // It's an imported CAD (not linked) — show as non-reloadable
                        string name = imp.Category != null ? imp.Category.Name : typeElem.Name;
                        if (string.IsNullOrEmpty(name)) name = "Unknown Import";

                        links.Add(new CADLinkInfo
                        {
                            FileName = name,
                            FilePath = "Imported - Not reloadable",
                            Status = "Imported",
                            StatusIcon = "📥",
                            TypeId = typeId,
                            LinkedStatus = LinkedFileStatus.Loaded,
                            IsReloadable = false,
                            IsCloudPath = false,
                            IsSelected = false
                        });
                    }
                }
                catch { continue; }
            }

            // 3. Search inside linked Revit documents for nested CAD links
            try
            {
                var revitLinks = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var revitLink in revitLinks)
                {
                    try
                    {
                        Document linkedDoc = revitLink.GetLinkDocument();
                        if (linkedDoc == null) continue;

                        var nestedCadTypes = new FilteredElementCollector(linkedDoc)
                            .OfClass(typeof(CADLinkType))
                            .Cast<CADLinkType>()
                            .ToList();

                        foreach (var cadType in nestedCadTypes)
                        {
                            try
                            {
                                // Use linked doc title + type id for uniqueness
                                string uniqueKey = linkedDoc.Title + "|" + cadType.Id.ToString();
                                if (processedTypeIds.Contains(uniqueKey)) continue;
                                processedTypeIds.Add(uniqueKey);

                                ExternalFileReference extRef = null;
                                try { extRef = cadType.GetExternalFileReference(); } catch { }
                                bool isLink = extRef != null;

                                string filePath = "";
                                string fileName = cadType.Name;
                                LinkedFileStatus status = LinkedFileStatus.Loaded;
                                bool isCloud = false;

                                if (isLink)
                                {
                                    ModelPath modelPath = extRef.GetAbsolutePath();
                                    isCloud = modelPath.ServerPath || modelPath.CloudPath;
                                    filePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
                                    try { fileName = Path.GetFileName(filePath); } catch { }
                                    if (string.IsNullOrEmpty(fileName)) fileName = cadType.Name;
                                    status = extRef.GetLinkedFileStatus();

                                    if (!isCloud && !File.Exists(filePath) && status != LinkedFileStatus.NotFound)
                                        status = LinkedFileStatus.NotFound;
                                }
                                else
                                {
                                    filePath = "Imported in: " + linkedDoc.Title;
                                }

                                links.Add(new CADLinkInfo
                                {
                                    FileName = fileName,
                                    FilePath = filePath,
                                    Status = isLink ? CADLinkInfo.GetStatusText(status) : "Imported",
                                    StatusIcon = isLink ? CADLinkInfo.GetStatusIcon(status) : "📥",
                                    TypeId = cadType.Id,
                                    LinkedStatus = status,
                                    IsReloadable = isLink,
                                    IsCloudPath = isCloud,
                                    IsSelected = isLink && (isCloud || status != LinkedFileStatus.NotFound)
                                });
                            }
                            catch { continue; }
                        }
                    }
                    catch { continue; }
                }
            }
            catch { }

            return links.OrderBy(l => l.FileName).ToList();
        }
    }
}
