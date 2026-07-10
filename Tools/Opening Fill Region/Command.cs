using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.OpeningFillRegion
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                View activeView = uidoc.ActiveView;
                if (activeView == null || (activeView.ViewType != ViewType.FloorPlan
                    && activeView.ViewType != ViewType.CeilingPlan
                    && activeView.ViewType != ViewType.EngineeringPlan
                    && activeView.ViewType != ViewType.AreaPlan))
                {
                    TaskDialog.Show("Lỗi", "Vui lòng mở một Plan View để sử dụng tool này.");
                    return Result.Failed;
                }

                // 1. Chọn Shaft Openings
                IList<Reference> shaftRefs;
                try
                {
                    shaftRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new ShaftOpeningSelectionFilter(),
                        "Chọn Shaft Opening(s)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (shaftRefs == null || shaftRefs.Count == 0)
                {
                    TaskDialog.Show("Thông báo", "Không có Shaft Opening nào được chọn.");
                    return Result.Cancelled;
                }

                // 2. Chọn Filled Regions
                IList<Reference> filledRegionRefs;
                try
                {
                    filledRegionRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new FilledRegionSelectionFilter(),
                        "Chọn Filled Region(s)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (filledRegionRefs == null || filledRegionRefs.Count == 0)
                {
                    TaskDialog.Show("Thông báo", "Không có Filled Region nào được chọn.");
                    return Result.Cancelled;
                }

                // 3. Thực hiện đục lỗ
                List<Element> shaftOpenings = shaftRefs
                    .Select(r => doc.GetElement(r))
                    .Where(e => e != null)
                    .ToList();

                List<FilledRegion> filledRegions = filledRegionRefs
                    .Select(r => doc.GetElement(r))
                    .OfType<FilledRegion>()
                    .ToList();

                int successCount = OpeningFillRegionLogic.Execute(doc, uidoc, activeView, shaftOpenings, filledRegions);

                TaskDialog.Show("Hoàn tất", $"Đã xử lý thành công {successCount}/{filledRegions.Count} Filled Region(s).");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "An error occurred:\n\n" + ex.ToString());
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Selection filter cho Shaft Opening (BuiltInCategory.OST_ShaftOpening)
    /// </summary>
    public class ShaftOpeningSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null
                && elem.Category.Id.GetIdValue() == (int)BuiltInCategory.OST_ShaftOpening;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    /// <summary>
    /// Selection filter cho Filled Region
    /// </summary>
    public class FilledRegionSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is FilledRegion;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
