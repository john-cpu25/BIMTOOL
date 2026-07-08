using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.Create_Sheet_Set.UI;
using RincoNhan.Tools.Create_Sheet_Set.ViewModels;

namespace RincoNhan.Tools.Create_Sheet_Set
{
    [Transaction(TransactionMode.Manual)]
    public class CreateSheetSetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            // Get all revisions
            var revisions = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Revisions)
                .WhereElementIsNotElementType()
                .Cast<Revision>()
                .ToList();

            if (revisions.Count == 0)
            {
                TaskDialog.Show("Error", "No revisions found in the document.");
                return Result.Cancelled;
            }

            var viewModels = revisions.Select(r => new RevisionViewModel(r)).ToList();
            var window = new CreateSheetSetWindow(viewModels);

            if (window.ShowDialog() == true)
            {
                var selectedRevisions = viewModels.Where(x => x.IsSelected).ToList();
                if (selectedRevisions.Count == 0) return Result.Cancelled;

                bool matchAny = window.MatchAny;
                string setName = window.SetName;

                if (string.IsNullOrWhiteSpace(setName))
                {
                    TaskDialog.Show("Error", "Operation cancelled. No name was provided for the revision sheet set.");
                    return Result.Cancelled;
                }

                using (Transaction t = new Transaction(doc, "Create Revision Sheet Set"))
                {
                    t.Start();

                    // Get all sheets
                    var sheets = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Sheets)
                        .WhereElementIsNotElementType()
                        .Cast<ViewSheet>()
                        .ToList();

                    var matchingSheets = new List<ViewSheet>();
                    var selectedRevIds = selectedRevisions.Select(x => x.Id).ToList();

                    foreach (var sheet in sheets)
                    {
                        var sheetRevIds = sheet.GetAllRevisionIds();
                        if (matchAny)
                        {
                            if (selectedRevIds.Any(id => sheetRevIds.Contains(id)))
                            {
                                matchingSheets.Add(sheet);
                            }
                        }
                        else
                        {
                            if (selectedRevIds.All(id => sheetRevIds.Contains(id)))
                            {
                                matchingSheets.Add(sheet);
                            }
                        }
                    }

                    if (matchingSheets.Count == 0)
                    {
                        TaskDialog.Show("Warning", "No sheets found matching the selected revisions.");
                        t.RollBack();
                        return Result.Cancelled;
                    }

                    // Check for empty sheets (placeholders)
                    var emptySheets = matchingSheets.Where(s => IsSheetEmpty(doc, s)).ToList();

                    ViewSet viewSet = new ViewSet();
                    foreach (var sheet in matchingSheets)
                    {
                        viewSet.Insert(sheet);
                    }

                    PrintManager printManager = doc.PrintManager;
                    printManager.PrintRange = PrintRange.Select;
                    ViewSheetSetting viewSheetSetting = printManager.ViewSheetSetting;
                    viewSheetSetting.CurrentViewSheetSet.Views = viewSet;
                    
                    try
                    {
                        viewSheetSetting.SaveAs(setName);
                    }
                    catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                    {
                        TaskDialog.Show("Error", "A View/Sheet Set with this name already exists. Please choose a different name.");
                        t.RollBack();
                        return Result.Failed;
                    }

                    t.Commit();

                    if (emptySheets.Count > 0)
                    {
                        string emptySheetNames = string.Join("\n", emptySheets.Select(s => s.SheetNumber + " - " + s.Name));
                        TaskDialog.Show("Placeholder Sheets", "These sheets do not have any model contents and seem to be placeholders for other content:\n" + emptySheetNames);
                    }

                    TaskDialog.Show("Success", $"Successfully created Sheet Set '{setName}' with {matchingSheets.Count} sheets.");
                }

                return Result.Succeeded;
            }

            return Result.Cancelled;
        }

        private bool IsSheetEmpty(Document doc, ViewSheet sheet)
        {
            var viewports = sheet.GetAllViewports();
            if (viewports.Count > 0) return false;

            // Also check for schedule instances
            var schedules = new FilteredElementCollector(doc, sheet.Id)
                .OfClass(typeof(ScheduleSheetInstance))
                .ToList();
            
            return schedules.Count == 0;
        }
    }
}
