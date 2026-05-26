using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.WallDivide
{
    public class WallData
    {
        public ElementId Id { get; set; }
        public ElementId GroupId { get; set; }
        public string Name { get; set; }
        public double Height { get; set; } // mm
        public double Length { get; set; } // mm
        public double Volume { get; set; } // m3
        public double Weight { get; set; } // ton
        public bool HasHostedElements { get; set; }
        public bool NeedsDivision { get; set; }
        public int SuggestedParts { get; set; }
    }

    public class RevitDataCollector
    {
        private Document _doc;

        public RevitDataCollector(Document doc)
        {
            _doc = doc;
        }

        public List<WallData> GetWallsFromGroup(Group group)
        {
            List<WallData> results = new List<WallData>();
            var memberIds = group.GetMemberIds();

            foreach (ElementId id in memberIds)
            {
                Element elem = _doc.GetElement(id);
                if (elem is Wall wall)
                {
                    results.Add(ExtractWallData(wall));
                }
            }
            return results;
        }

        /// <summary>
        /// Extracts data for a single wall. 
        /// Note: BuiltInParameter.HOST_VOLUME_COMPUTED already subtracts opening volumes (doors/windows).
        /// </summary>
        public WallData ExtractWallData(Wall wall)
        {
            // Convert internal units (feet) to mm/m3
            double heightFt = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double lengthFt = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
            double volumeCuFt = wall.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED).AsDouble();

            double heightMm = UnitUtils.ConvertFromInternalUnits(heightFt, UnitTypeId.Millimeters);
            double lengthMm = UnitUtils.ConvertFromInternalUnits(lengthFt, UnitTypeId.Millimeters);
            double volumeM3 = UnitUtils.ConvertFromInternalUnits(volumeCuFt, UnitTypeId.CubicMeters);

            // Check hosted elements (doors/windows)
            var hosted = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(e => e.Host != null && e.Host.Id == wall.Id)
                .Any();

            bool needsDiv = WallDivideLogic.CheckIfNeedsDivision(heightMm, lengthMm, volumeM3);
            int suggested = WallDivideLogic.CalculateSuggestedParts(heightMm, lengthMm, volumeM3);

            return new WallData
            {
                Id = wall.Id,
                GroupId = wall.GroupId,
                Name = wall.Name,
                Height = Math.Round(heightMm, 1),
                Length = Math.Round(lengthMm, 1),
                Volume = Math.Round(volumeM3, 3),
                Weight = Math.Round(volumeM3 * WallDivideLogic.CONCRETE_DENSITY, 2),
                HasHostedElements = hosted, // Still keep for info if needed, but UI will ignore
                NeedsDivision = needsDiv,
                SuggestedParts = suggested
            };
        }

        public double GetFloorThickness(Floor floor)
        {
            Parameter thickParam = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            if (thickParam == null)
            {
                // Try from type
                FloorType type = _doc.GetElement(floor.GetTypeId()) as FloorType;
                thickParam = type.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
            }

            if (thickParam != null)
            {
                return UnitUtils.ConvertFromInternalUnits(thickParam.AsDouble(), UnitTypeId.Millimeters);
            }
            return 0;
        }
    }
}
