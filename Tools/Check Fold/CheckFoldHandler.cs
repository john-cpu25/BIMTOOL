using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RincoNhan.Tools.CheckFold.Models;

namespace RincoNhan.Tools.CheckFold
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

        // Step items for update operations (set by ViewModel before raising)
        public List<StepCheckItem> ItemsToUpdate { get; set; }

        // Element to select/zoom when clicking a row
        public ElementId ElementIdToSelect { get; set; }

        // Track overridden element IDs for Reset
        private readonly HashSet<ElementId> _overriddenIds = new HashSet<ElementId>();

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

            // Apply overrides
            Color orange = new Color(255, 165, 0);
            int wrongCount = 0;

            using (Transaction tx = new Transaction(doc, "Check Fold - Highlight Wrong RL"))
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
                        CheckFoldLogic.SetElementColorOverride(doc, view, item.StepFamilyId, orange);
                        _overriddenIds.Add(item.StepFamilyId);
                        wrongCount++;
                    }
                }

                tx.Commit();
            }

            OnCheckCompleted?.Invoke(wrongCount);

            NotifyStatus?.Invoke(wrongCount > 0
                ? $"⚠ Tìm thấy {wrongCount} Step sai (đã tô cam)."
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

                    bool success = CheckFoldLogic.SetStepValues(stepFamily, item.CalculatedValue);

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
            NotifyStatus?.Invoke("Đã reset tất cả override màu.");
        }

        private void SelectElement(UIApplication app)
        {
            if (ElementIdToSelect == null) return;

            var uidoc = app.ActiveUIDocument;
            if (uidoc == null) return;

            // Select the element
            uidoc.Selection.SetElementIds(new List<ElementId> { ElementIdToSelect });

            // Zoom to element
            try
            {
                uidoc.ShowElements(ElementIdToSelect);
            }
            catch { /* ShowElements may fail for 2D annotations in 3D views */ }
        }

        public string GetName() => "CheckFoldHandler";
    }
}
