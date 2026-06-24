using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoModeling.Tools.CheckFold.Models;

namespace RincoModeling.Tools.CheckFold
{
    /// <summary>
    /// IExternalEventHandler for all Revit API calls from modeless window.
    /// Ensures thread safety by running on Revit's main thread.
    /// </summary>
    public class CheckFoldHandler : IExternalEventHandler
    {
        // Current action to execute
        public string Action { get; set; }

        // Callbacks to UI thread
        public Action<List<FoldCheckItem>> OnFoldDataLoaded { get; set; }
        public Action<List<StepCheckItem>> OnStepDataLoaded { get; set; }
        public Action<string> NotifyStatus { get; set; }
        public Action<int> OnCheckCompleted { get; set; }
        public Action<int, int> OnUpdateCompleted { get; set; }
        public Action OnResetCompleted { get; set; }
        
        public Action<List<MissingStepItem>> OnMissingStepsChecked { get; set; }
        public Action OnHighlightsCleared { get; set; }

        // Step items for update operations (set by ViewModel before raising)
        public List<StepCheckItem> ItemsToUpdate { get; set; }

        // Element to select/zoom when clicking a row
        public ElementId ElementIdToSelect { get; set; }

        // Track overridden element IDs for Reset
        private readonly HashSet<ElementId> _overriddenIds = new HashSet<ElementId>();
        private readonly HashSet<ElementId> _highlightLineIds = new HashSet<ElementId>();

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                View view = doc.ActiveView;

