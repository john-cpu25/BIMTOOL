using System;
using System.IO;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;
using System.Collections.Generic;

namespace RincoNhan.Tools.ExportFamilyData
{
    [Transaction(TransactionMode.Manual)]
    public class ImportFamilyDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

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
                    openFileDialog.Title = "Chọn file dữ liệu Family (JSON)";

                    if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string jsonString = File.ReadAllText(openFileDialog.FileName);
                        var data = JsonHelper.Deserialize<FamilyDataModel>(jsonString);

                        if (data == null)
                        {
                            TaskDialog.Show("Lỗi", "File JSON không hợp lệ hoặc rỗng.");
                            return Result.Failed;
                        }

                        var viewModel = new ViewModels.FamilyDataDebugViewModel(data, "Import Family Data Preview", "Chạy Import");
                        var window = new UI.FamilyDataDebugWindow(viewModel);

                        viewModel.ExecuteAction = () =>
                        {
                            try
                            {
                                using (Transaction tx = new Transaction(doc, "Import Family Data"))
                                {
                                    tx.Start();

                                int paramCount = 0;
                                if (data.Parameters != null)
                                {
                                    FamilyManager fm = doc.FamilyManager;
                                    foreach (var pModel in data.Parameters)
                                    {
                                        FamilyParameter fp = fm.get_Parameter(pModel.Name);
                                        if (fp == null)
                                        {
                                            fp = CreateMissingParameter(doc, commandData.Application.Application, pModel);
                                        }
                                        
                                        if (fp != null && !fp.IsReadOnly)
                                        {
                                            paramCount++;
                                            bool hasFormula = false;
                                            if (!string.IsNullOrEmpty(pModel.Formula))
                                            {
                                                try { fm.SetFormula(fp, pModel.Formula); hasFormula = true; } catch { }
                                            }
                                            
                                            if (!hasFormula)
                                            {
                                                try
                                                {
                                                    if (pModel.StorageType == "Double") fm.Set(fp, pModel.InternalValue);
                                                    else if (pModel.StorageType == "Integer") fm.Set(fp, pModel.IntegerValue);
                                                    else if (pModel.StorageType == "String" && pModel.ValueString != null) fm.Set(fp, pModel.ValueString);
                                                    else if (!string.IsNullOrEmpty(pModel.ValueString)) fm.SetValueString(fp, pModel.ValueString);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }

                                Dictionary<int, Element> importedElements = new Dictionary<int, Element>();
                            
                            // 1. Tạo Reference Planes
                            int rpCount = 0;
                            if (data.ReferencePlanes != null)
                            {
                                foreach (var rpModel in data.ReferencePlanes)
                                {
                                    if (rpModel.BubbleEnd == null || rpModel.FreeEnd == null || rpModel.Normal == null) continue;
                                    
                                    // Try to find existing reference plane by name ONLY for default origin planes
                                    // Otherwise, we might accidentally match multiple unnamed planes (e.g. \"Reference Plane\") to a single existing one!
                                    ReferencePlane existingRp = null;
                                    if (!string.IsNullOrEmpty(rpModel.Name) && 
                                        (rpModel.Name == "Center (Left/Right)" || 
                                         rpModel.Name == "Center (Front/Back)" || 
                                         rpModel.Name == "Center (Elevation)" ||
                                         rpModel.Name == "Ref. Level"))
                                    {
                                        existingRp = new FilteredElementCollector(doc)
                                            .OfClass(typeof(ReferencePlane))
                                            .Cast<ReferencePlane>()
                                            .FirstOrDefault(r => r.Name == rpModel.Name);
                                    }
                                    
                                    ReferencePlane targetRp = existingRp;
                                    
                                    if (targetRp == null)
                                    {
                                        XYZ p1 = new XYZ(rpModel.BubbleEnd.X, rpModel.BubbleEnd.Y, rpModel.BubbleEnd.Z);
                                        XYZ p2 = new XYZ(rpModel.FreeEnd.X, rpModel.FreeEnd.Y, rpModel.FreeEnd.Z);
                                        XYZ normal = new XYZ(rpModel.Normal.X, rpModel.Normal.Y, rpModel.Normal.Z);
                                        
                                        // Đảm bảo p1 và p2 không trùng nhau
                                        if (p1.IsAlmostEqualTo(p2))
                                        {
                                            p2 = p1 + new XYZ(rpModel.Direction.X, rpModel.Direction.Y, rpModel.Direction.Z) * 10;
                                            if (p1.IsAlmostEqualTo(p2)) p2 = p1 + new XYZ(10, 0, 0);
                                        }

                                        XYZ dir = (p2 - p1).Normalize();
                                        XYZ cutVec = normal.CrossProduct(dir);
                                        if (cutVec.IsAlmostEqualTo(XYZ.Zero))
                                        {
                                            cutVec = normal.CrossProduct(new XYZ(0, 0, 1));
                                            if (cutVec.IsAlmostEqualTo(XYZ.Zero)) cutVec = normal.CrossProduct(new XYZ(0, 1, 0));
                                        }
                                        cutVec = cutVec.Normalize();
                                        
                                        View rpView = GetAppropriateViewForReferencePlane(doc, normal) ?? doc.ActiveView;
                                        
                                        try
                                        {
                                            targetRp = doc.FamilyCreate.NewReferencePlane(p1, p2, cutVec, rpView);
                                            if (targetRp != null)
                                            {
                                                try { targetRp.Maximize3DExtents(); } catch { } // Kéo dài RP ra toàn mô hình để luôn thấy được
                                                
                                                if (!string.IsNullOrEmpty(rpModel.Name) && rpModel.Name != "Center (Left/Right)" && rpModel.Name != "Center (Front/Back)")
                                                {
                                                    string desiredName = rpModel.Name;
                                                    
                                                    // Bỏ qua gán tên nếu nó là tên mặc định vô thưởng vô phạt để tránh sinh ra "Reference Plane_1"
                                                    if (desiredName == "Reference Plane" || desiredName == "Mặt phẳng Tham chiếu")
                                                    {
                                                        // Không set name, Revit tự cho tên mặc định
                                                    }
                                                    else
                                                    {
                                                        int suffix = 1;
                                                        while (new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().Any(r => r.Name == desiredName && r.Id != targetRp.Id))
                                                        {
                                                            desiredName = rpModel.Name + "_" + suffix;
                                                            suffix++;
                                                        }
                                                        try { targetRp.Name = desiredName; } catch { }
                                                    }
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                    
                                    if (targetRp != null)
                                    {
                                        importedElements[rpModel.Id] = targetRp;
                                        
                                        // Áp dụng Is Reference
                                        Parameter isRefParam = targetRp.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
                                        if (isRefParam != null && !isRefParam.IsReadOnly)
                                        {
                                            try { isRefParam.Set(rpModel.IsReference); } catch { }
                                        }

                                        // Áp dụng Defines Origin
                                        Parameter defOriginParam = targetRp.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN);
                                        if (defOriginParam != null && !defOriginParam.IsReadOnly)
                                        {
                                            try { defOriginParam.Set(rpModel.DefinesOrigin ? 1 : 0); } catch { }
                                        }

                                        rpCount++;
                                    }
                                }
                            }

                            // 2. Tạo Lines
                            int lineCount = 0;
                            if (data.Lines != null)
                            {
                                // Tìm SketchPlane của view hiện tại, hoặc tạo mới nếu chưa có
                                SketchPlane sketchPlane = doc.ActiveView.SketchPlane;
                                
                                XYZ viewNormal = doc.ActiveView.ViewDirection.Multiply(-1);
                                Plane viewPlane = Plane.CreateByNormalAndOrigin(viewNormal, doc.ActiveView.Origin);
                                
                                if (sketchPlane == null)
                                {
                                    try
                                    {
                                        sketchPlane = SketchPlane.Create(doc, viewPlane);
                                        doc.ActiveView.SketchPlane = sketchPlane;
                                    }
                                    catch
                                    {
                                        // Ignore sketch plane creation error in 2D families (e.g., Annotation Symbols)
                                    }
                                }
                                
                                Plane spPlane = sketchPlane != null ? sketchPlane.GetPlane() : viewPlane;

                                foreach (var lineModel in data.Lines)
                                {
                                    XYZ start = null;
                                    XYZ end = null;
                                    
                                    if (lineModel.StartPoint != null && lineModel.EndPoint != null)
                                    {
                                        XYZ rawStart = new XYZ(lineModel.StartPoint.X, lineModel.StartPoint.Y, lineModel.StartPoint.Z);
                                        XYZ rawEnd = new XYZ(lineModel.EndPoint.X, lineModel.EndPoint.Y, lineModel.EndPoint.Z);
                                        start = ProjectOntoPlane(rawStart, spPlane);
                                        end = ProjectOntoPlane(rawEnd, spPlane);
                                        
                                        // Bỏ qua nếu đường line có điểm đầu trùng điểm cuối
                                        if (start.IsAlmostEqualTo(end)) continue;
                                    }

                                    Curve geomCurve = null;
                                    
                                    if (lineModel.CurveShape == "Arc")
                                    {
                                        if (lineModel.Center != null && lineModel.Normal != null)
                                        {
                                            try
                                            {
                                                XYZ rawCenter = new XYZ(lineModel.Center.X, lineModel.Center.Y, lineModel.Center.Z);
                                                XYZ rawNormal = new XYZ(lineModel.Normal.X, lineModel.Normal.Y, lineModel.Normal.Z);
                                                XYZ center = ProjectOntoPlane(rawCenter, spPlane);
                                                
                                                // Guarantee perfectly parallel normal to sketch plane to avoid "Curve must be in the plane"
                                                XYZ normalToUse = rawNormal.DotProduct(spPlane.Normal) >= 0 ? spPlane.Normal : spPlane.Normal.Negate();
                                                Plane arcPlane = Plane.CreateByNormalAndOrigin(normalToUse, center);
                                                
                                                double sAngle = lineModel.StartAngle;
                                                double eAngle = lineModel.EndAngle;
                                                if (Math.Abs(eAngle - sAngle) < 1e-9)
                                                {
                                                    sAngle = 0;
                                                    eAngle = 2 * Math.PI;
                                                }
                                                else if (sAngle > eAngle)
                                                {
                                                    double temp = sAngle;
                                                    sAngle = eAngle;
                                                    eAngle = temp;
                                                }
                                                
                                                geomCurve = Arc.Create(arcPlane, lineModel.Radius, sAngle, eAngle);
                                            }
                                            catch (Exception innerEx)
                                            {
                                                TaskDialog.Show("Arc Create Error", $"Arc.Create failed: {innerEx.Message}\nAngles: {lineModel.StartAngle} to {lineModel.EndAngle}");
                                                if (start != null && end != null)
                                                {
                                                    if (lineModel.MidPoint != null)
                                                    {
                                                        XYZ rawMid = new XYZ(lineModel.MidPoint.X, lineModel.MidPoint.Y, lineModel.MidPoint.Z);
                                                        XYZ mid = ProjectOntoPlane(rawMid, spPlane);
                                                        try { geomCurve = Arc.Create(start, end, mid); } catch { geomCurve = Line.CreateBound(start, end); }
                                                    }
                                                    else
                                                    {
                                                        geomCurve = Line.CreateBound(start, end);
                                                    }
                                                }
                                            }
                                        }
                                        else if (lineModel.MidPoint != null && start != null && end != null)
                                        {
                                            XYZ rawMid = new XYZ(lineModel.MidPoint.X, lineModel.MidPoint.Y, lineModel.MidPoint.Z);
                                            XYZ mid = ProjectOntoPlane(rawMid, spPlane);
                                            try { geomCurve = Arc.Create(start, end, mid); } catch { geomCurve = Line.CreateBound(start, end); }
                                        }
                                        else if (start != null && end != null)
                                        {
                                            geomCurve = Line.CreateBound(start, end);
                                        }
                                    }
                                    else
                                    {
                                        if (start != null && end != null)
                                            geomCurve = Line.CreateBound(start, end);
                                    }
                                    
                                    if (geomCurve == null) continue;
                                    
                                    CurveElement newCurve = null;
                                    try
                                    {
                                        if (lineModel.IsReferenceLine)
                                        {
                                            ModelCurve mc = doc.FamilyCreate.NewModelCurve(geomCurve, sketchPlane);
                                            if (mc != null)
                                            {
                                                try { mc.ChangeToReferenceLine(); } catch { }
                                                newCurve = mc;
                                            }
                                        }
                                        else if (lineModel.Type.Contains("Symbolic"))
                                        {
                                            newCurve = doc.FamilyCreate.NewSymbolicCurve(geomCurve, sketchPlane);
                                        }
                                        else if (lineModel.Type.Contains("Detail"))
                                        {
                                            newCurve = doc.FamilyCreate.NewDetailCurve(doc.ActiveView, geomCurve);
                                        }
                                        else
                                        {
                                            newCurve = doc.FamilyCreate.NewModelCurve(geomCurve, sketchPlane);
                                        }
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            // Fallback to SymbolicCurve if ModelCurve is not permitted in this family type
                                            newCurve = doc.FamilyCreate.NewSymbolicCurve(geomCurve, sketchPlane);
                                        }
                                        catch (Exception innerEx)
                                        {
                                            // Fallback to DetailCurve if all else fails
                                            try { newCurve = doc.FamilyCreate.NewDetailCurve(doc.ActiveView, geomCurve); } 
                                            catch (Exception finalEx)
                                            {
                                                TaskDialog.Show("Error Creating Curve", $"Failed to create curve. Inner: {innerEx.Message}, Final: {finalEx.Message}\nType: {lineModel.Type}\nShape: {lineModel.CurveShape}");
                                            }
                                        }
                                    }

                                    if (newCurve != null)
                                    {
                                        if (!string.IsNullOrEmpty(lineModel.LineStyle) && lineModel.LineStyle != "None")
                                        {
                                            var graphicsStyle = new FilteredElementCollector(doc)
                                                .OfClass(typeof(GraphicsStyle))
                                                .Cast<GraphicsStyle>()
                                                .FirstOrDefault(gs => gs.Name == lineModel.LineStyle);
                                                
                                            if (graphicsStyle != null)
                                            {
                                                try { newCurve.LineStyle = graphicsStyle; } catch { }
                                            }
                                        }
                                        
                                        importedElements[lineModel.Id] = newCurve;
                                        lineCount++;
                                    }
                                }
                            }

                            // 3. Tạo Dimensions
                            int dimCount = 0;
                            List<DimensionData> dimensionsToProcess = new List<DimensionData>();
                            
                            if (data.Dimensions != null)
                            {
                                foreach (var dimModel in data.Dimensions)
                                {
                                    if (dimModel.ReferenceIds == null || dimModel.ReferenceIds.Count < 2) continue;
                                    
                                    ReferenceArray refArray = new ReferenceArray();
                                    for (int i = 0; i < dimModel.ReferenceIds.Count; i++)
                                    {
                                        int oldId = dimModel.ReferenceIds[i];
                                        string oldStable = null;
                                        if (dimModel.StableRepresentations != null && i < dimModel.StableRepresentations.Count)
                                        {
                                            oldStable = dimModel.StableRepresentations[i];
                                        }
                                        
                                        if (oldId == -1) continue;
                                        
                                        if (importedElements.TryGetValue(oldId, out Element el))
                                        {
                                            try
                                            {
                                                Reference newRef = null;
                                                
                                                if (!string.IsNullOrEmpty(oldStable))
                                                {
                                                    string oldIdStr = oldId.ToString();
                                                    string newIdStr = el.Id.GetIdValue().ToString();
                                                    
                                                    if (oldStable == oldIdStr)
                                                    {
                                                        newRef = Reference.ParseFromStableRepresentation(doc, newIdStr);
                                                    }
                                                    else if (oldStable.StartsWith(oldIdStr + ":"))
                                                    {
                                                        string newStable = newIdStr + oldStable.Substring(oldIdStr.Length);
                                                        newRef = Reference.ParseFromStableRepresentation(doc, newStable);
                                                    }
                                                }
                                                
                                                if (newRef != null)
                                                {
                                                    refArray.Append(newRef);
                                                }
                                                else
                                                {
                                                    // Fallback
                                                    if (el is ReferencePlane rp) refArray.Append(rp.GetReference());
                                                    else if (el is ModelCurve mc) refArray.Append(mc.GeometryCurve.Reference);
                                                    else if (el is SymbolicCurve sc) refArray.Append(sc.GeometryCurve.Reference);
                                                    else if (el is DetailCurve dc) refArray.Append(dc.GeometryCurve.Reference);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                    
                                    if (refArray.Size >= 2)
                                    {
                                        try
                                        {
                                            Line dimLine = null;
                                            Plane viewPlane = Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
                                            
                                            if (dimModel.DimLineStart != null && dimModel.DimLineEnd != null)
                                            {
                                                XYZ pt1 = new XYZ(dimModel.DimLineStart.X, dimModel.DimLineStart.Y, dimModel.DimLineStart.Z);
                                                XYZ pt2 = new XYZ(dimModel.DimLineEnd.X, dimModel.DimLineEnd.Y, dimModel.DimLineEnd.Z);
                                                
                                                pt1 = ProjectOntoPlane(pt1, viewPlane);
                                                pt2 = ProjectOntoPlane(pt2, viewPlane);
                                                
                                                if (!pt1.IsAlmostEqualTo(pt2))
                                                {
                                                    dimLine = Line.CreateBound(pt1, pt2);
                                                }
                                            }
                                            
                                            if (dimLine == null)
                                            {
                                                XYZ defaultPt1 = ProjectOntoPlane(XYZ.Zero, viewPlane);
                                                XYZ defaultPt2 = ProjectOntoPlane(doc.ActiveView.RightDirection * 10, viewPlane);
                                                if (!defaultPt1.IsAlmostEqualTo(defaultPt2))
                                                    dimLine = Line.CreateBound(defaultPt1, defaultPt2);
                                            }
                                            
                                            Dimension newDim = doc.FamilyCreate.NewDimension(doc.ActiveView, dimLine, refArray);
                                            if (newDim != null)
                                            {
                                                if (dimModel.IsLocked || !string.IsNullOrEmpty(dimModel.Label))
                                                {
                                                    dimensionsToProcess.Add(new DimensionData { Dim = newDim, IsLocked = dimModel.IsLocked, Label = dimModel.Label });
                                                }
                                                
                                                dimCount++;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }

                            // 3.5 Tạo Texts
                            int textCount = 0;
                            if (data.Texts != null && data.Texts.Count > 0)
                            {
                                ElementId defaultTextTypeId = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType)).FirstElementId();
                                if (defaultTextTypeId != null)
                                {
                                    foreach (var txtModel in data.Texts)
                                    {
                                        if (txtModel.Position != null && !string.IsNullOrEmpty(txtModel.Text))
                                        {
                                            try
                                            {
                                                XYZ pos = new XYZ(txtModel.Position.X, txtModel.Position.Y, txtModel.Position.Z);
                                                Plane viewPlane = Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
                                                pos = ProjectOntoPlane(pos, viewPlane);
                                                TextNote.Create(doc, doc.ActiveView.Id, pos, txtModel.Text, defaultTextTypeId);
                                                textCount++;
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }

                            tx.Commit();
                            
                            // 4. Áp dụng Label và Lock trong các Transaction riêng biệt để tránh Overconstrained
                            foreach (var dimData in dimensionsToProcess)
                            {
                                using (Transaction txLabel = new Transaction(doc, "Apply Dimension Label/Lock"))
                                {
                                    txLabel.Start();
                                    bool hasError = false;
                                    try
                                    {
                                        if (dimData.IsLocked)
                                        {
                                            dimData.Dim.IsLocked = true;
                                        }
                                        if (!string.IsNullOrEmpty(dimData.Label))
                                        {
                                            FamilyParameter param = doc.FamilyManager.get_Parameter(dimData.Label);
                                            if (param != null)
                                            {
                                                try 
                                                {
                                                    dimData.Dim.FamilyLabel = param;
                                                }
                                                catch
                                                {
                                                    hasError = true;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        hasError = true;
                                    }

                                    if (hasError)
                                    {
                                        if (txLabel.GetStatus() == TransactionStatus.Started)
                                        {
                                            txLabel.RollBack();
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            txLabel.Commit();
                                        }
                                        catch
                                        {
                                            if (txLabel.GetStatus() == TransactionStatus.Started)
                                            {
                                                txLabel.RollBack();
                                            }
                                        }
                                    }
                                }
                            }
                            
                            int totalRp = data.ReferencePlanes?.Count ?? 0;
                            int totalLine = data.Lines?.Count ?? 0;
                            int totalDim = data.Dimensions?.Count ?? 0;
                            int totalParam = data.Parameters?.Count ?? 0;
                            int totalText = data.Texts?.Count ?? 0;
                            
                            TaskDialog.Show("Hoàn tất", $"Đã Import hoàn tất!\n" +
                                                        $"- Parameters: {paramCount}/{totalParam}\n" +
                                                        $"- Reference Planes: {rpCount}/{totalRp}\n" +
                                                        $"- Lines: {lineCount}/{totalLine}\n" +
                                                        $"- Dimensions: {dimCount}/{totalDim}\n" +
                                                        $"- Texts: {textCount}/{totalText}\n\n" +
                                                        "(Các đối tượng không hợp lệ hoặc bị trùng lặp đã được tự động bỏ qua).");
                        }
                            }
                            catch (Exception ex)
                            {
                                TaskDialog.Show("Lỗi ExecuteAction", "Chi tiết lỗi: " + ex.Message + "\n" + ex.StackTrace);
                            }
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

        private XYZ ProjectOntoPlane(XYZ point, Plane plane)
        {
            double distance = plane.Normal.DotProduct(point - plane.Origin);
            return point - distance * plane.Normal;
        }

        private View GetAppropriateViewForReferencePlane(Document doc, XYZ normal)
        {
            if (doc.ActiveView != null && Math.Abs(doc.ActiveView.ViewDirection.DotProduct(normal)) < 0.1)
            {
                return doc.ActiveView;
            }

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan || v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section))
                .ToList();

            foreach (var view in views)
            {
                if (Math.Abs(view.ViewDirection.DotProduct(normal)) < 0.1)
                {
                    return view;
                }
            }
            return doc.ActiveView;
        }

        private class DimensionData
        {
            public Dimension Dim { get; set; }
            public bool IsLocked { get; set; }
            public string Label { get; set; }
        }
    }
}

