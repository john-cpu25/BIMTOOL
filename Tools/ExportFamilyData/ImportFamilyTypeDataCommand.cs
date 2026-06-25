using System;
using System.IO;

using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.ExportFamilyData
{
    [Transaction(TransactionMode.Manual)]
    public class ImportFamilyTypeDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;

            if (!doc.IsFamilyDocument)
            {
                TaskDialog.Show("Lỗi", "Lệnh này chỉ chạy được trong môi trường Family Document (.rfa).");
                return Result.Failed;
            }

            try
            {
                using (var openFileDialog = new System.Windows.Forms.OpenFileDialog())
                {
                    openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                    openFileDialog.Title = "Chọn file dữ liệu Family Type (JSON)";

                    if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string jsonString = File.ReadAllText(openFileDialog.FileName);
                        
                        FamilyTypeExportModel data = null;
                        try
                        {
                            data = JsonHelper.Deserialize<FamilyTypeExportModel>(jsonString);
                        }
                        catch
                        {
                            TaskDialog.Show("Lỗi", "File JSON không đúng định dạng của Family Type Export.");
                            return Result.Failed;
                        }

                        if (data == null || data.Types == null || data.Types.Count == 0)
                        {
                            TaskDialog.Show("Lỗi", "File JSON không chứa dữ liệu Type nào.");
                            return Result.Failed;
                        }

                        var viewModel = new ViewModels.FamilyTypeDataPreviewViewModel(data, "Import Family Type Data Preview", "Chạy Import");
                        var window = new UI.FamilyTypeDataPreviewWindow(viewModel);

                        viewModel.ExecuteAction = () =>
                        {
                            int successTypes = 0;
                            int skippedParams = 0;
                            List<string> missingParamsLog = new List<string>();

                            using (Transaction tx = new Transaction(doc, "Import Family Types"))
                            {
                                tx.Start();
                                FamilyManager fm = doc.FamilyManager;

                                foreach (var typeModel in data.Types)
                                {
                                    if (string.IsNullOrEmpty(typeModel.TypeName)) continue;

                                    // Tìm Type đã có hoặc tạo mới
                                    FamilyType targetType = null;
                                    foreach (FamilyType ft in fm.Types)
                                    {
                                        if (ft.Name == typeModel.TypeName)
                                        {
                                            targetType = ft;
                                            break;
                                        }
                                    }

                                    if (targetType == null)
                                    {
                                        targetType = fm.NewType(typeModel.TypeName);
                                    }

                                    fm.CurrentType = targetType;

                                    // Set parameter values
                                    foreach (var pModel in typeModel.Parameters)
                                    {
                                        FamilyParameter fp = fm.get_Parameter(pModel.Name);
                                        if (fp == null)
                                        {
                                            fp = CreateMissingParameter(doc, commandData.Application.Application, pModel);
                                            
                                            if (fp == null)
                                            {
                                                if (!missingParamsLog.Contains(pModel.Name))
                                                {
                                                    missingParamsLog.Add(pModel.Name);
                                                }
                                                skippedParams++;
                                                continue;
                                            }
                                        }

                                        // Chỉ gán giá trị hoặc Formula
                                        if (!fp.IsReadOnly)
                                        {
                                            bool hasFormula = false;
                                            if (!string.IsNullOrEmpty(pModel.Formula))
                                            {
                                                try 
                                                { 
                                                    fm.SetFormula(fp, pModel.Formula); 
                                                    hasFormula = true;
                                                } 
                                                catch { }
                                            }

                                            if (!hasFormula)
                                            {
                                                try
                                                {
                                                if (pModel.StorageType == "Double")
                                                {
                                                    fm.Set(fp, pModel.InternalValue);
                                                }
                                                else if (pModel.StorageType == "Integer")
                                                {
                                                    fm.Set(fp, pModel.IntegerValue);
                                                }
                                                else if (pModel.StorageType == "String" && pModel.ValueString != null)
                                                {
                                                    fm.Set(fp, pModel.ValueString);
                                                }
                                                else if (pModel.StorageType == "ElementId")
                                                {
                                                    // ElementId thường không khớp id giữa 2 file project/family nên bỏ qua
                                                }
                                                else if (!string.IsNullOrEmpty(pModel.ValueString))
                                                {
                                                    // Fallback cho JSON cũ hoặc khi không có StorageType
                                                    fm.SetValueString(fp, pModel.ValueString);
                                                }
                                            }
                                            catch { }
                                        }
                                        }
                                    }
                                    successTypes++;
                                }

                                if (data.Geometry != null)
                                {
                                    ImportGeometry(doc, data.Geometry, fm);
                                }

                                tx.Commit();
                            }

                            string resultMsg = $"Đã Import thành công {successTypes} Types!";
                            if (missingParamsLog.Count > 0)
                            {
                                resultMsg += $"\n\nBỏ qua {missingParamsLog.Count} Parameters do chưa tồn tại trong Family và không thể tự tạo (lỗi kiểu dữ liệu):\n";
                                resultMsg += string.Join("\n- ", missingParamsLog.Take(10).Select(p => p));
                                if (missingParamsLog.Count > 10) resultMsg += "\n...";
                            }

                            TaskDialog.Show("Hoàn tất", resultMsg);
                        };

                        window.ShowDialog();
                    }
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", "Đã xảy ra lỗi:\n" + ex.Message);
                return Result.Failed;
            }
        }

        private FamilyParameter CreateMissingParameter(Document doc, Autodesk.Revit.ApplicationServices.Application app, ParameterModel pModel)
        {
            FamilyManager fm = doc.FamilyManager;
            
            // Helper function to get BuiltInParameterGroup
#if !REVIT2024_OR_GREATER
            BuiltInParameterGroup GetBIPG()
            {
                if (!string.IsNullOrEmpty(pModel.Group) && pModel.Group.StartsWith("PG_"))
                {
                    try { return (BuiltInParameterGroup)Enum.Parse(typeof(BuiltInParameterGroup), pModel.Group); } catch { return BuiltInParameterGroup.PG_DATA; }
                }
                return BuiltInParameterGroup.PG_DATA;
            }
#endif

            // Helper function to get ParameterType
#if !REVIT2022_OR_GREATER
            ParameterType GetPT()
            {
                if (!string.IsNullOrEmpty(pModel.Type))
                {
                    try { return (ParameterType)Enum.Parse(typeof(ParameterType), pModel.Type); } catch { return ParameterType.Text; }
                }
                return ParameterType.Text;
            }
#endif

#if REVIT2022_OR_GREATER
            // Helper to get ForgeTypeId for Spec (Type)
            ForgeTypeId GetSpecTypeId()
            {
                if (!string.IsNullOrEmpty(pModel.Type) && pModel.Type.Contains("autodesk."))
                    return new ForgeTypeId(pModel.Type);
                return SpecTypeId.String.Text;
            }
            
            // Helper to get ForgeTypeId for Group
            ForgeTypeId GetGroupTypeId()
            {
                if (!string.IsNullOrEmpty(pModel.Group) && pModel.Group.Contains("autodesk."))
                    return new ForgeTypeId(pModel.Group);
                return GroupTypeId.Data;
            }
#endif

            // 1. Thử tạo Shared Parameter bằng cách tạo file TXT ảo
            if (pModel.IsShared && !string.IsNullOrEmpty(pModel.GUID))
            {
                string tempFile = System.IO.Path.GetTempFileName() + ".txt";
                string originalSpFile = "";
                try { originalSpFile = app.SharedParametersFilename; } catch { }

                try
                {
                    System.IO.File.WriteAllText(tempFile, "");
                    app.SharedParametersFilename = tempFile;

                    DefinitionFile spFile = app.OpenSharedParameterFile();
                    if (spFile != null)
                    {
                        DefinitionGroup group = spFile.Groups.Create("TempGroup");
                        
#if REVIT2022_OR_GREATER
                        ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(pModel.Name, GetSpecTypeId());
#else
                        ExternalDefinitionCreationOptions options = new ExternalDefinitionCreationOptions(pModel.Name, GetPT());
#endif
                        options.GUID = new Guid(pModel.GUID);

                        ExternalDefinition def = group.Definitions.Create(options) as ExternalDefinition;

                        if (def != null)
                        {
#if REVIT2024_OR_GREATER
                            return fm.AddParameter(def, GetGroupTypeId(), pModel.IsInstance);
#else
                            return fm.AddParameter(def, GetBIPG(), pModel.IsInstance);
#endif
                        }
                    }
                }
                catch { }
                finally
                {
                    try { if (!string.IsNullOrEmpty(originalSpFile)) app.SharedParametersFilename = originalSpFile; } catch { }
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }

            // 2. Tạo Non-shared (Local) Parameter
            try 
            {
#if REVIT2022_OR_GREATER
                return fm.AddParameter(pModel.Name, GetGroupTypeId(), GetSpecTypeId(), pModel.IsInstance);
#else
                return fm.AddParameter(pModel.Name, GetBIPG(), GetPT(), pModel.IsInstance);
#endif
            }
            catch { return null; }
        }

        private void ImportGeometry(Document doc, FamilyGeometryModel geom, FamilyManager fm)
        {
            Dictionary<string, Reference> refMap = new Dictionary<string, Reference>();

            // 1. Subcategories
            Category familyCat = doc.OwnerFamily?.FamilyCategory;
            if (familyCat != null && geom.Subcategories != null)
            {
                foreach (var subModel in geom.Subcategories)
                {
                    if (!familyCat.SubCategories.Contains(subModel.Name))
                    {
                        try 
                        {
                            Category newSub = doc.Settings.Categories.NewSubcategory(familyCat, subModel.Name);
                            var colorParts = subModel.LineColor.Split(',');
                            if (colorParts.Length == 3)
                            {
                                newSub.LineColor = new Color(byte.Parse(colorParts[0]), byte.Parse(colorParts[1]), byte.Parse(colorParts[2]));
                            }
                            newSub.SetLineWeight(subModel.LineWeight, GraphicsStyleType.Projection);
                        }
                        catch { }
                    }
                }
            }

            View view = doc.ActiveView;
            if (view == null || view.ViewType != ViewType.FloorPlan)
            {
                view = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().FirstOrDefault(v => v.ViewType == ViewType.FloorPlan && !v.IsTemplate);
            }
            if (view == null) return;

            // 2. Reference Planes
            if (geom.ReferencePlanes != null)
            {
                foreach (var rpModel in geom.ReferencePlanes)
                {
                    try 
                    {
                        XYZ bubbleEnd = new XYZ(rpModel.BubbleEnd.X, rpModel.BubbleEnd.Y, rpModel.BubbleEnd.Z);
                        XYZ freeEnd = new XYZ(rpModel.FreeEnd.X, rpModel.FreeEnd.Y, rpModel.FreeEnd.Z);
                        XYZ cutVec = new XYZ(rpModel.Normal.X, rpModel.Normal.Y, rpModel.Normal.Z);
                        
                        ReferencePlane newRp = doc.FamilyCreate.NewReferencePlane(bubbleEnd, freeEnd, cutVec, view);
                        newRp.Name = rpModel.Name;

                        refMap[rpModel.UniqueId] = newRp.GetReference();
                    }
                    catch { }
                }
            }

            doc.Regenerate();

            // 3. Curves
            if (geom.Lines != null)
            {
                foreach (var lineModel in geom.Lines)
                {
                    try
                    {
                        Curve curve = null;
                        if (lineModel.CurveShape == "Line")
                        {
                            XYZ start = new XYZ(lineModel.StartPoint.X, lineModel.StartPoint.Y, lineModel.StartPoint.Z);
                            XYZ end = new XYZ(lineModel.EndPoint.X, lineModel.EndPoint.Y, lineModel.EndPoint.Z);
                            curve = Line.CreateBound(start, end);
                        }
                        else if (lineModel.CurveShape == "Arc")
                        {
                            XYZ center = new XYZ(lineModel.Center.X, lineModel.Center.Y, lineModel.Center.Z);
                            Plane plane = Plane.CreateByNormalAndOrigin(new XYZ(lineModel.Normal.X, lineModel.Normal.Y, lineModel.Normal.Z), center);
                            curve = Arc.Create(plane, lineModel.Radius, lineModel.StartAngle, lineModel.EndAngle);
                        }

                        if (curve != null)
                        {
                            DetailCurve newCurve = doc.FamilyCreate.NewDetailCurve(view, curve);
                            
                            if (!string.IsNullOrEmpty(lineModel.LineStyle) && familyCat != null && familyCat.SubCategories.Contains(lineModel.LineStyle))
                            {
                                Category subCat = familyCat.SubCategories.get_Item(lineModel.LineStyle);
                                if (subCat != null)
                                {
                                    newCurve.LineStyle = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
                                }
                            }
                            
                            // GeometryCurve Reference sometimes needs regenerating or accessing via GeometryElement
                            refMap[lineModel.UniqueId] = newCurve.GeometryCurve.Reference;
                        }
                    }
                    catch { }
                }
            }

            doc.Regenerate();

            // 4. Dimensions
            if (geom.Dimensions != null)
            {
                foreach (var dimModel in geom.Dimensions)
                {
                    try
                    {
                        ReferenceArray refArray = new ReferenceArray();
                        foreach (string refId in dimModel.ReferenceUniqueIds)
                        {
                            if (refMap.ContainsKey(refId) && refMap[refId] != null)
                            {
                                refArray.Append(refMap[refId]);
                            }
                        }

                        if (refArray.Size >= 2 && dimModel.DimLineStart != null && dimModel.DimLineEnd != null)
                        {
                            XYZ start = new XYZ(dimModel.DimLineStart.X, dimModel.DimLineStart.Y, dimModel.DimLineStart.Z);
                            XYZ end = new XYZ(dimModel.DimLineEnd.X, dimModel.DimLineEnd.Y, dimModel.DimLineEnd.Z);
                            Line dimLine = Line.CreateBound(start, end);

                            Dimension newDim = doc.FamilyCreate.NewDimension(view, dimLine, refArray);

                            if (!string.IsNullOrEmpty(dimModel.Label))
                            {
                                FamilyParameter fp = fm.get_Parameter(dimModel.Label);
                                if (fp != null)
                                {
                                    using (SubTransaction st = new SubTransaction(doc))
                                    {
                                        st.Start();
                                        try
                                        {
                                            newDim.FamilyLabel = fp;
                                            doc.Regenerate(); // Ép Revit kiểm tra lỗi Constraint ngay lập tức
                                            st.Commit();
                                        }
                                        catch
                                        {
                                            st.RollBack(); // Nếu bị lỗi "This dimension can not be labeled", bỏ qua gán nhãn
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }
}

