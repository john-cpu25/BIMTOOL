using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.DuplicateSheet.UI;

namespace RincoNhan.Tools.DuplicateSheet
{
    public static class DuplicateSheetLogic
    {
        public static void Execute(ExternalCommandData commandData, bool withDetailing)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Step 1: Try to get pre-selected sheets
            List<ViewSheet> selectedSheets = new List<ViewSheet>();
            var selectionIds = uidoc.Selection.GetElementIds();
            foreach (var id in selectionIds)
            {
                if (doc.GetElement(id) is ViewSheet sheet)
                {
                    selectedSheets.Add(sheet);
                }
            }

            // Step 2: If no sheets selected, show UI
            if (selectedSheets.Count == 0)
            {
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(s => !s.IsTemplate)
                    .ToList();

                if (allSheets.Count == 0)
                {
                    TaskDialog.Show("Error", "No sheets found in the document.");
                    return;
                }

                var window = new SelectSheetsWindow(allSheets);
                // Set Revit as parent window
                var hndl = uiapp.MainWindowHandle;
                var helper = new WindowInteropHelper(window);
                helper.Owner = hndl;

                if (window.ShowDialog() == true)
                {
                    selectedSheets = window.SelectedSheets;
                }
                else
                {
                    return; // Cancelled
                }
            }

            if (selectedSheets.Count == 0) return;

            // Step 3: Duplicate sheets
            HashSet<string> allSheetNames = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.Name));
            HashSet<string> allSheetNumbers = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber));
            HashSet<string> allViewNames = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Select(v => v.Name));

            List<ViewSheet> copiedSheets = new List<ViewSheet>();
            int copiedViewsCount = 0;

            using (Transaction t = new Transaction(doc, withDetailing ? "Duplicate Sheets with Detailing" : "Duplicate Empty Sheets"))
            {
                t.Start();
                try
                {
                    foreach (var sheet in selectedSheets)
                    {
                        // Get TitleBlock
                        var titleblocks = new FilteredElementCollector(doc, sheet.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsNotElementType()
                            .ToElements();

                        ElementId tbTypeId = ElementId.InvalidElementId;
                        if (titleblocks.Count > 0)
                        {
                            tbTypeId = titleblocks[0].GetTypeId();
                        }

                        // Unique Name & Number
                        string baseName = sheet.Name + "_MTO";
                        string newName = MakeUniqueName(baseName, allSheetNames);
                        allSheetNames.Add(newName);

                        string baseNumber = sheet.SheetNumber + "_MTO";
                        string newNumber = MakeUniqueName(baseNumber, allSheetNumbers);
                        allSheetNumbers.Add(newNumber);

                        ViewSheet newSheet = ViewSheet.Create(doc, tbTypeId);
                        newSheet.Name = newName;
                        newSheet.SheetNumber = newNumber;
                        copiedSheets.Add(newSheet);

                        if (withDetailing)
                        {
                            // Copy views
                            foreach (ElementId vpId in sheet.GetAllViewports())
                            {
                                Viewport vp = doc.GetElement(vpId) as Viewport;
                                if (vp == null) continue;

                                View view = doc.GetElement(vp.ViewId) as View;
                                if (view == null) continue;

                                // Check if view can be duplicated
                                if (view.CanViewBeDuplicated(ViewDuplicateOption.WithDetailing))
                                {
                                    ElementId newViewId = view.Duplicate(ViewDuplicateOption.WithDetailing);
                                    View newView = doc.GetElement(newViewId) as View;

                                    if (newView != null)
                                    {
                                        string newViewName = newView.Name.Replace("Copy", "MTO");
                                        newViewName = MakeUniqueName(newViewName, allViewNames);
                                        newView.Name = newViewName;
                                        allViewNames.Add(newViewName);

                                        Viewport.Create(doc, newSheet.Id, newView.Id, vp.GetBoxCenter());
                                        copiedViewsCount++;
                                    }
                                }
                            }
                        }
                    }
                    t.Commit();
                }
                catch (Exception ex)
                {
                    t.RollBack();
                    TaskDialog.Show("Error", $"An error occurred:\n{ex.Message}");
                    return;
                }
            }

            // Step 4: Show summary
            string msg = $"Duplicated {copiedSheets.Count} sheet(s)";
            if (withDetailing)
            {
                msg += $" and {copiedViewsCount} view(s).";
            }
            else
            {
                msg += ".";
            }
            TaskDialog.Show("Success", msg);
        }

        private static string MakeUniqueName(string baseName, HashSet<string> existingNames)
        {
            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            int i = 1;
            while (true)
            {
                string newName = $"{baseName}_{i}";
                if (!existingNames.Contains(newName))
                {
                    return newName;
                }
                i++;
            }
        }
    }
}
