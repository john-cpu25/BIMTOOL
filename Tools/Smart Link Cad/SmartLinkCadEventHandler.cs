using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using View = Autodesk.Revit.DB.View;

namespace RincoNhan.Tools.SmartLinkCad
{
    /// <summary>
    /// Handles all Revit API transactions for Smart Link Cad modeless window.
    /// </summary>
    public class SmartLinkCadEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;

        // Action to execute
        public string RequestAction { get; set; }

        // Shared data from window
        public Document Doc { get; set; }
        public View TargetView { get; set; }
        public List<CADCategoryInfo> SelectedCads { get; set; }
        public List<CADLayerInfo> AllLayers { get; set; }
        public List<CADLayerInfo> TargetLayers { get; set; }
        public Autodesk.Revit.DB.Color OverrideColor { get; set; }
        public bool Halftone { get; set; }
        public int WeightIndex { get; set; }
        public ElementId PatternId { get; set; }

        // Callback to update UI status
        public Action<string> StatusCallback { get; set; }

        public SmartLinkCadEventHandler()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        public void Raise() => _externalEvent.Raise();

        public void Execute(UIApplication app)
        {
            try
            {
                if (RequestAction == "BATCH_OVERRIDE")
                {
                    BatchOverrideAction();
                }
                else if (RequestAction == "APPLY_VISIBILITY")
                {
                    ApplyVisibilityAction();
                }
                else if (RequestAction == "LAYER_OVERRIDE")
                {
                    LayerOverrideAction();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Smart Link CAD - Error", ex.Message);
            }
        }

        private void BatchOverrideAction()
        {
            if (TargetView == null || SelectedCads == null || SelectedCads.Count == 0) return;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(OverrideColor);
            ogs.SetHalftone(Halftone);

            if (WeightIndex > 0)
                ogs.SetProjectionLineWeight(WeightIndex);

            if (PatternId != null && PatternId != ElementId.InvalidElementId)
                ogs.SetProjectionLinePatternId(PatternId);

            using (Transaction t = new Transaction(Doc, "Smart Link Cad Override"))
            {
                t.Start();

                foreach (var cadInfo in SelectedCads)
                {
                    Category rootCat = cadInfo.Category;

                    if (TargetView.AreGraphicsOverridesAllowed())
                    {
                        TargetView.SetCategoryOverrides(rootCat.Id, ogs);
                    }

                    foreach (Category subCat in rootCat.SubCategories)
                    {
                        if (TargetView.AreGraphicsOverridesAllowed())
                        {
                            TargetView.SetCategoryOverrides(subCat.Id, ogs);
                        }
                    }
                }

                t.Commit();
            }

            StatusCallback?.Invoke($"✅ Override applied to {SelectedCads.Count} CAD files.");
        }

        private void ApplyVisibilityAction()
        {
            if (TargetView == null || AllLayers == null) return;

            int successCount = 0;
            int errorCount = 0;

            using (Transaction t = new Transaction(Doc, "Smart Link Cad - Layer Visibility"))
            {
                t.Start();

                foreach (var layer in AllLayers)
                {
                    if (layer.SubCategory == null) continue;

                    try
                    {
                        TargetView.SetCategoryHidden(layer.SubCategory.Id, !layer.IsVisible);
                        successCount++;
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

                t.Commit();
            }

            string msg = $"✅ Visibility applied: {successCount} layers.";
            if (errorCount > 0)
                msg += $" ⚠️ {errorCount} errors.";

            StatusCallback?.Invoke(msg);
        }

        private void LayerOverrideAction()
        {
            if (TargetView == null || TargetLayers == null || TargetLayers.Count == 0) return;

            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(OverrideColor);
            ogs.SetHalftone(Halftone);

            if (WeightIndex > 0)
                ogs.SetProjectionLineWeight(WeightIndex);

            if (PatternId != null && PatternId != ElementId.InvalidElementId)
                ogs.SetProjectionLinePatternId(PatternId);

            using (Transaction t = new Transaction(Doc, "Smart Link Cad - Layer Override"))
            {
                t.Start();

                foreach (var layer in TargetLayers)
                {
                    if (layer.SubCategory == null) continue;

                    if (TargetView.AreGraphicsOverridesAllowed())
                    {
                        TargetView.SetCategoryOverrides(layer.SubCategory.Id, ogs);
                    }
                }

                t.Commit();
            }

            StatusCallback?.Invoke($"✅ Override applied to {TargetLayers.Count} layers.");
        }

        public string GetName() => "SmartLinkCadEventHandler";
    }
}