                switch (Action)
                {
                    case "LoadData":
                        LoadAllData(doc, view);
                        break;
                    case "CheckSteps":
                        CheckSteps(doc, view);
                        break;
                    case "UpdateSteps":
                        UpdateSteps(doc, view);
                        break;
                    case "ResetOverrides":
                        ResetOverrides(doc, view);
                        break;
                    case "SelectElement":
                        SelectElement(app);
                        break;
                    case "CheckMissingSteps":
                        CheckMissingSteps(doc, view);
                        break;
                }
            }
            catch (Exception ex)
            {
                NotifyStatus?.Invoke("Error: " + ex.Message);
            }
        }

        private void LoadAllData(Document doc, View view)
        {
            // Load fold data
            var foldFloors = CheckFoldLogic.GetFoldFloors(doc, view);
            var nonFoldFloors = CheckFoldLogic.GetNonFoldFloors(doc, view);

            var foldItems = new List<FoldCheckItem>();
            foreach (var fold in foldFloors)
            {
                foldItems.Add(CheckFoldLogic.BuildFoldCheckItem(doc, fold, nonFoldFloors));
            }

            // Load step data - pass ALL floors (fold + non-fold) for step detection
            var allFloors = new List<Floor>(foldFloors);
            allFloors.AddRange(nonFoldFloors);
            var stepFamilies = CheckFoldLogic.GetStepFamilies(doc, view);
            var stepItems = new List<StepCheckItem>();
            foreach (var step in stepFamilies)
            {
                stepItems.Add(CheckFoldLogic.BuildStepCheckItem(doc, step, allFloors));
            }

            // Dispatch results to UI
            OnFoldDataLoaded?.Invoke(foldItems);
            OnStepDataLoaded?.Invoke(stepItems);

            int mismatch = foldItems.Count(i => i.Status == "Mismatch");
            if (foldItems.Count == 0)
                NotifyStatus?.Invoke("Không tìm thấy sàn fold nào trong view hiện tại.");
            else
                NotifyStatus?.Invoke($"Tìm thấy {foldItems.Count} sàn fold, {stepItems.Count} step. {mismatch} mismatch.");
        }

        private void CheckSteps(Document doc, View view)
        {
            var foldFloors = CheckFoldLogic.GetFoldFloors(doc, view);
            var nonFoldFloors = CheckFoldLogic.GetNonFoldFloors(doc, view);
            var allFloors = new List<Floor>(foldFloors);
            allFloors.AddRange(nonFoldFloors);
            var stepFamilies = CheckFoldLogic.GetStepFamilies(doc, view);

            // Build fresh step data
            var stepItems = new List<StepCheckItem>();
            foreach (var step in stepFamilies)
            {
                stepItems.Add(CheckFoldLogic.BuildStepCheckItem(doc, step, allFloors));
            }

            // Update step data on UI
            OnStepDataLoaded?.Invoke(stepItems);

            int wrongCount = 0;
            Color red = new Color(255, 0, 0);

            using (Transaction tx = new Transaction(doc, "Check Fold - Highlight Sai Steps"))
            {
                tx.Start();

                // Reset previous overrides
                foreach (var id in _overriddenIds)
                {
                    CheckFoldLogic.ResetElementOverride(view, id);
                }
                _overriddenIds.Clear();

                foreach (var item in stepItems)
                {
                    if (item.Status == "Sai")
                    {
                        CheckFoldLogic.SetElementColorOverride(doc, view, item.StepFamilyId, red);
                        _overriddenIds.Add(item.StepFamilyId);
                        wrongCount++;
                    }
                }

                tx.Commit();
            }

            OnCheckCompleted?.Invoke(wrongCount);

            NotifyStatus?.Invoke(wrongCount > 0
                ? $"⚠ Tìm thấy {wrongCount} Step sai (đã tô đỏ)."
                : "✓ Tất cả Height Offset đều đúng!");
        }

        private void UpdateSteps(Document doc, View view)
        {
            if (ItemsToUpdate == null || !ItemsToUpdate.Any())
            {
                NotifyStatus?.Invoke("Không có RL STEP nào cần cập nhật.");
                return;
            }

            Color green = new Color(0, 200, 80);
            int updated = 0;
            int failed = 0;

            using (Transaction tx = new Transaction(doc, "Check Fold - Update RL STEP"))
            {
                tx.Start();

                foreach (var item in ItemsToUpdate)
                {
                    var stepFamily = doc.GetElement(item.StepFamilyId) as FamilyInstance;
                    if (stepFamily == null) { failed++; continue; }

                    bool success = CheckFoldLogic.SetStepValues(stepFamily, item.CalculatedValue, item.IsVaries);

                    if (success)
                    {
                        // Override color to green
                        CheckFoldLogic.SetElementColorOverride(doc, view, item.StepFamilyId, green);
                        _overriddenIds.Add(item.StepFamilyId);
                        updated++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                tx.Commit();
            }

            OnUpdateCompleted?.Invoke(updated, failed);

            string msg = $"✓ Đã cập nhật {updated} Step thành công (đã tô xanh lá).";
            if (failed > 0) msg += $" {failed} thất bại.";
            NotifyStatus?.Invoke(msg);
        }

        private void ResetOverrides(Document doc, View view)
        {
            using (Transaction tx = new Transaction(doc, "Check Fold - Reset Overrides"))
            {
                tx.Start();

                foreach (var id in _overriddenIds)
                {
                    CheckFoldLogic.ResetElementOverride(view, id);
                }
                _overriddenIds.Clear();

                tx.Commit();
            }

            OnResetCompleted?.Invoke();
            NotifyStatus?.Invoke("Đã reset tất cả màu tô đỏ/xanh.");
        }

        private void SelectElement(UIApplication app)
        {
            if (ElementIdToSelect == null) return;

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;
            var doc = uidoc.Document;

            // Select the element
            uidoc.Selection.SetElementIds(new List<ElementId> { ElementIdToSelect });

            // Zoom to element
            try
            {
                var element = doc.GetElement(ElementIdToSelect);
                if (element != null)
                {
                    // Switch to element's view if it is view-specific
                    if (element.OwnerViewId != ElementId.InvalidElementId && element.OwnerViewId != uidoc.ActiveView.Id)
                    {
                        var ownerView = doc.GetElement(element.OwnerViewId) as View;
                        if (ownerView != null)
                        {
                            uidoc.ActiveView = ownerView;
                        }
                    }

                    // Zoom with a bounding box for better framing
                    var bb = element.get_BoundingBox(uidoc.ActiveView);
                    if (bb != null)
                    {
                        var uiview = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == uidoc.ActiveView.Id);
                        if (uiview != null)
                        {
                            double offset = 5.0; // 5 feet
                            XYZ min = new XYZ(bb.Min.X - offset, bb.Min.Y - offset, bb.Min.Z - offset);
                            XYZ max = new XYZ(bb.Max.X + offset, bb.Max.Y + offset, bb.Max.Z + offset);
                            uiview.ZoomAndCenterRectangle(min, max);
                            return;
                        }
                    }
                }

                uidoc.ShowElements(ElementIdToSelect);
            }
            catch { /* Fallback fail gracefully */ }
        }

        private void CheckMissingSteps(Document doc, View view)
        {
            var missingSteps = CheckFoldLogic.FindMissing3DSteps(doc, view);

            if (missingSteps.Count > 0)
            {
                using (Transaction tx = new Transaction(doc, "Check Fold - Highlight Missing Steps"))
                {
                    tx.Start();

                    // Clear previous lines
                    foreach (var id in _highlightLineIds)
                    {
                        try { doc.Delete(id); } catch { }
                    }
                    _highlightLineIds.Clear();

                    var leftoverLines = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(CurveElement))
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .Where(e => 
                        {
                            var p = e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                            return p != null && p.AsString() == "CheckFold_MissingStep";
                        })
                        .Select(e => e.Id)
                        .ToList();

                    foreach (var id in leftoverLines)
                    {
                        try { doc.Delete(id); } catch { }
                    }

                    tx.Commit();
                }
            }

            OnMissingStepsChecked?.Invoke(missingSteps);
            NotifyStatus?.Invoke(missingSteps.Count > 0
                ? $"⚠ Tìm thấy {missingSteps.Count} vị trí thiếu Step (đã vẽ line đỏ)."
                : "✓ Tuyệt vời! Không phát hiện vị trí nào thiếu Step.");
        }

        public string GetName() => "CheckFoldHandler";
    }
}

