using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CreateViewSheet.Models;
using RincoNhan.Tools.CreateViewSheet.ViewModels;

namespace RincoNhan.Tools.CreateViewSheet
{
    public class CreateViewSheetHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        public string RequestAction { get; set; }
        public CreateViewSheetViewModel ViewModel { get; set; }

        public CreateViewSheetHandler()
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
                if (RequestAction == "CREATE_VIEWS")
                {
                    CreateViewsAction(doc);
                }
                else if (RequestAction == "CREATE_SHEETS")
                {
                    CreateSheetsAction(doc);
                }
                else if (RequestAction == "ADD_VIEWS_TO_SHEETS")
                {
                    AddViewsToSheetsAction(doc);
                }
                else if (RequestAction == "ALIGN_VIEWS")
                {
                    AlignViewsAction(doc);
                }
                else if (RequestAction == "REFRESH_DATA")
                {
                    ViewModel.RefreshAllData(doc);
                }
            }
            catch (Exception ex)
            {
                ViewModel.SetStatus($"Error: {ex.Message}");
            }
        }

        // ================= Tab 1: Create Views =================
        private void CreateViewsAction(Document doc)
        {
            var rowsToProcess = ViewModel.CreateViewRows.Where(r => r.IsSelected && r.SelectedLevel != null).ToList();
            if (!rowsToProcess.Any())
            {
                ViewModel.SetStatus("No valid rows selected to create views.");
                return;
            }

            int createdCount = 0;
            int errorCount = 0;

            using (Transaction trans = new Transaction(doc, "Rinco - Create Views"))
            {
                trans.Start();

                foreach (var row in rowsToProcess)
                {
                    if (row.SelectedViewType == null) continue;

                    try
                    {
                        // Find existing view for the level to duplicate
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
                            ViewDuplicateOption dupOpt = ViewDuplicateOption.Duplicate;
                            if (row.DuplicateMode == DuplicateMode.DuplicateWithDetailing)
                                dupOpt = ViewDuplicateOption.WithDetailing;
                            else if (row.DuplicateMode == DuplicateMode.DuplicateAsDependent)
                                dupOpt = ViewDuplicateOption.AsDependent;

                            ElementId newViewId = existingView.Duplicate(dupOpt);
                            newView = doc.GetElement(newViewId) as View;
                        }
                        else
                        {
                            // No existing view to duplicate, create a new one
                            newView = ViewPlan.Create(doc, row.SelectedViewType.Id, row.SelectedLevel.Id);
                        }

                        if (newView != null)
                        {
                            // Build view name: "LEVEL NAME SUFFIX" (e.g. "GROUND - OVER")
                            string suffix = string.IsNullOrWhiteSpace(row.Suffix) ? "" : $" {row.Suffix}";
                            string newName = $"{row.SelectedLevel.Name}{suffix}";
                            newName = GetUniqueViewName(doc, newName);
                            newView.Name = newName;

                            if (row.SelectedViewTemplate != null && row.SelectedViewTemplate.Id != ElementId.InvalidElementId)
                            {
                                newView.ViewTemplateId = row.SelectedViewTemplate.Id;
                            }

                            createdCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        System.Diagnostics.Debug.WriteLine($"CreateView error: {ex.Message}");
                    }
                }

                trans.Commit();
            }

            string msg = $"✓ Created {createdCount} views.";
            if (errorCount > 0) msg += $" ({errorCount} errors skipped)";
            ViewModel.SetStatus(msg);
            ViewModel.RefreshAllData(doc);
        }

        // ================= Tab 2: Create Sheets =================
        private void CreateSheetsAction(Document doc)
        {
            var rowsToProcess = ViewModel.CreateSheetRows.Where(r => r.IsSelected && r.SelectedLevel != null).ToList();
            if (!rowsToProcess.Any() || ViewModel.SelectedTitleBlock == null)
            {
                ViewModel.SetStatus("No valid rows selected or Titleblock missing.");
                return;
            }

            int createdCount = 0;
            int setCount = 0;

            using (Transaction trans = new Transaction(doc, "Rinco - Create Sheets"))
            {
                trans.Start();

                foreach (var row in rowsToProcess)
                {
                    try
                    {
                        ViewSheet newSheet = ViewSheet.Create(doc, ViewModel.SelectedTitleBlock.Id);
                        if (newSheet != null)
                        {
                            if (!string.IsNullOrEmpty(row.SheetNumber))
                            {
                                try { newSheet.SheetNumber = row.SheetNumber; } catch { }
                            }

                            newSheet.Name = string.IsNullOrEmpty(row.SheetName) ? row.SelectedLevel.Name : row.SheetName;

                            // Set Sheet Series parameter directly on ViewSheet
                            if (!string.IsNullOrEmpty(ViewModel.SelectedSheetSeries))
                            {
                                var param = newSheet.LookupParameter("RINCO_TB_SHEET SERIES");
                                if (param != null && !param.IsReadOnly)
                                {
                                    param.Set(ViewModel.SelectedSheetSeries);
                                    setCount++;
                                }
                            }

                            createdCount++;
                        }
                    }
                    catch { }
                }

                trans.Commit();
            }

            ViewModel.SetStatus($"✓ Created {createdCount} sheets. (Series set: {setCount})");
            ViewModel.RefreshAllData(doc);
        }

        // ================= Tab 3: Add Views to Sheets =================
        private void AddViewsToSheetsAction(Document doc)
        {
            var selectedViews = ViewModel.ProjectViews.Where(v => v.IsSelected).ToList();
            var selectedSheets = ViewModel.ProjectSheets.Where(s => s.IsSelected).ToList();

            if (!selectedViews.Any() || !selectedSheets.Any())
            {
                ViewModel.SetStatus("Select at least one view and one sheet.");
                return;
            }

            int addedCount = 0;

            using (Transaction trans = new Transaction(doc, "Rinco - Add Views to Sheets"))
            {
                trans.Start();

                foreach (var sheetItem in selectedSheets)
                {
                    var sheet = doc.GetElement(sheetItem.Id) as ViewSheet;
                    if (sheet == null) continue;

                    // Place at center of sheet (typical A1 sheet ~841mm x 594mm → ~1.38ft x 0.975ft)
                    XYZ placementPt = new XYZ(1.38, 0.975, 0);
                    double offset = 0;

                    foreach (var viewItem in selectedViews)
                    {
                        try
                        {
                            if (Viewport.CanAddViewToSheet(doc, sheet.Id, viewItem.Id))
                            {
                                XYZ pt = ViewModel.StackViews
                                    ? placementPt
                                    : new XYZ(placementPt.X + offset, placementPt.Y, 0);

                                Viewport.Create(doc, sheet.Id, viewItem.Id, pt);
                                addedCount++;
                                offset += 0.5;
                            }
                        }
                        catch { }
                    }
                }

                trans.Commit();
            }

            ViewModel.SetStatus($"✓ Added {addedCount} views to sheets.");
            ViewModel.RefreshAllData(doc);
        }

        // ================= Tab 3: Align Views =================
        private void AlignViewsAction(Document doc)
        {
            if (ViewModel.TemplateViewForAlign == null)
            {
                ViewModel.SetStatus("Please select a Template View to align to.");
                return;
            }

            var selectedSheets = ViewModel.ProjectSheets.Where(s => s.IsSelected).ToList();
            if (!selectedSheets.Any())
            {
                ViewModel.SetStatus("Select at least one target sheet to align views in.");
                return;
            }

            // Find the Viewport for the template view
            Viewport templateViewport = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .FirstOrDefault(vp => vp.ViewId == ViewModel.TemplateViewForAlign.Id);

            if (templateViewport == null)
            {
                ViewModel.SetStatus("Template view is not placed on any sheet.");
                return;
            }

            XYZ targetCenter = templateViewport.GetBoxCenter();
            int alignedCount = 0;

            using (Transaction trans = new Transaction(doc, "Rinco - Align Views"))
            {
                trans.Start();

                foreach (var sheetItem in selectedSheets)
                {
                    var sheet = doc.GetElement(sheetItem.Id) as ViewSheet;
                    if (sheet == null) continue;

                    var viewportsOnSheet = sheet.GetAllViewports();
                    foreach (var vpId in viewportsOnSheet)
                    {
                        var vp = doc.GetElement(vpId) as Viewport;
                        if (vp == null) continue;

                        // Don't align legends
                        var view = doc.GetElement(vp.ViewId) as View;
                        if (view != null && view.ViewType != ViewType.Legend)
                        {
                            vp.SetBoxCenter(targetCenter);
                            alignedCount++;
                        }
                    }
                }

                trans.Commit();
            }

            ViewModel.SetStatus($"✓ Aligned {alignedCount} views on selected sheets.");
        }

        // ================= Helpers =================
        private string GetUniqueViewName(Document doc, string baseName)
        {
            var existingNames = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!existingNames.Contains(baseName)) return baseName;

            int count = 1;
            while (existingNames.Contains($"{baseName} ({count})"))
            {
                count++;
            }
            return $"{baseName} ({count})";
        }

        public string GetName() => "CreateViewSheetHandler";
    }
}
