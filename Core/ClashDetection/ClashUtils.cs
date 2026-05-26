using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RincoNhan.Core.ClashDetection
{
    public static class ClashUtils
    {
        public static List<Solid> GetSolids(Element element)
        {
            List<Solid> solids = new List<Solid>();
            Options options = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geomElem = element.get_Geometry(options);

            if (geomElem == null) return solids;

            ParseGeometry(geomElem, solids);
            return solids;
        }

        private static void ParseGeometry(GeometryElement geomElem, List<Solid> solids)
        {
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    solids.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    ParseGeometry(geomInst.GetInstanceGeometry(), solids);
                }
            }
        }

        public static BoundingBoxXYZ GetAggregateBoundingBox(Element element)
        {
            return element.get_BoundingBox(null);
        }

        public static BoundingBoxXYZ ExpandBoundingBox(BoundingBoxXYZ bbox, double offsetFeet)
        {
            BoundingBoxXYZ newBbox = new BoundingBoxXYZ();
            newBbox.Min = new XYZ(bbox.Min.X - offsetFeet, bbox.Min.Y - offsetFeet, bbox.Min.Z - offsetFeet);
            newBbox.Max = new XYZ(bbox.Max.X + offsetFeet, bbox.Max.Y + offsetFeet, bbox.Max.Z + offsetFeet);
            return newBbox;
        }
    }
}
