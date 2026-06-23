using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.ExportFamilyData
{
    public class FamilyInstanceSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is FamilyInstance;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ExportFamilyTypeDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (doc.IsFamilyDocument)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Lệnh này phải được chạy trong môi trường Project.");
                return Result.Failed;
            }

            try
            {
                Reference r = uidoc.Selection.PickObject(ObjectType.Element, new FamilyInstanceSelectionFilter(), "Chọn một đối tượng Family");
                FamilyInstance inst = doc.GetElement(r) as FamilyInstance;

                if (inst == null || inst.Symbol == null || inst.Symbol.Family == null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Không thể lấy thông tin Family từ đối tượng đã chọn.");
                    return Result.Failed;
                }

                Family family = inst.Symbol.Family;
                
                Document familyDoc = doc.EditFamily(family);
                if (familyDoc == null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Không thể mở Family ảo để lấy dữ liệu.");
                    return Result.Failed;
                }

                try
                {
                    FamilyTypeExportModel exportData = null;

                    using (Transaction tx = new Transaction(familyDoc, "Read Family Data"))
                    {
                        tx.Start();

                        FamilyManager fm = familyDoc.FamilyManager;

                        exportData = new FamilyTypeExportModel
                        {
                            FamilyName = family.Name
                        };

                        // Lặp qua các Type trong FamilyManager
                        foreach (FamilyType ft in fm.Types)
                        {
                            FamilyTypeModel typeModel = new FamilyTypeModel
                            {
                                TypeName = ft.Name
                            };
                            
                            fm.CurrentType = ft;

                            foreach (FamilyParameter fp in fm.Parameters)
                            {
                                ParameterModel paramModel = new ParameterModel
                                {
                                    Name = fp.Definition.Name,
                                    IsShared = fp.IsShared,
                                    IsInstance = fp.IsInstance,
                                    Formula = fp.Formula
                                };

                                ExtractFamilyParameterInfo(fp, ft, paramModel, inst);
                                
                                typeModel.Parameters.Add(paramModel);
                            }

                            // Sắp xếp param cho dễ đọc
                            typeModel.Parameters = typeModel.Parameters.OrderBy(p => p.Name).ToList();

                            exportData.Types.Add(typeModel);
                        }

                        // Sắp xếp Types theo tên
                        exportData.Types = exportData.Types.OrderBy(t => t.TypeName).ToList();

                        // Trích xuất hình học
                        exportData.Geometry = ExtractGeometry(familyDoc);

                        tx.RollBack(); // Không cần lưu thay đổi vào family ảo
                    }

                    // Mở hộp thoại lưu file JSON
                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "JSON files (*.json)|*.json",
                        Title = "Lưu file dữ liệu Family Type",
                        FileName = $"{family.Name}_TypeData.json"
                    };

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        
                        string jsonString = JsonHelper.Serialize(exportData);
                        File.WriteAllText(saveFileDialog.FileName, jsonString);

                        Autodesk.Revit.UI.TaskDialog.Show("Hoàn tất", $"Đã xuất thành công {exportData.Types.Count} Types của Family '{family.Name}' ra file JSON!");
                    }
                }
                finally
                {
                    familyDoc.Close(false);
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng bấm ESC hủy lệnh chọn
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Lỗi", "Đã xảy ra lỗi:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private void ExtractFamilyParameterInfo(FamilyParameter fp, FamilyType ft, ParameterModel paramModel, FamilyInstance inst)
        {
            try 
            {
                var def = fp.Definition;
                var typeProp = def.GetType().GetProperty("ParameterType");
                if (typeProp != null)
                {
                    paramModel.Type = typeProp.GetValue(def)?.ToString();
                }
                else
                {
                    var getDataTypeMethod = def.GetType().GetMethod("GetDataType");
                    if (getDataTypeMethod != null)
                    {
                        var forgeTypeId = getDataTypeMethod.Invoke(def, null);
                        if (forgeTypeId != null)
                        {
                            paramModel.Type = forgeTypeId.GetType().GetProperty("TypeId")?.GetValue(forgeTypeId)?.ToString();
                        }
                    }
                }
            } catch { }
            
            // Lấy Group
            try 
            {
                var def = fp.Definition;
                var groupProp = def.GetType().GetProperty("ParameterGroup");
                if (groupProp != null)
                {
                    paramModel.Group = groupProp.GetValue(def)?.ToString();
                }
                else
                {
                    var getGroupMethod = def.GetType().GetMethod("GetGroupTypeId");
                    if (getGroupMethod != null)
                    {
                        var forgeTypeId = getGroupMethod.Invoke(def, null);
                        if (forgeTypeId != null)
                        {
                            paramModel.Group = forgeTypeId.GetType().GetProperty("TypeId")?.GetValue(forgeTypeId)?.ToString();
                        }
                    }
                }
            } catch { }

            if (fp.IsShared)
            {
                try { paramModel.GUID = fp.GUID.ToString(); } catch { }
            }
            
            paramModel.StorageType = fp.StorageType.ToString();

            // Nếu là Instance Parameter, ưu tiên lấy giá trị từ inst trên Project
            bool hasInstanceValue = false;
            if (fp.IsInstance && inst != null)
            {
                Parameter projectParam = null;
                if (fp.IsShared)
                {
                    try { projectParam = inst.get_Parameter(new Guid(paramModel.GUID)); } catch { }
                }
                if (projectParam == null)
                {
                    projectParam = inst.LookupParameter(fp.Definition.Name);
                }

                if (projectParam != null && projectParam.HasValue)
                {
                    hasInstanceValue = true;
                    if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = projectParam.AsValueString();
                    
                    switch (projectParam.StorageType)
                    {
                        case StorageType.Double: 
                            paramModel.InternalValue = projectParam.AsDouble(); 
                            if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = paramModel.InternalValue.ToString();
                            break;
                        case StorageType.Integer: 
                            paramModel.IntegerValue = projectParam.AsInteger(); 
                            if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = paramModel.IntegerValue.ToString();
                            break;
                        case StorageType.String: 
                            if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = projectParam.AsString(); 
                            break;
                        case StorageType.ElementId: 
                            if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = projectParam.AsElementId().ToString(); 
                            break;
                    }
                }
            }

            // Nếu không phải Instance Parameter hoặc không lấy được giá trị từ inst, thì lấy từ FamilyType
            if (!hasInstanceValue && ft.HasValue(fp))
            {
                paramModel.ValueString = ft.AsValueString(fp);
                switch (fp.StorageType)
                {
                    case StorageType.Double: 
                        paramModel.InternalValue = ft.AsDouble(fp) ?? 0.0; 
                        if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = paramModel.InternalValue.ToString();
                        break;
                    case StorageType.Integer: 
                        paramModel.IntegerValue = ft.AsInteger(fp) ?? 0; 
                        if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = paramModel.IntegerValue.ToString();
                        break;
                    case StorageType.String: 
                        if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = ft.AsString(fp); 
                        break;
                    case StorageType.ElementId: 
                        if (string.IsNullOrEmpty(paramModel.ValueString)) paramModel.ValueString = ft.AsElementId(fp)?.ToString(); 
                        break;
                }
            }
        }

        private FamilyGeometryModel ExtractGeometry(Document doc)
        {
            FamilyGeometryModel geom = new FamilyGeometryModel();

            // 1. Subcategories
            Category familyCat = doc.OwnerFamily?.FamilyCategory;
            if (familyCat != null)
            {
                CategoryNameMap subcats = familyCat.SubCategories;
                if (subcats != null)
                {
                    foreach (Category sub in subcats)
                    {
                        geom.Subcategories.Add(new SubcategoryModel
                        {
                            Name = sub.Name,
                            LineWeight = sub.GetLineWeight(GraphicsStyleType.Projection).HasValue ? sub.GetLineWeight(GraphicsStyleType.Projection).Value : 1,
                            LineColor = $"{sub.LineColor.Red},{sub.LineColor.Green},{sub.LineColor.Blue}"
                        });
                    }
                }
            }

            // 2. Reference Planes
            var refPlanes = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>();
            foreach (var rp in refPlanes)
            {
                geom.ReferencePlanes.Add(new ReferencePlaneModel
                {
                    UniqueId = rp.UniqueId,
                    Name = rp.Name,
                    Direction = new XYZModel { X = rp.Direction.X, Y = rp.Direction.Y, Z = rp.Direction.Z },
                    Normal = new XYZModel { X = rp.Normal.X, Y = rp.Normal.Y, Z = rp.Normal.Z },
                    BubbleEnd = new XYZModel { X = rp.BubbleEnd.X, Y = rp.BubbleEnd.Y, Z = rp.BubbleEnd.Z },
                    FreeEnd = new XYZModel { X = rp.FreeEnd.X, Y = rp.FreeEnd.Y, Z = rp.FreeEnd.Z }
                });
            }

            // 3. Curves (Detail & Model)
            var curves = new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).Cast<CurveElement>();
            foreach (var c in curves)
            {
                if (c.GeometryCurve is Line line)
                {
                    geom.Lines.Add(new LineModel
                    {
                        UniqueId = c.UniqueId,
                        LineStyle = c.LineStyle?.Name,
                        Type = c.GetType().Name,
                        CurveShape = "Line",
                        StartPoint = new XYZModel { X = line.GetEndPoint(0).X, Y = line.GetEndPoint(0).Y, Z = line.GetEndPoint(0).Z },
                        EndPoint = new XYZModel { X = line.GetEndPoint(1).X, Y = line.GetEndPoint(1).Y, Z = line.GetEndPoint(1).Z }
                    });
                }
                else if (c.GeometryCurve is Arc arc)
                {
                    geom.Lines.Add(new LineModel
                    {
                        UniqueId = c.UniqueId,
                        LineStyle = c.LineStyle?.Name,
                        Type = c.GetType().Name,
                        CurveShape = "Arc",
                        Center = new XYZModel { X = arc.Center.X, Y = arc.Center.Y, Z = arc.Center.Z },
                        Normal = new XYZModel { X = arc.Normal.X, Y = arc.Normal.Y, Z = arc.Normal.Z },
                        Radius = arc.Radius,
                        StartAngle = arc.GetEndParameter(0),
                        EndAngle = arc.GetEndParameter(1),
                        StartPoint = new XYZModel { X = arc.GetEndPoint(0).X, Y = arc.GetEndPoint(0).Y, Z = arc.GetEndPoint(0).Z },
                        EndPoint = new XYZModel { X = arc.GetEndPoint(1).X, Y = arc.GetEndPoint(1).Y, Z = arc.GetEndPoint(1).Z }
                    });
                }
            }

            // 4. Dimensions
            var dims = new FilteredElementCollector(doc).OfClass(typeof(Dimension)).Cast<Dimension>();
            foreach (var dim in dims)
            {
                var dModel = new DimensionModel
                {
                    UniqueId = dim.UniqueId,
                    Name = dim.Name,
                    ValueString = dim.ValueString,
                    Label = dim.FamilyLabel?.Definition?.Name
                };
                
                try 
                {
                    if (dim.Curve is Line dLine)
                    {
                        dModel.DimLineStart = new XYZModel { X = dLine.GetEndPoint(0).X, Y = dLine.GetEndPoint(0).Y, Z = dLine.GetEndPoint(0).Z };
                        dModel.DimLineEnd = new XYZModel { X = dLine.GetEndPoint(1).X, Y = dLine.GetEndPoint(1).Y, Z = dLine.GetEndPoint(1).Z };
                    }
                } catch { }

                try
                {
                    if (dim.References != null)
                    {
                        foreach (Reference r in dim.References)
                        {
                            Element refElem = doc.GetElement(r.ElementId);
                            if (refElem != null)
                            {
                                dModel.ReferenceUniqueIds.Add(refElem.UniqueId);
                            }
                        }
                    }
                } catch { }

                geom.Dimensions.Add(dModel);
            }

            return geom;
        }
    }
}



