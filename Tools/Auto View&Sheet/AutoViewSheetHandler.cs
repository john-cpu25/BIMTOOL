using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.AutoViewSheet.Models;
using RincoNhan.Tools.AutoViewSheet.ViewModels;

namespace RincoNhan.Tools.AutoViewSheet
{
    public class AutoViewSheetHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public AutoViewSheetViewModel ViewModel { get; set; }

        public AutoViewSheetHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                ExecuteAutoProcess(doc);
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Error: {ex.Message}";
            }
        }

        private int _tempSheetCounter = 100000;

        private void ExecuteAutoProcess(Document doc)
        {
            var rowsToProcess = ViewModel.AutoRows.Where(r => r.IsSelected && r.SelectedLevel != null).ToList();
            if (!rowsToProcess.Any())
            {
                ViewModel.StatusMessage = "No valid rows selected.";
                return;
            }

            if (ViewModel.SelectedTitleBlock == null)
            {
                ViewModel.StatusMessage = "Please select a TitleBlock in Settings.";
                return;
            }

            var existingSheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var duplicateNumbers = rowsToProcess.Select(r => r.SheetNumber)
                .Where(sn => !string.IsNullOrEmpty(sn) && existingSheets.Contains(sn))
                .Distinct()
                .ToList();

            if (duplicateNumbers.Any())
            {
                TaskDialog td = new TaskDialog("Duplicate Sheets");
                td.MainInstruction = "Some Sheet Numbers already exist.";
                td.MainContent = $"The following Sheet Numbers already exist in the project:\n\n{string.Join(", ", duplicateNumbers.Take(10))}{(duplicateNumbers.Count > 10 ? "\n..." : "")}\n\nDo you want to continue? (They will automatically be renamed with .1, .2, etc.)";
                td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                td.DefaultButton = TaskDialogResult.No;
                if (td.Show() == TaskDialogResult.No)
                {
                    ViewModel.StatusMessage = "Process cancelled due to duplicate sheets.";
                    return;
                }
            }

            int successCount = 0;
            int errorCount = 0;
            _tempSheetCounter = 100000;

            using (Transaction trans = new Transaction(doc, "Rinco - Auto View & Sheet"))
            {
                trans.Start();

                Dictionary<string, ViewSheet> createdSheets = new Dictionary<string, ViewSheet>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, int> sheetViewCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rowsToProcess)
                {
                    try
                    {
                        // 1. CREATE VIEW
                        View newView = CreateView(doc, row);
                        if (newView == null) throw new Exception("Failed to create View.");

                        // 2. CREATE OR GET SHEET
                        ViewSheet newSheet = null;
                        if (!string.IsNullOrEmpty(row.SheetNumber) && createdSheets.ContainsKey(row.SheetNumber))
                        {
                            newSheet = createdSheets[row.SheetNumber];
                        }
                        else
                        {
                            newSheet = CreateSheet(doc, row, ViewModel.SelectedTitleBlock);
                            if (newSheet == null) throw new Exception("Failed to create Sheet.");

                            if (!string.IsNullOrEmpty(row.SheetNumber))
                            {
                                createdSheets[row.SheetNumber] = newSheet;
                            }
                        }

                        // 3. ADD SCOPE BOX
                        if (ViewModel.SelectedGlobalScopeBox != null && ViewModel.SelectedGlobalScopeBox.Id != ElementId.InvalidElementId)
                        {
                            var sbParam = newView.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                            if (sbParam != null && !sbParam.IsReadOnly)
                            {
                                sbParam.Set(ViewModel.SelectedGlobalScopeBox.Id);
                            }
                        }

                        // 4. ADD VIEW TO SHEET
                        if (Viewport.CanAddViewToSheet(doc, newSheet.Id, newView.Id))
                        {
                            // Track view count for offset calculation
                            int count = sheetViewCount.ContainsKey(newSheet.SheetNumber) ? sheetViewCount[newSheet.SheetNumber] : 0;
                            
                            doc.Regenerate(); // Ensure titleblock geometry is available
                            
                            XYZ centerPt = new XYZ(1.38, 0.975, 0); // Fallback A1 center
                            var titleBlock = new FilteredElementCollector(doc, newSheet.Id)
                                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                                .OfClass(typeof(FamilyInstance))
                                .Cast<FamilyInstance>()
                                .FirstOrDefault();
                                
                            if (titleBlock != null)
                            {
                                BoundingBoxXYZ bbox = titleBlock.get_BoundingBox(newSheet);
                                if (bbox != null)
                                {
                                    centerPt = (bbox.Max + bbox.Min) / 2.0;
                                }
                            }

                            double offset = ViewModel.StackViews ? 0 : count * 2.0; // 2 ft offset if not stacked
                            XYZ placementPt = new XYZ(centerPt.X + offset, centerPt.Y, 0);
                            
                            Viewport newViewport = Viewport.Create(doc, newSheet.Id, newView.Id, placementPt);
                            
                            sheetViewCount[newSheet.SheetNumber] = count + 1;

                            // 5. TURN ON TITLE (Apply Viewport Title Type)
                            // User requested: ONLY FOR VIEW OVER
                            if (row.Suffix != null && row.Suffix.IndexOf("OVER", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (ViewModel.SelectedViewportTitle != null && ViewModel.SelectedViewportTitle.Id != ElementId.InvalidElementId)
                                {
                                    try { newViewport.ChangeTypeId(ViewModel.SelectedViewportTitle.Id); } catch { }
                                }
                            }


                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        System.Diagnostics.Debug.WriteLine($"AutoViewSheet error on {row.ViewName}: {ex.Message}");
                    }
                }

                // 7. RENAME SHEETS TO TARGET NUMBERS
                foreach (var kvp in createdSheets)
                {
                    try
                    {
                        kvp.Value.SheetNumber = GetUniqueSheetNumber(doc, kvp.Key);
                    }
                    catch { }
                }

                trans.Commit();
            }

            ViewModel.StatusMessage = $"✓ Successfully processed {successCount} rows. ({errorCount} errors)";
        }

        private View CreateView(Document doc, AutoViewSheetRow row)
        {
            if (row.SelectedViewType == null) return null;

            // Find existing view for the level to duplicate (prefer plan views)
            var existingView = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .FirstOrDefault(v => !v.IsTemplate
                    && v.GenLevel != null
                    && v.GenLevel.Id == row.SelectedLevel.Id
                    && v.GetTypeId() == row.SelectedViewType.Id);

            View newView = null;

            if (existingView != null)
            {
                ElementId newViewId = existingView.Duplicate(ViewDuplicateOption.Duplicate);
                newView = doc.GetElement(newViewId) as View;
            }
            else
            {
                newView = ViewPlan.Create(doc, row.SelectedViewType.Id, row.SelectedLevel.Id);
            }

            if (newView != null)
            {
                string newName = GetUniqueViewName(doc, row.ViewName);
                try { newView.Name = newName; } catch { }

                if (row.SelectedViewTemplate != null && row.SelectedViewTemplate.Id != ElementId.InvalidElementId)
                {
                    try { newView.ViewTemplateId = row.SelectedViewTemplate.Id; } catch { }
                }
            }

            return newView;
        }

        private ViewSheet CreateSheet(Document doc, AutoViewSheetRow row, TitleBlockItem titleBlock)
        {
            ViewSheet newSheet = ViewSheet.Create(doc, titleBlock.Id);
            if (newSheet != null)
            {
                // Assign temporary number first to avoid Revit's internal auto-increment collision
                string tempNumber = _tempSheetCounter.ToString();
                while (IsSheetNumberExists(doc, tempNumber))
                {
                    _tempSheetCounter++;
                    tempNumber = _tempSheetCounter.ToString();
                }
                try { newSheet.SheetNumber = tempNumber; } catch { }
                _tempSheetCounter++;

                try { newSheet.Name = string.IsNullOrEmpty(row.SheetName) ? row.SelectedLevel.Name : row.SheetName; } catch { }

                if (!string.IsNullOrEmpty(ViewModel.SelectedSheetSeries))
                {
                    var param = newSheet.LookupParameter("RINCO_TB_SHEET SERIES");
                    if (param != null && !param.IsReadOnly)
                    {
                        param.Set(ViewModel.SelectedSheetSeries);
                    }
                }
            }
            return newSheet;
        }


        private string GetUniqueViewName(Document doc, string baseName)
        {
            string name = baseName;
            int counter = 1;
            while (IsViewNameExists(doc, name))
            {
                name = $"{baseName} ({counter})";
                counter++;
            }
            return name;
        }

        private bool IsViewNameExists(Document doc, string name)
        {
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>();
            return views.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private string GetUniqueSheetNumber(Document doc, string baseNumber)
        {
            string number = baseNumber;
            int counter = 1;
            while (IsSheetNumberExists(doc, number))
            {
                number = $"{baseNumber}.{counter}";
                counter++;
            }
            return number;
        }

        private bool IsSheetNumberExists(Document doc, string number)
        {
            var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
            return sheets.Any(s => s.SheetNumber.Equals(number, StringComparison.OrdinalIgnoreCase));
        }

        public string GetName() => "AutoViewSheetHandler";
    }
}
