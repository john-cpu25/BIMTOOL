using System;
using System.Collections.Generic;

namespace RincoNhan.Tools.AutoPT
{
    public class PTPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PTPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X} , {Y})";
        }
    }

    public class PTHighPoint : PTPoint
    {
        public string Condition { get; set; } // "end" or "continuous"

        public PTHighPoint(double x, double y, string condition) : base(x, y)
        {
            Condition = condition;
        }
    }

    public static class PTProfileCalculator
    {
        private static double Sq(double x) => x * x;

        public static int RoundNearest5(double x)
        {
            return (int)(5 * Math.Round(x / 5.0));
        }

        public static double Length2Pts(PTPoint pt1, PTPoint pt2)
        {
            return Math.Sqrt(Sq(pt1.X - pt2.X) + Sq(pt1.Y - pt2.Y));
        }

        private static double[][] AMatrix(PTPoint pt1, PTPoint pt2, PTPoint pt3)
        {
            return new double[][]
            {
                new double[] { Sq(pt1.X), pt1.X, 1.0 },
                new double[] { Sq(pt2.X), pt2.X, 1.0 },
                new double[] { Sq(pt3.X), pt3.X, 1.0 }
            };
        }

        private static double[] BMatrix(PTPoint pt1, PTPoint pt2, PTPoint pt3)
        {
            return new double[] { pt1.Y, pt2.Y, pt3.Y };
        }

        public static double[] Solve3x3(double[][] A, double[] B)
        {
            // Cofactor matrix C
            double[][] C = new double[][]
            {
                new double[] { (A[1][1] * A[2][2] - A[1][2] * A[2][1]), -(A[1][0] * A[2][2] - A[2][0] * A[1][2]), (A[1][0] * A[2][1] - A[2][0] * A[1][1]) },
                new double[] { -(A[0][1] * A[2][2] - A[2][1] * A[0][2]), (A[0][0] * A[2][2] - A[2][0] * A[0][2]), -(A[0][0] * A[2][1] - A[2][0] * A[0][1]) },
                new double[] { (A[0][1] * A[1][2] - A[1][1] * A[0][2]), -(A[0][0] * A[1][2] - A[1][0] * A[0][2]), (A[0][0] * A[1][1] - A[1][0] * A[0][1]) }
            };

            // Determinant D
            double D = A[0][0] * A[1][1] * A[2][2] + A[0][1] * A[1][2] * A[2][0] + A[0][2] * A[1][0] * A[2][1] -
                       (A[0][1] * A[1][0] * A[2][2] + A[0][0] * A[1][2] * A[2][1] + A[0][2] * A[1][1] * A[2][0]);

            double DI = 1.0 / D;

            // Inverse AI
            double[][] AI = new double[][]
            {
                new double[] { DI * C[0][0], DI * C[1][0], DI * C[2][0] },
                new double[] { DI * C[0][1], DI * C[1][1], DI * C[2][1] },
                new double[] { DI * C[0][2], DI * C[1][2], DI * C[2][2] }
            };

            // Solution X
            double[] X = new double[]
            {
                AI[0][0] * B[0] + AI[0][1] * B[1] + AI[0][2] * B[2],
                AI[1][0] * B[0] + AI[1][1] * B[1] + AI[1][2] * B[2],
                AI[2][0] * B[0] + AI[2][1] * B[1] + AI[2][2] * B[2]
            };

            return X;
        }

        public static double[] SolveQuadEq3Pts(PTPoint pt1, PTPoint pt2, PTPoint pt3)
        {
            double[][] A = AMatrix(pt1, pt2, pt3);
            double[] B = BMatrix(pt1, pt2, pt3);
            return Solve3x3(A, B);
        }

        public static double[] SolveQuadEq2PtsWithSlopeZero(PTPoint pt1, PTPoint pt2, PTPoint pt3)
        {
            double[][] A = new double[][]
            {
                new double[] { Sq(pt1.X), pt1.X, 1.0 },
                new double[] { Sq(pt2.X), pt2.X, 1.0 },
                new double[] { pt3.X * 2, 1.0, 0.0 }
            };
            double[] B = new double[] { pt1.Y, pt2.Y, 0.0 };
            return Solve3x3(A, B);
        }

        public static double SlopeQuad(double[] coeff, double x)
        {
            return 2 * coeff[0] * x + coeff[1];
        }

        public static double QuadraticYValue(double[] coeff, double x)
        {
            if (coeff == null || coeff.Length < 3) return 0;
            return coeff[0] * Sq(x) + coeff[1] * x + coeff[2];
        }

        public static double LinearYValue(double[] coeff, double x)
        {
            if (coeff == null || coeff.Length < 2) return 0;
            return coeff[0] * x + coeff[1];
        }

        public static double[] SolveLinear2Pts(PTPoint pt1, PTPoint pt2)
        {
            double a = (pt1.Y - pt2.Y) / (pt1.X - pt2.X);
            double b = pt1.Y - a * pt1.X;
            return new double[] { a, b };
        }

        public static void PTProfile(PTHighPoint highPt1, PTHighPoint highPt2, double lowPtHeight,
            out double[] S1, out double[] S2, out double[] S3,
            out PTPoint inflectionPt1, out PTPoint inflectionPt2, out PTPoint lowPt)
        {
            double spanLength = Math.Abs(highPt2.X - highPt1.X);
            double inflectionPercent = 0.1;

            S1 = null; S2 = null; S3 = null;
            inflectionPt1 = null; inflectionPt2 = null; lowPt = null;

            // STRAIGHT TENDON
            if (Math.Abs(lowPtHeight) < 0.001)
            {
                if (highPt1.Condition == "end") highPt1.Y -= 10;
                if (highPt2.Condition == "end") highPt2.Y -= 10;
                
                S2 = SolveLinear2Pts(highPt1, highPt2);
                return;
            }

            // END - END
            if (highPt1.Condition == "end" && highPt2.Condition == "end")
            {
                highPt1.Y -= 10;
                highPt2.Y -= 10;
                double lowBoundX = highPt1.X;
                double highBoundX = highPt2.X;

                while (true)
                {
                    double lowPtX = (highBoundX + lowBoundX) / 2.0;
                    lowPt = new PTPoint(lowPtX, lowPtHeight);
                    S2 = SolveQuadEq3Pts(highPt1, lowPt, highPt2);
                    double slope = SlopeQuad(S2, lowPt.X);

                    if (Math.Abs(slope) < 0.00001) break;
                    if (slope > 0) highBoundX = lowPtX;
                    else lowBoundX = lowPtX;
                }
                return;
            }

            // END - CONTINUOUS
            if (highPt1.Condition == "end" && highPt2.Condition == "continuous")
            {
                highPt1.Y -= 10;
                double minSlopeDelta = 1;

                double[] s2Temp = null;
                double[] s3Temp = null;
                PTPoint inflectionPtTemp = null;
                PTPoint lowPtSelect = null;

                for (int inflectionPtY = (int)lowPtHeight; inflectionPtY <= (int)highPt2.Y + 5; inflectionPtY += 5)
                {
                    double inflectionPtX = highPt2.X - inflectionPercent * spanLength;
                    PTPoint inflectionPt = new PTPoint(inflectionPtX, inflectionPtY);

                    double[] s3 = SolveQuadEq2PtsWithSlopeZero(inflectionPt, highPt2, highPt2);
                    double slopeS3Inflection = SlopeQuad(s3, inflectionPtX);

                    double lowBoundX = highPt1.X;
                    double highBoundX = inflectionPt.X;

                    PTPoint tempLowPt = null;
                    double[] s2 = null;

                    while (true)
                    {
                        double lowPtX = (highBoundX + lowBoundX) / 2.0;
                        tempLowPt = new PTPoint(lowPtX, lowPtHeight);
                        s2 = SolveQuadEq3Pts(highPt1, tempLowPt, inflectionPt);
                        double slope = SlopeQuad(s2, tempLowPt.X);

                        if (Math.Abs(slope) < 0.00001) break;
                        if (slope > 0) highBoundX = lowPtX;
                        else lowBoundX = lowPtX;
                    }

                    double slopeS2Inflection = SlopeQuad(s2, inflectionPt.X);
                    double slopeDelta = Math.Abs(slopeS2Inflection - slopeS3Inflection);

                    if (slopeDelta < minSlopeDelta)
                    {
                        minSlopeDelta = slopeDelta;
                        s2Temp = s2;
                        s3Temp = s3;
                        inflectionPtTemp = inflectionPt;
                        lowPtSelect = tempLowPt;
                    }
                }

                S2 = s2Temp;
                S3 = s3Temp;
                inflectionPt2 = inflectionPtTemp;
                lowPt = lowPtSelect;
                return;
            }

            // CONTINUOUS - END
            if (highPt1.Condition == "continuous" && highPt2.Condition == "end")
            {
                highPt2.Y -= 10;
                double minSlopeDelta = 1;

                double[] s1Temp = null;
                double[] s2Temp = null;
                PTPoint inflectionPtTemp = null;
                PTPoint lowPtSelect = null;

                for (int inflectionPtY = (int)lowPtHeight; inflectionPtY <= (int)highPt1.Y + 5; inflectionPtY += 5)
                {
                    double inflectionPtX = highPt1.X + inflectionPercent * spanLength;
                    PTPoint inflectionPt = new PTPoint(inflectionPtX, inflectionPtY);

                    double[] s1 = SolveQuadEq2PtsWithSlopeZero(inflectionPt, highPt1, highPt1);
                    double slopeS1Inflection = SlopeQuad(s1, inflectionPtX);

                    double lowBoundX = inflectionPt.X;
                    double highBoundX = highPt2.X;

                    PTPoint tempLowPt = null;
                    double[] s2 = null;

                    while (true)
                    {
                        double lowPtX = (highBoundX + lowBoundX) / 2.0;
                        tempLowPt = new PTPoint(lowPtX, lowPtHeight);
                        s2 = SolveQuadEq3Pts(highPt2, tempLowPt, inflectionPt);
                        double slope = SlopeQuad(s2, tempLowPt.X);

                        if (Math.Abs(slope) < 0.00001) break;
                        if (slope > 0) highBoundX = lowPtX;
                        else lowBoundX = lowPtX;
                    }

                    double slopeS2Inflection = SlopeQuad(s2, inflectionPt.X);
                    double slopeDelta = Math.Abs(slopeS2Inflection - slopeS1Inflection);

                    if (slopeDelta < minSlopeDelta)
                    {
                        minSlopeDelta = slopeDelta;
                        s2Temp = s2;
                        s1Temp = s1;
                        inflectionPtTemp = inflectionPt;
                        lowPtSelect = tempLowPt;
                    }
                }

                S1 = s1Temp;
                S2 = s2Temp;
                inflectionPt1 = inflectionPtTemp;
                lowPt = lowPtSelect;
                return;
            }

            // CONTINUOUS - CONTINUOUS
            if (highPt1.Condition == "continuous" && highPt2.Condition == "continuous")
            {
                double minSlopeDelta = 1;
                double[] s1Temp = null;
                double[] s2Temp = null;
                double[] s3Temp = null;
                PTPoint inflectionPt1Temp = null;
                PTPoint inflectionPt2Temp = null;
                PTPoint lowPtSelect = null;

                for (int inflectionPt1Y = (int)lowPtHeight; inflectionPt1Y <= (int)highPt1.Y + 5; inflectionPt1Y += 5)
                {
                    for (int inflectionPt2Y = (int)lowPtHeight; inflectionPt2Y <= (int)highPt2.Y + 5; inflectionPt2Y += 5)
                    {
                        double inflectionPt1X = highPt1.X + inflectionPercent * spanLength;
                        double inflectionPt2X = highPt2.X - inflectionPercent * spanLength;

                        PTPoint infPt1 = new PTPoint(inflectionPt1X, inflectionPt1Y);
                        PTPoint infPt2 = new PTPoint(inflectionPt2X, inflectionPt2Y);

                        double[] s1 = SolveQuadEq2PtsWithSlopeZero(infPt1, highPt1, highPt1);
                        double[] s3 = SolveQuadEq2PtsWithSlopeZero(infPt2, highPt2, highPt2);
                        double slopeS1Inflection = SlopeQuad(s1, inflectionPt1X);
                        double slopeS3Inflection = SlopeQuad(s3, inflectionPt2X);

                        double lowBoundX = infPt1.X;
                        double highBoundX = infPt2.X;

                        PTPoint tempLowPt = null;
                        double[] s2 = null;

                        while (true)
                        {
                            double lowPtX = (highBoundX + lowBoundX) / 2.0;
                            tempLowPt = new PTPoint(lowPtX, lowPtHeight);
                            s2 = SolveQuadEq3Pts(infPt1, tempLowPt, infPt2);
                            double slope = SlopeQuad(s2, tempLowPt.X);

                            if (Math.Abs(slope) < 0.00001) break;
                            if (slope > 0) highBoundX = lowPtX;
                            else lowBoundX = lowPtX;
                        }

                        double slopeS2InflectionS1 = SlopeQuad(s2, infPt1.X);
                        double slopeS2InflectionS3 = SlopeQuad(s2, infPt2.X);
                        double slopeDeltaS2S1 = Math.Abs(slopeS2InflectionS1 - slopeS1Inflection);
                        double slopeDeltaS2S3 = Math.Abs(slopeS2InflectionS3 - slopeS3Inflection);

                        if (slopeDeltaS2S1 + slopeDeltaS2S3 < minSlopeDelta)
                        {
                            minSlopeDelta = slopeDeltaS2S1 + slopeDeltaS2S3;
                            s1Temp = s1;
                            s2Temp = s2;
                            s3Temp = s3;
                            inflectionPt1Temp = infPt1;
                            inflectionPt2Temp = infPt2;
                            lowPtSelect = tempLowPt;
                        }
                    }
                }

                S1 = s1Temp;
                S2 = s2Temp;
                S3 = s3Temp;
                inflectionPt1 = inflectionPt1Temp;
                inflectionPt2 = inflectionPt2Temp;
                lowPt = lowPtSelect;
            }
        }
    }
}
