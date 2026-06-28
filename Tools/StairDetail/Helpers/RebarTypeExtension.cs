using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace RincoNhan.Tools.StairDetail
{
    public static class RebarTypeExtension
    {
        public static double GetBarDiameter(this RebarBarType barType)
        {
#if REVIT2022_OR_GREATER
            return barType.BarModelDiameter;
#else
            return barType.BarDiameter;
#endif
        }
    }
}
