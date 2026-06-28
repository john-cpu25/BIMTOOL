using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.StairDetail
{
    public static class HelperFixes
    {
        public static IndependentTag LeaderEnd(this IndependentTag tag, XYZ point)
        {
            tag.HasLeader = true;
#if REVIT2022_OR_GREATER
            if (tag.GetTaggedReferences().Count > 0)
            {
                tag.SetLeaderEnd(tag.GetTaggedReferences().First(), point);
            }
#else
            tag.LeaderEnd = point;
#endif
            return tag;
        }

        public static XYZ LeaderEnd(this IndependentTag tag)
        {
#if REVIT2022_OR_GREATER
            if (tag.GetTaggedReferences().Count > 0)
            {
                return tag.GetLeaderEnd(tag.GetTaggedReferences().First());
            }
            return XYZ.Zero;
#else
            return tag.LeaderEnd;
#endif
        }

        public static IndependentTag LeaderElbow(this IndependentTag tag, XYZ point)
        {
            tag.HasLeader = true;
#if REVIT2022_OR_GREATER
            if (tag.GetTaggedReferences().Count > 0)
            {
                tag.SetLeaderElbow(tag.GetTaggedReferences().First(), point);
            }
#else
            tag.LeaderElbow = point;
#endif
            return tag;
        }

        public static XYZ LeaderElbow(this IndependentTag tag)
        {
#if REVIT2022_OR_GREATER
            if (tag.GetTaggedReferences().Count > 0)
            {
                return tag.GetLeaderElbow(tag.GetTaggedReferences().First());
            }
            return XYZ.Zero;
#else
            return tag.LeaderElbow;
#endif
        }

        public static XYZ ProjectOnPlane(XYZ point, Plane plane)
        {
            double distance = plane.Normal.DotProduct(point - plane.Origin);
            return point - distance * plane.Normal;
        }
    }
    
    public static partial class TinhToan
    {
        public static XYZ GiaoDiem(Curve cv1, Curve cv2)
        {
            IntersectionResultArray results;
            SetComparisonResult result = cv1.Intersect(cv2, out results);
            if (result == SetComparisonResult.Overlap && results != null && !results.IsEmpty)
            {
                return results.get_Item(0).XYZPoint;
            }
            return null;
        }
    }

    public class FamilyLoadOption : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}


