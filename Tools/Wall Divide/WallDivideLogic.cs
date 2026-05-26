using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.WallDivide
{
    public static class WallDivideLogic
    {
        public const double CONCRETE_DENSITY = 2.5; // ton/m3
        public const double MAX_WEIGHT = 15.0;      // tons
        public const double MAX_VOLUME = 6.0;      // m3 (15 / 2.5)

        // Bảng tra: Key là Height limit (mm), Value là Max Length (mm)
        private static readonly SortedDictionary<double, double> DimensionTable = new SortedDictionary<double, double>
        {
            { 2700, 12500 },
            { 3200, 10600 },
            { 3700, 6600 },
            { 4000, 4000 }
        };

        public static double GetMaxLength(double heightMm)
        {
            foreach (var entry in DimensionTable)
            {
                if (heightMm <= entry.Key) return entry.Value;
            }
            return -1; // Vượt quá 4000mm
        }

        public static bool CheckIfNeedsDivision(double heightMm, double lengthMm, double volumeM3)
        {
            // CHỈ chia khi vượt quá 15 tấn (tương đương 6m3 với density 2.5)
            if (volumeM3 > MAX_VOLUME) return true;
            
            return false; 
        }

        public static int CalculateSuggestedParts(double heightMm, double lengthMm, double volumeM3)
        {
            int partsByWeight = (int)Math.Ceiling(volumeM3 / MAX_VOLUME);
            
            double maxL = GetMaxLength(heightMm);
            if (maxL < 0) return 1; // Manual selection needed

            int partsByDim = (int)Math.Ceiling(lengthMm / maxL);

            return Math.Max(partsByWeight, partsByDim);
        }

    }
}
