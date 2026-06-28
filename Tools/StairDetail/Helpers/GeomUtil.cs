using System;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.StairDetail
{
    public static class GeomUtil
    {
        public static XYZ TransformPoint(XYZ p, Transform t)
        {
            return t.OfPoint(p);
        }

        public static XYZ AddXYZ(XYZ p1, XYZ p2)
        {
            return p1 + p2;
        }

        public static XYZ SubXYZ(XYZ p1, XYZ p2)
        {
            return p1 - p2;
        }

        public static XYZ MultiplyVector(XYZ v, double scalar)
        {
            return v * scalar;
        }

        public static double KhoangCach(XYZ p1, XYZ p2)
        {
            return p1.DistanceTo(p2);
        }

        public static XYZ Middle2Point(XYZ p1, XYZ p2)
        {
            return (p1 + p2) / 2.0;
        }

        public static bool IsSameDirection(XYZ v1, XYZ v2)
        {
            return v1.Normalize().IsAlmostEqualTo(v2.Normalize());
        }

        public static bool IsOppositeDirection(XYZ v1, XYZ v2)
        {
            return v1.Normalize().IsAlmostEqualTo(-v2.Normalize());
        }

        public static bool IsEqual(double d1, double d2)
        {
            return Math.Abs(d1 - d2) < 1e-5;
        }

        public static bool IsEqual(XYZ p1, XYZ p2)
        {
            return p1.IsAlmostEqualTo(p2, 1e-5);
        }
    }
}
