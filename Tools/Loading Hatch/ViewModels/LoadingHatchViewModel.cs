using System;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Tools.LoadingHatch.Models;

namespace RincoNhan.Tools.LoadingHatch.ViewModels
{
    public class LoadingHatchViewModel
    {
        public ObservableCollection<LoadingHatchItem> Items { get; set; }

        public LoadingHatchViewModel(Document doc, View activeView)
        {
            Items = new ObservableCollection<LoadingHatchItem>();
            LoadData(doc, activeView);
        }

        private void LoadData(Document doc, View activeView)
        {
            // Get all filled regions in the current view
            var filledRegions = new FilteredElementCollector(doc, activeView.Id)
                .OfClass(typeof(FilledRegion))
                .Cast<FilledRegion>()
                .ToList();

            // Group by Type Id
            var groupedRegions = filledRegions.GroupBy(fr => fr.GetTypeId());

            foreach (var group in groupedRegions)
            {
                var typeId = group.Key;
                var regionType = doc.GetElement(typeId) as FilledRegionType;
                
                if (regionType == null) continue;

                // Extract Type Parameters
                string typeName = regionType.Name;
                string typeMark = "";
                
                var markParam = regionType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                if (markParam != null && markParam.HasValue)
                {
                    typeMark = markParam.AsString();
                }

                // Get patterns and colors for additional info
                string foregroundPattern = "";
                var foregroundPatternId = regionType.ForegroundPatternId;
                if (foregroundPatternId != ElementId.InvalidElementId)
                {
                    var patternElem = doc.GetElement(foregroundPatternId) as FillPatternElement;
                    if (patternElem != null) foregroundPattern = patternElem.Name;
                }

                string backgroundPattern = "";
                var backgroundPatternId = regionType.BackgroundPatternId;
                if (backgroundPatternId != ElementId.InvalidElementId)
                {
                    var patternElem = doc.GetElement(backgroundPatternId) as FillPatternElement;
                    if (patternElem != null) backgroundPattern = patternElem.Name;
                }
                
                string colorStr = "";
                var color = regionType.ForegroundPatternColor;
                if (color != null && color.IsValid)
                {
                    colorStr = $"{color.Red}, {color.Green}, {color.Blue}";
                }

                Items.Add(new LoadingHatchItem
                {
                    TypeName = typeName,
                    TypeMark = typeMark,
                    ForegroundPattern = foregroundPattern,
                    BackgroundPattern = backgroundPattern,
                    Color = colorStr,
                    Count = group.Count()
                });
            }
        }
    }
}
