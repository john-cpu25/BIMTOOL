using System;
using System.IO;
using System.Text.Json;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

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
                        var data = JsonSerializer.Deserialize<FamilyDataModel>(jsonString);

                        if (data == null)
                        {
                            TaskDialog.Show("Lỗi", "File JSON không hợp lệ hoặc rỗng.");
                            return Result.Failed;
                        }

                        using (Transaction tx = new Transaction(doc, "Import Family Data"))
                        {
                            tx.Start();
                            
                            // 1. Tạo Reference Planes
                            int rpCount = 0;
                            if (data.ReferencePlanes != null)
                            {
                                foreach (var rpModel in data.ReferencePlanes)
                                {
                                    if (rpModel.BubbleEnd == null || rpModel.FreeEnd == null || rpModel.Normal == null) continue;
                                    
                                    // Try to find existing reference plane by name
                                    ReferencePlane existingRp = null;
                                    if (!string.IsNullOrEmpty(rpModel.Name))
                                    {
                                        existingRp = new FilteredElementCollector(doc)
                                            .OfClass(typeof(ReferencePlane))
                                            .Cast<ReferencePlane>()
                                            .FirstOrDefault(r => r.Name == rpModel.Name);
                                    }
                                    
                                    ReferencePlane targetRp = existingRp;
                                    
                                    if (targetRp == null)
                                    {
                                        XYZ bubble = new XYZ(rpModel.BubbleEnd.X, rpModel.BubbleEnd.Y, rpModel.BubbleEnd.Z);
                                        XYZ free = new XYZ(rpModel.FreeEnd.X, rpModel.FreeEnd.Y, rpModel.FreeEnd.Z);
                                        XYZ normal = new XYZ(rpModel.Normal.X, rpModel.Normal.Y, rpModel.Normal.Z);
                                        XYZ direction = new XYZ(rpModel.Direction.X, rpModel.Direction.Y, rpModel.Direction.Z);
                                        XYZ cutVec = normal.CrossProduct(direction).Normalize();
                                        
                                        targetRp = doc.FamilyCreate.NewReferencePlane(bubble, free, cutVec, doc.ActiveView);
                                        if (targetRp != null && !string.IsNullOrEmpty(rpModel.Name) && rpModel.Name != "Center (Left/Right)" && rpModel.Name != "Center (Front/Back)")
                                        {
                                            try { targetRp.Name = rpModel.Name; } catch { }
                                        }
                                    }
                                    
                                    if (targetRp != null)
                                    {
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
                                if (sketchPlane == null)
                                {
                                    // Revit ViewDirection points away from viewer. Plane normal should point towards viewer.
                                    XYZ viewNormal = doc.ActiveView.ViewDirection.Multiply(-1);
                                    Plane plane = Plane.CreateByNormalAndOrigin(viewNormal, doc.ActiveView.Origin);
                                    sketchPlane = SketchPlane.Create(doc, plane);
                                    doc.ActiveView.SketchPlane = sketchPlane;
                                }

                                foreach (var lineModel in data.Lines)
                                {
                                    if (lineModel.StartPoint == null || lineModel.EndPoint == null) continue;
                                    
                                    // Flatten points to the sketch plane to prevent tiny precision errors that cause tilted arcs
                                    Plane spPlane = sketchPlane.GetPlane();
                                    
                                    XYZ rawStart = new XYZ(lineModel.StartPoint.X, lineModel.StartPoint.Y, lineModel.StartPoint.Z);
                                    XYZ rawEnd = new XYZ(lineModel.EndPoint.X, lineModel.EndPoint.Y, lineModel.EndPoint.Z);
                                    
                                    // Project points onto sketch plane
                                    XYZ start = ProjectOntoPlane(rawStart, spPlane);
                                    XYZ end = ProjectOntoPlane(rawEnd, spPlane);

                                    // Bỏ qua nếu đường line có điểm đầu trùng điểm cuối
                                    if (start.IsAlmostEqualTo(end)) continue;

                                    Curve geomCurve = null;
                                    
                                    if (lineModel.CurveShape == "Arc")
                                    {
                                        if (lineModel.Center != null && lineModel.Normal != null)
                                        {
                                            try
                                            {
                                                XYZ rawCenter = new XYZ(lineModel.Center.X, lineModel.Center.Y, lineModel.Center.Z);
                                                XYZ rawNormal = new XYZ(lineModel.Normal.X, lineModel.Normal.Y, lineModel.Normal.Z);
                                                
                                                // Create a plane for the arc. We don't flatten the normal, we use it directly to preserve the arc's true orientation
                                                // However, we project the center onto the sketch plane to avoid the "Curve must be in the plane" error
                                                XYZ center = ProjectOntoPlane(rawCenter, spPlane);
                                                Plane arcPlane = Plane.CreateByNormalAndOrigin(rawNormal, center);
                                                
                                                geomCurve = Arc.Create(arcPlane, lineModel.Radius, lineModel.StartAngle, lineModel.EndAngle);
                                            }
                                            catch
                                            {
                                                // If that fails, fallback to 3 points
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
                                        else if (lineModel.MidPoint != null)
                                        {
                                            XYZ rawMid = new XYZ(lineModel.MidPoint.X, lineModel.MidPoint.Y, lineModel.MidPoint.Z);
                                            XYZ mid = ProjectOntoPlane(rawMid, spPlane);
                                            try
                                            {
                                                geomCurve = Arc.Create(start, end, mid);
                                            }
                                            catch
                                            {
                                                // Fallback to straight line if arc creation fails
                                                geomCurve = Line.CreateBound(start, end);
                                            }
                                        }
                                        else
                                        {
                                            geomCurve = Line.CreateBound(start, end);
                                        }
                                    }
                                    else
                                    {
                                        geomCurve = Line.CreateBound(start, end);
                                    }
                                    
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
                                        catch
                                        {
                                            // Fallback to DetailCurve if all else fails
                                            try { newCurve = doc.FamilyCreate.NewDetailCurve(doc.ActiveView, geomCurve); } catch { }
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
                                        
                                        lineCount++;
                                    }
                                }
                            }

                            tx.Commit();
                            
                            TaskDialog.Show("Thành công", $"Đã Import thành công:\n- {rpCount} Reference Planes\n- {lineCount} Lines\n(Dimensions đã bị bỏ qua vì không có References).");
                        }
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
        private XYZ ProjectOntoPlane(XYZ point, Plane plane)
        {
            double distance = plane.Normal.DotProduct(point - plane.Origin);
            return point - distance * plane.Normal;
        }
    }
}
