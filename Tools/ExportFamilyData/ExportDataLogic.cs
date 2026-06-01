using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RincoNhan.Core;

namespace RincoNhan.Tools.ExportFamilyData
{
    public static class ExportDataLogic
    {
        public static FamilyDataModel ExtractData(Document doc)
        {
            var data = new FamilyDataModel
            {
                FamilyName = doc.Title
            };

            // Extract Lines (CurveElements)
            var curveElements = new FilteredElementCollector(doc)
                .OfClass(typeof(CurveElement))
                .Cast<CurveElement>();

            foreach (var curve in curveElements)
            {
                var geomCurve = curve.GeometryCurve;
                if (geomCurve == null) continue;

                var lineModel = new LineModel
                {
                    Id = (int)curve.Id.GetIdValue(),
                    Type = curve.GetType().Name,
                    LineStyle = curve.LineStyle?.Name ?? "None",
                    CurveShape = geomCurve.GetType().Name,
                    IsReferenceLine = curve.Category != null && curve.Category.Id.IntegerValue == (int)BuiltInCategory.OST_ReferenceLines
                };

                if (geomCurve.IsBound)
                {
                    lineModel.StartPoint = ToXYZModel(geomCurve.GetEndPoint(0));
                    lineModel.EndPoint = ToXYZModel(geomCurve.GetEndPoint(1));
                    
                    // Lấy điểm giữa để phục dựng lại đường cong (đặc biệt là Arc)
                    try
                    {
                        lineModel.MidPoint = ToXYZModel(geomCurve.Evaluate(0.5, true));
                    }
                    catch { }
                }

                if (geomCurve is Arc arc)
                {
                    lineModel.Center = ToXYZModel(arc.Center);
                    lineModel.Normal = ToXYZModel(arc.Normal);
                    lineModel.Radius = arc.Radius;
                    
                    // The parameter bounds of an Arc are its Start and End angles
                    lineModel.StartAngle = arc.GetEndParameter(0);
                    lineModel.EndAngle = arc.GetEndParameter(1);
                }

                data.Lines.Add(lineModel);
            }

            // Extract Reference Planes
            var refPlanes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>();

            foreach (var rp in refPlanes)
            {

                var rpModel = new ReferencePlaneModel
                {
                    Id = (int)rp.Id.GetIdValue(),
                    Name = rp.Name,
                    Direction = ToXYZModel(rp.Direction),
                    Normal = ToXYZModel(rp.Normal),
                    BubbleEnd = ToXYZModel(rp.BubbleEnd),
                    FreeEnd = ToXYZModel(rp.FreeEnd),
                    IsReference = 0,
                    DefinesOrigin = false
                };
                
                Parameter isRefParam = rp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
                if (isRefParam != null && isRefParam.StorageType == StorageType.Integer)
                {
                    rpModel.IsReference = isRefParam.AsInteger();
                }

                Parameter defOriginParam = rp.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN);
                if (defOriginParam != null && defOriginParam.StorageType == StorageType.Integer)
                {
                    rpModel.DefinesOrigin = defOriginParam.AsInteger() == 1;
                }

                data.ReferencePlanes.Add(rpModel);
            }

            // Extract Dimensions
            var dimensions = new FilteredElementCollector(doc)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>();

            foreach (var dim in dimensions)
            {
                if (dim.Value == null) continue; // Skip text-only notes if they somehow cast

                data.Dimensions.Add(new DimensionModel
                {
                    Id = (int)dim.Id.GetIdValue(),
                    Name = dim.Name,
                    Value = dim.Value.Value,
                    ValueString = dim.ValueString,
                    IsLocked = dim.IsLocked
                });
            }

            return data;
        }

        private static XYZModel ToXYZModel(XYZ pt)
        {
            if (pt == null) return null;
            return new XYZModel
            {
                X = pt.X,
                Y = pt.Y,
                Z = pt.Z
            };
        }
    }
}
