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

            // Extract Parameters
            var familyManager = doc.FamilyManager;
            if (familyManager != null)
            {
                var currentType = familyManager.CurrentType;
                foreach (FamilyParameter param in familyManager.Parameters)
                {
                    var paramModel = new ParameterModel
                    {
                        Name = param.Definition.Name,
                        IsInstance = param.IsInstance,
                        IsReporting = param.IsReporting,
                        IsShared = param.IsShared,
                        Formula = param.Formula
                    };

#if REVIT2022_OR_GREATER
                    try { paramModel.Type = param.Definition.GetDataType().TypeId; } catch { }
#else
#pragma warning disable CS0618
                    try { paramModel.Type = param.Definition.ParameterType.ToString(); } catch { }
#pragma warning restore CS0618
#endif

#if REVIT2024_OR_GREATER
                    try { paramModel.Group = param.Definition.GetGroupTypeId().TypeId; } catch { }
#else
#pragma warning disable CS0618
                    try { paramModel.Group = param.Definition.ParameterGroup.ToString(); } catch { }
#pragma warning restore CS0618
#endif

                    try
                    {
                        if (currentType != null)
                        {
                            if (param.StorageType == StorageType.Double)
                                paramModel.ValueString = currentType.AsDouble(param)?.ToString();
                            else if (param.StorageType == StorageType.Integer)
                                paramModel.ValueString = currentType.AsInteger(param)?.ToString();
                            else if (param.StorageType == StorageType.String)
                                paramModel.ValueString = currentType.AsString(param);
                            else if (param.StorageType == StorageType.ElementId)
                                paramModel.ValueString = currentType.AsElementId(param)?.ToString();
                            else
                                paramModel.ValueString = currentType.AsValueString(param);
                        }
                    }
                    catch { }

                    data.Parameters.Add(paramModel);
                }
            }

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
                    IsReferenceLine = curve.Category != null && curve.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_ReferenceLines
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
                    try
                    {
                        if (arc.IsBound)
                        {
                            lineModel.StartAngle = arc.GetEndParameter(0);
                            lineModel.EndAngle = arc.GetEndParameter(1);
                        }
                    }
                    catch { }
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

                string labelName = "";
                try
                {
                    labelName = dim.FamilyLabel?.Definition?.Name ?? "";
                }
                catch { }

                var dimModel = new DimensionModel
                {
                    Id = (int)dim.Id.GetIdValue(),
                    Name = dim.Name,
                    Value = dim.Value.Value,
                    ValueString = dim.ValueString,
                    IsLocked = dim.IsLocked,
                    Label = labelName
                };

                try
                {
                    if (dim.Curve != null && dim.Curve.IsBound)
                    {
                        dimModel.DimLineStart = ToXYZModel(dim.Curve.GetEndPoint(0));
                        dimModel.DimLineEnd = ToXYZModel(dim.Curve.GetEndPoint(1));
                    }
                    else if (dim.Curve is Line line)
                    {
                        // Unbound line, just get Origin and a point along direction
                        dimModel.DimLineStart = ToXYZModel(line.Origin);
                        dimModel.DimLineEnd = ToXYZModel(line.Origin + line.Direction * 10);
                    }
                }
                catch { }

                try
                {
                    if (dim.References != null)
                    {
                        foreach (Reference r in dim.References)
                        {
                            dimModel.StableRepresentations.Add(r.ConvertToStableRepresentation(doc));
                            Element referencedElement = doc.GetElement(r);
                            if (referencedElement != null)
                            {
                                dimModel.ReferenceNames.Add(referencedElement.Name);
                                dimModel.ReferenceIds.Add((int)referencedElement.Id.GetIdValue());
                            }
                            else
                            {
                                dimModel.ReferenceNames.Add("Unknown");
                                dimModel.ReferenceIds.Add(-1);
                            }
                        }
                    }
                }
                catch { }

                data.Dimensions.Add(dimModel);
            }

            // Extract TextElements (Labels and TextNotes)
            var textElements = new FilteredElementCollector(doc)
                .OfClass(typeof(TextElement))
                .Cast<TextElement>();

            foreach (var txt in textElements)
            {
                var txtModel = new TextElementModel
                {
                    Id = (int)txt.Id.GetIdValue(),
                    UniqueId = txt.UniqueId,
                    Text = txt.Text,
                    Name = txt.Name
                };

                try
                {
                    if (txt.Coord != null)
                    {
                        txtModel.Position = ToXYZModel(txt.Coord);
                    }
                }
                catch { }

                data.Texts.Add(txtModel);
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
