using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace RincoNhan.Tools.ConvertHatch
{
    public enum ConvertHatchAction
    {
        None,
        ExportJson,
        ImportJson,
        ExportPat
    }

    public class ConvertHatchEventHandler : IExternalEventHandler
    {
        public ConvertHatchAction ActionToRun { get; set; } = ConvertHatchAction.None;
        
        // Dùng để truyền File/Folder path từ WPF xuống Revit API
        public string SelectedPath { get; set; }

        public void Execute(UIApplication uiapp)
        {
            if (ActionToRun == ConvertHatchAction.None) return;

            try
            {
                switch (ActionToRun)
                {
                    case ConvertHatchAction.ExportJson:
                        RunExportJson(uiapp);
                        break;
                    case ConvertHatchAction.ImportJson:
                        RunImportJson(uiapp);
                        break;
                    case ConvertHatchAction.ExportPat:
                        RunExportPat(uiapp);
                        break;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng hủy thao tác PickObjects
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi (Crash Prevented)", $"Đã xảy ra lỗi:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
            finally
            {
                ActionToRun = ConvertHatchAction.None;
                try
                {
                    if (ConvertHatchCommand.WindowInstance != null)
                    {
                        ConvertHatchCommand.WindowInstance.Dispatcher.Invoke(() => 
                        {
                            ConvertHatchCommand.WindowInstance.Show();
                            ConvertHatchCommand.WindowInstance.Activate();
                        });
                    }
                }
                catch { }
            }
        }

        public string GetName()
        {
            return "ConvertHatchEventHandler";
        }

        #region Export JSON Logic
        private void RunExportJson(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (string.IsNullOrEmpty(SelectedPath)) return;

            IList<Reference> selectedRefs = uidoc.Selection.PickObjects(ObjectType.Element, new MixedExportSelectionFilter(), "Select Filled Regions, Texts, and Lines to export");
            if (selectedRefs == null || selectedRefs.Count == 0) return;

            ConvertHatchModel exportModel = new ConvertHatchModel();

            // 1. Export ALL Fill Patterns in the project
            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .ToList();

            foreach (var fpe in allPatterns)
            {
                try
                {
                    FillPattern fp = fpe.GetFillPattern();
                    FillPatternData fpData = new FillPatternData
                    {
                        Name = fpe.Name,
                        Target = fp.Target == FillPatternTarget.Drafting ? "Drafting" : "Model"
                    };

                    IList<FillGrid> grids = fp.GetFillGrids();
                    foreach (FillGrid grid in grids)
                    {
                        FillGridData gridData = new FillGridData
                        {
                            Angle = grid.Angle,
                            OriginU = grid.Origin.U,
                            OriginV = grid.Origin.V,
                            Offset = grid.Offset,
                            Shift = grid.Shift,
                            Segments = grid.GetSegments().ToList()
                        };
                        fpData.Grids.Add(gridData);
                    }

                    exportModel.AllFillPatterns.Add(fpData);
                }
                catch { }
            }

            // 2. Export Selected Elements
            foreach (Reference r in selectedRefs)
            {
                Element elem = doc.GetElement(r);

                if (elem is FilledRegion fr)
                {
                    FilledRegionType frType = doc.GetElement(fr.GetTypeId()) as FilledRegionType;
                    if (frType == null) continue;

                    FilledRegionData frData = new FilledRegionData();

                    // Type Data
                    frData.TypeData = new FilledRegionTypeData
                    {
                        Name = frType.Name,
                        ColorRed = frType.ForegroundPatternColor.Red,
                        ColorGreen = frType.ForegroundPatternColor.Green,
                        ColorBlue = frType.ForegroundPatternColor.Blue,
                        LineWeight = frType.LineWeight,
                        IsMasking = frType.IsMasking
                    };

                    try {
                        frData.TypeData.BackgroundColorRed = frType.BackgroundPatternColor.Red;
                        frData.TypeData.BackgroundColorGreen = frType.BackgroundPatternColor.Green;
                        frData.TypeData.BackgroundColorBlue = frType.BackgroundPatternColor.Blue;
                    } catch { }

                    frData.TypeData.Description = frType.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.AsString();
                    frData.TypeData.Model = frType.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.AsString();
                    frData.TypeData.Manufacturer = frType.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER)?.AsString();
                    frData.TypeData.TypeComments = frType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.AsString();
                    frData.TypeData.Url = frType.get_Parameter(BuiltInParameter.ALL_MODEL_URL)?.AsString();
                    frData.TypeData.Keynote = frType.get_Parameter(BuiltInParameter.KEYNOTE_PARAM)?.AsString();

                    try {
                        ElementId fgId = frType.ForegroundPatternId;
                        if (fgId != ElementId.InvalidElementId) {
                            FillPatternElement fpe = doc.GetElement(fgId) as FillPatternElement;
                            if (fpe != null) frData.TypeData.ForegroundPatternName = fpe.Name;
                        }
                    } catch { }

                    try {
                        ElementId bgId = frType.BackgroundPatternId;
                        if (bgId != ElementId.InvalidElementId) {
                            FillPatternElement fpe = doc.GetElement(bgId) as FillPatternElement;
                            if (fpe != null) frData.TypeData.BackgroundPatternName = fpe.Name;
                        }
                    } catch { }

                    try {
                        OverrideGraphicSettings overrides = uidoc.ActiveView.GetElementOverrides(fr.Id);
                        if (overrides != null) {
                            try {
                                ElementId fgOverrideId = overrides.SurfaceForegroundPatternId;
                                if (fgOverrideId != ElementId.InvalidElementId) {
                                    FillPatternElement fpe = doc.GetElement(fgOverrideId) as FillPatternElement;
                                    if (fpe != null) {
                                        frData.OverrideForegroundPatternName = fpe.Name;
                                        frData.HasOverride = true;
                                    }
                                }
                            } catch {}

                            try {
                                Color fgColor = overrides.SurfaceForegroundPatternColor;
                                if (fgColor.IsValid) {
                                    frData.OverrideColorRed = fgColor.Red;
                                    frData.OverrideColorGreen = fgColor.Green;
                                    frData.OverrideColorBlue = fgColor.Blue;
                                    frData.HasOverride = true;
                                }
                            } catch {}

                            try {
                                ElementId bgOverrideId = overrides.SurfaceBackgroundPatternId;
                                if (bgOverrideId != ElementId.InvalidElementId) {
                                    FillPatternElement fpe = doc.GetElement(bgOverrideId) as FillPatternElement;
                                    if (fpe != null) {
                                        frData.OverrideBackgroundPatternName = fpe.Name;
                                        frData.HasOverride = true;
                                    }
                                }
                            } catch {}

                            try {
                                Color bgColor = overrides.SurfaceBackgroundPatternColor;
                                if (bgColor.IsValid) {
                                    frData.OverrideBackgroundColorRed = bgColor.Red;
                                    frData.OverrideBackgroundColorGreen = bgColor.Green;
                                    frData.OverrideBackgroundColorBlue = bgColor.Blue;
                                    frData.HasOverride = true;
                                }
                            } catch {}
                        }
                    } catch { }

                    IList<CurveLoop> loops = fr.GetBoundaries();
                    foreach (CurveLoop loop in loops)
                    {
                        List<CurveData> loopData = new List<CurveData>();
                        foreach (Curve curve in loop)
                        {
                            CurveData cData = ConvertCurve(curve);
                            if (cData != null) loopData.Add(cData);
                        }
                        frData.Boundaries.Add(loopData);
                    }

                    exportModel.FilledRegions.Add(frData);
                }
                else if (elem is TextNote textNote)
                {
                    TextNoteType type = doc.GetElement(textNote.GetTypeId()) as TextNoteType;
                    if (type == null) continue;

                    TextNoteData tData = new TextNoteData
                    {
                        Text = textNote.Text,
                        Location = new PointData(textNote.Coord.X, textNote.Coord.Y, textNote.Coord.Z),
                        BaseDirection = new PointData(textNote.BaseDirection.X, textNote.BaseDirection.Y, textNote.BaseDirection.Z),
                        UpDirection = new PointData(textNote.UpDirection.X, textNote.UpDirection.Y, textNote.UpDirection.Z),
                        Width = textNote.Width,
                        HorizontalAlignment = textNote.HorizontalAlignment.ToString(),
                        VerticalAlignment = textNote.VerticalAlignment.ToString()
                    };

                    tData.TypeData = new TextNoteTypeData
                    {
                        Name = type.Name
                    };

                    try {
                        int cInt = type.get_Parameter(BuiltInParameter.LINE_COLOR).AsInteger();
                        // Color in Revit is usually represented as R + 256 * G + 65536 * B
                        int red = cInt % 256;
                        int green = (cInt / 256) % 256;
                        int blue = (cInt / 65536) % 256;
                        
                        tData.TypeData.ColorRed = red;
                        tData.TypeData.ColorGreen = green;
                        tData.TypeData.ColorBlue = blue;
                    } catch { }

                    try { tData.TypeData.TextSize = type.get_Parameter(BuiltInParameter.TEXT_SIZE).AsDouble(); } catch { }
                    try { tData.TypeData.FontName = type.get_Parameter(BuiltInParameter.TEXT_FONT).AsString(); } catch { }
                    try { tData.TypeData.Bold = type.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD).AsInteger(); } catch { }
                    try { tData.TypeData.Italic = type.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC).AsInteger(); } catch { }
                    try { tData.TypeData.Underline = type.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE).AsInteger(); } catch { }
                    try { tData.TypeData.WidthScale = type.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE).AsDouble(); } catch { }

                    exportModel.Texts.Add(tData);
                }
                else if (elem is CurveElement curveElement)
                {
                    CurveData cData = ConvertCurve(curveElement.GeometryCurve);
                    if (cData != null)
                    {
                        CurveElementData ceData = new CurveElementData
                        {
                            Curve = cData,
                            IsModelCurve = curveElement is ModelCurve
                        };

                        GraphicsStyle gs = curveElement.LineStyle as GraphicsStyle;
                        if (gs != null && gs.GraphicsStyleCategory != null)
                        {
                            ceData.LineStyleName = gs.GraphicsStyleCategory.Name;
                        }

                        exportModel.Lines.Add(ceData);
                    }
                }
            }

            // Save JSON
            string json = JsonHelper.Serialize(exportModel);
            File.WriteAllText(SelectedPath, json);
            TaskDialog.Show("Export Success", $"Exported {exportModel.FilledRegions.Count} Filled Regions, {exportModel.Texts.Count} Texts, {exportModel.Lines.Count} Lines to JSON.");
        }

        private CurveData ConvertCurve(Curve curve)
        {
            CurveData data = new CurveData();
            
            try 
            {
                if (curve.IsBound)
                {
                    data.StartPoint = new PointData(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, curve.GetEndPoint(0).Z);
                    data.EndPoint = new PointData(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, curve.GetEndPoint(1).Z);
                }
                else
                {
                    XYZ p0 = curve.Evaluate(0, true);
                    XYZ p1 = curve.Evaluate(1, true);
                    data.StartPoint = new PointData(p0.X, p0.Y, p0.Z);
                    data.EndPoint = new PointData(p1.X, p1.Y, p1.Z);
                }
            } 
            catch 
            {
                data.StartPoint = new PointData(0, 0, 0);
                data.EndPoint = new PointData(0, 0, 0);
            }
            
            if (curve is Line)
            {
                data.CurveType = "Line";
            }
            else if (curve is Arc arc)
            {
                data.CurveType = "Arc";
                data.Center = new PointData(arc.Center.X, arc.Center.Y, arc.Center.Z);
                data.Radius = arc.Radius;
                data.XDirection = new PointData(arc.XDirection.X, arc.XDirection.Y, arc.XDirection.Z);
                data.YDirection = new PointData(arc.YDirection.X, arc.YDirection.Y, arc.YDirection.Z);
                data.Normal = new PointData(arc.Normal.X, arc.Normal.Y, arc.Normal.Z);
                try {
                    data.StartParameter = arc.GetEndParameter(0);
                    data.EndParameter = arc.GetEndParameter(1);
                } catch {
                    data.StartParameter = 0;
                    data.EndParameter = 2 * Math.PI;
                }
            }
            else if (curve is Ellipse ellipse)
            {
                data.CurveType = "Ellipse";
                data.Center = new PointData(ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);
                data.RadiusX = ellipse.RadiusX;
                data.RadiusY = ellipse.RadiusY;
                data.XDirection = new PointData(ellipse.XDirection.X, ellipse.XDirection.Y, ellipse.XDirection.Z);
                data.YDirection = new PointData(ellipse.YDirection.X, ellipse.YDirection.Y, ellipse.YDirection.Z);
                data.Normal = new PointData(ellipse.Normal.X, ellipse.Normal.Y, ellipse.Normal.Z);
                try {
                    data.StartParameter = ellipse.GetEndParameter(0);
                    data.EndParameter = ellipse.GetEndParameter(1);
                } catch {
                    data.StartParameter = 0;
                    data.EndParameter = 2 * Math.PI;
                }
            }
            else if (curve is NurbSpline)
            {
                data.CurveType = "NurbSpline";
                NurbSpline spline = curve as NurbSpline;
                data.Degree = spline.Degree;
#if REVIT2021_OR_GREATER
                data.IsClosed = spline.IsClosed;
                data.IsRational = spline.isRational;
#else
                data.IsClosed = spline.isClosed;
                data.IsRational = spline.isRational;
#endif
                data.ControlPoints = spline.CtrlPoints.Select(p => new PointData(p.X, p.Y, p.Z)).ToList();
                data.Knots = spline.Knots.Cast<double>().ToList();
                data.Weights = spline.Weights.Cast<double>().ToList();
            }
            else if (curve is HermiteSpline hermite)
            {
                data.CurveType = "HermiteSpline";
                data.ControlPoints = hermite.ControlPoints.Select(p => new PointData(p.X, p.Y, p.Z)).ToList();
#if REVIT2021_OR_GREATER
                data.IsClosed = hermite.IsClosed;
#else
                try { data.IsClosed = hermite.IsBound && hermite.GetEndPoint(0).IsAlmostEqualTo(hermite.GetEndPoint(1)); } catch { data.IsClosed = false; }
#endif
            }
            else
            {
                // Fallback
                data.CurveType = "Line";
            }
            return data;
        }
        #endregion

        #region Import JSON Logic
        private void RunImportJson(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (string.IsNullOrEmpty(SelectedPath)) return;

            // Only allow running in a view
            if (uidoc.ActiveView == null || (uidoc.ActiveView.ViewType != ViewType.DraftingView && uidoc.ActiveView.ViewType != ViewType.FloorPlan && uidoc.ActiveView.ViewType != ViewType.Section && uidoc.ActiveView.ViewType != ViewType.Elevation && uidoc.ActiveView.ViewType != ViewType.Detail))
            {
                TaskDialog.Show("Lỗi", "Vui lòng mở một View 2D để Import Filled Regions.");
                return;
            }

            string json = File.ReadAllText(SelectedPath);
            ConvertHatchModel importModel = JsonHelper.Deserialize<ConvertHatchModel>(json);

            if (importModel == null || importModel.FilledRegions == null)
            {
                TaskDialog.Show("Lỗi", "Định dạng JSON không hợp lệ.");
                return;
            }

            using (Transaction t = new Transaction(doc, "Import Filled Regions"))
            {
                t.Start();
                
                // 1. Recreate missing Fill Patterns
                if (importModel.AllFillPatterns != null)
                {
                    foreach (var fpData in importModel.AllFillPatterns)
                    {
                        var existing = new FilteredElementCollector(doc)
                            .OfClass(typeof(FillPatternElement))
                            .Cast<FillPatternElement>()
                            .FirstOrDefault(x => x.Name == fpData.Name);
                            
                        if (existing == null)
                        {
                            try
                            {
                                FillPatternTarget target = fpData.Target == "Drafting" ? FillPatternTarget.Drafting : FillPatternTarget.Model;
                                FillPattern newPattern = new FillPattern(fpData.Name, target, FillPatternHostOrientation.ToView);
                                
                                List<FillGrid> grids = new List<FillGrid>();
                                foreach (var gridData in fpData.Grids)
                                {
                                    FillGrid grid = new FillGrid();
                                    grid.Angle = gridData.Angle;
                                    grid.Origin = new UV(gridData.OriginU, gridData.OriginV);
                                    grid.Offset = gridData.Offset;
                                    grid.Shift = gridData.Shift;
                                    grid.SetSegments(gridData.Segments);
                                    grids.Add(grid);
                                }
                                newPattern.SetFillGrids(grids);
                                FillPatternElement.Create(doc, newPattern);
                            }
                            catch { }
                        }
                    }
                }

                int successCount = 0;
                if (importModel.FilledRegions != null)
                {
                    foreach (var frData in importModel.FilledRegions)
                    {
                        FilledRegionType type = GetOrCreateFilledRegionType(doc, frData.TypeData);
                        if (type == null) continue;

                        List<CurveLoop> curveLoops = new List<CurveLoop>();
                        foreach (var loopData in frData.Boundaries)
                        {
                            CurveLoop loop = new CurveLoop();
                            foreach (var curveData in loopData)
                            {
                                try {
                                    Curve c = ConvertDataToCurve(curveData);
                                    if (c != null) loop.Append(c);
                                } catch { }
                            }
                            if (loop.IsOpen() == false)
                            {
                                curveLoops.Add(loop);
                            }
                        }

                          if (curveLoops.Count > 0)
                          {
                              try {
                                  FilledRegion newFr = FilledRegion.Create(doc, type.Id, uidoc.ActiveView.Id, curveLoops);
                                  
                                  if (frData.HasOverride) {
                                      OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
                                      
                                      if (!string.IsNullOrEmpty(frData.OverrideForegroundPatternName)) {
                                          var fgPattern = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().FirstOrDefault(x => x.Name == frData.OverrideForegroundPatternName);
                                          if (fgPattern != null) overrideSettings.SetSurfaceForegroundPatternId(fgPattern.Id);
                                      }
                                      
                                      overrideSettings.SetSurfaceForegroundPatternColor(new Color((byte)frData.OverrideColorRed, (byte)frData.OverrideColorGreen, (byte)frData.OverrideColorBlue));

                                      if (!string.IsNullOrEmpty(frData.OverrideBackgroundPatternName)) {
                                          var bgPattern = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().FirstOrDefault(x => x.Name == frData.OverrideBackgroundPatternName);
                                          if (bgPattern != null) overrideSettings.SetSurfaceBackgroundPatternId(bgPattern.Id);
                                      }

                                      try {
                                          overrideSettings.SetSurfaceBackgroundPatternColor(new Color((byte)frData.OverrideBackgroundColorRed, (byte)frData.OverrideBackgroundColorGreen, (byte)frData.OverrideBackgroundColorBlue));
                                      } catch {}
                                      
                                      uidoc.ActiveView.SetElementOverrides(newFr.Id, overrideSettings);
                                  }
                                  
                                  successCount++;
                              } catch { }
                          }
                    }
                }

                int textCount = 0;
                if (importModel.Texts != null)
                {
                    foreach (var tData in importModel.Texts)
                    {
                        TextNoteType tType = GetOrCreateTextNoteType(doc, tData.TypeData);
                        if (tType == null) continue;
                        try {
                            XYZ loc = new XYZ(tData.Location.X, tData.Location.Y, tData.Location.Z);
                            TextNoteOptions options = new TextNoteOptions(tType.Id);
                            if (Enum.TryParse(tData.HorizontalAlignment, out HorizontalTextAlignment hAlign)) options.HorizontalAlignment = hAlign;
                            if (Enum.TryParse(tData.VerticalAlignment, out VerticalTextAlignment vAlign)) options.VerticalAlignment = vAlign;
                            
                            TextNote.Create(doc, uidoc.ActiveView.Id, loc, tData.Width, tData.Text, options);
                            textCount++;
                        } catch { }
                    }
                }

                int lineCount = 0;
                if (importModel.Lines != null)
                {
                    foreach (var lData in importModel.Lines)
                    {
                        try {
                            Curve c = ConvertDataToCurve(lData.Curve);
                            if (c != null)
                            {
                                DetailCurve dc = doc.Create.NewDetailCurve(uidoc.ActiveView, c);
                                if (!string.IsNullOrEmpty(lData.LineStyleName))
                                {
                                    GraphicsStyle gs = new FilteredElementCollector(doc)
                                        .OfClass(typeof(GraphicsStyle))
                                        .Cast<GraphicsStyle>()
                                        .FirstOrDefault(x => x.GraphicsStyleCategory != null && x.GraphicsStyleCategory.Name == lData.LineStyleName);
                                    
                                    if (gs != null)
                                    {
                                        dc.LineStyle = gs;
                                    }
                                }
                                lineCount++;
                            }
                        } catch { }
                    }
                }

                t.Commit();
                TaskDialog.Show("Import Success", $"Đã import thành công {successCount} Filled Regions, {textCount} Texts, {lineCount} Lines.");
            }
        }

        private TextNoteType GetOrCreateTextNoteType(Document doc, TextNoteTypeData data)
        {
            if (data == null || string.IsNullOrEmpty(data.Name)) return null;

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault(x => x.Name == data.Name);

            if (existing != null) return existing;

            var baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .FirstOrDefault();

            if (baseType == null) return null;

            TextNoteType newType = baseType.Duplicate(data.Name) as TextNoteType;
            if (newType != null)
            {
                try {
                    int cInt = data.ColorRed + data.ColorGreen * 256 + data.ColorBlue * 65536;
                    newType.get_Parameter(BuiltInParameter.LINE_COLOR)?.Set(cInt); 
                } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_SIZE)?.Set(data.TextSize); } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_FONT)?.Set(data.FontName); } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_STYLE_BOLD)?.Set(data.Bold); } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_STYLE_ITALIC)?.Set(data.Italic); } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_STYLE_UNDERLINE)?.Set(data.Underline); } catch { }
                try { newType.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE)?.Set(data.WidthScale); } catch { }
            }

            return newType;
        }

        private FilledRegionType GetOrCreateFilledRegionType(Document doc, FilledRegionTypeData data)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(x => x.Name == data.Name);
            
            if (existing != null) return existing;

            var baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault();
                
            if (baseType == null) return null;
            
            FilledRegionType newType = baseType.Duplicate(data.Name) as FilledRegionType;
            
            newType.ForegroundPatternColor = new Color((byte)data.ColorRed, (byte)data.ColorGreen, (byte)data.ColorBlue);
            newType.LineWeight = data.LineWeight;
            newType.IsMasking = data.IsMasking;
            
            try {
                newType.BackgroundPatternColor = new Color((byte)data.BackgroundColorRed, (byte)data.BackgroundColorGreen, (byte)data.BackgroundColorBlue);
            } catch {}

            // Identity Data
            if (data.Description != null) newType.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION)?.Set(data.Description);
            if (data.Model != null) newType.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL)?.Set(data.Model);
            if (data.Manufacturer != null) newType.get_Parameter(BuiltInParameter.ALL_MODEL_MANUFACTURER)?.Set(data.Manufacturer);
            if (data.TypeComments != null) newType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS)?.Set(data.TypeComments);
            if (data.Url != null) newType.get_Parameter(BuiltInParameter.ALL_MODEL_URL)?.Set(data.Url);
            if (data.Keynote != null) newType.get_Parameter(BuiltInParameter.KEYNOTE_PARAM)?.Set(data.Keynote);

            if (!string.IsNullOrEmpty(data.ForegroundPatternName))
            {
                var fgPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(x => x.Name == data.ForegroundPatternName);
                    
                if (fgPattern != null)
                {
                    try {
                        newType.ForegroundPatternId = fgPattern.Id;
                    } catch {
                    }
                }
            }
            else
            {
                try {
                    newType.ForegroundPatternId = ElementId.InvalidElementId;
                } catch {
                }
            }
            
            if (!string.IsNullOrEmpty(data.BackgroundPatternName))
            {
                var bgPattern = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(x => x.Name == data.BackgroundPatternName);
                    
                if (bgPattern != null)
                {
                    try {
                        newType.BackgroundPatternId = bgPattern.Id;
                    } catch { }
                }
            }
            else
            {
                try {
                    newType.BackgroundPatternId = ElementId.InvalidElementId;
                } catch { }
            }

            return newType;
        }

        private Curve ConvertDataToCurve(CurveData data)
        {
            XYZ start = new XYZ(data.StartPoint.X, data.StartPoint.Y, data.StartPoint.Z);
            XYZ end = new XYZ(data.EndPoint.X, data.EndPoint.Y, data.EndPoint.Z);
            
            if (data.CurveType == "Line")
            {
                return Line.CreateBound(start, end);
            }
            else if (data.CurveType == "Arc")
            {
                XYZ center = new XYZ(data.Center.X, data.Center.Y, data.Center.Z);
                XYZ xDir = new XYZ(data.XDirection.X, data.XDirection.Y, data.XDirection.Z);
                XYZ yDir = new XYZ(data.YDirection.X, data.YDirection.Y, data.YDirection.Z);
                return Arc.Create(center, data.Radius, data.StartParameter, data.EndParameter, xDir, yDir);
            }
            else if (data.CurveType == "Ellipse")
            {
                XYZ center = new XYZ(data.Center.X, data.Center.Y, data.Center.Z);
                XYZ xDir = new XYZ(data.XDirection.X, data.XDirection.Y, data.XDirection.Z);
                XYZ yDir = new XYZ(data.YDirection.X, data.YDirection.Y, data.YDirection.Z);
                return Ellipse.CreateCurve(center, data.RadiusX, data.RadiusY, xDir, yDir, data.StartParameter, data.EndParameter);
            }
            else if (data.CurveType == "NurbSpline")
            {
                IList<XYZ> ctrlPts = data.ControlPoints.Select(p => new XYZ(p.X, p.Y, p.Z)).ToList();
                return NurbSpline.CreateCurve(data.Degree, data.Knots, ctrlPts, data.Weights);
            }
            else if (data.CurveType == "HermiteSpline")
            {
                IList<XYZ> ctrlPts = data.ControlPoints.Select(p => new XYZ(p.X, p.Y, p.Z)).ToList();
                return HermiteSpline.Create(ctrlPts, data.IsClosed);
            }
            
            // Fallback
            return Line.CreateBound(start, end);
        }
        #endregion

        #region Export PAT Logic
        private void RunExportPat(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (string.IsNullOrEmpty(SelectedPath)) return;

            var allPatterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .ToList();

            int exportedCount = 0;
            double scaleToMM = 304.8; // Đổi từ Feet (internal của Revit) sang MM

            foreach (var fpe in allPatterns)
            {
                try
                {
                    FillPattern fp = fpe.GetFillPattern();
                    if (fp.IsSolidFill) continue;

                    string typeStr = fp.Target == FillPatternTarget.Drafting ? "DRAFTING" : "MODEL";
                    
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(";        Written by Batch Export PAT tool (BIMTOOL)");
                    sb.AppendLine($";-Date                                   : {DateTime.Now:yyyy-MM-dd}");
                    sb.AppendLine($";-Time                                   : {DateTime.Now:HH:mm:ss}");
                    sb.AppendLine(";---------------------------------------------------------------------");
                    sb.AppendLine(";%UNITS=MM");
                    sb.AppendLine($"*{fpe.Name}, exported by BIMTOOL");
                    sb.AppendLine($";%TYPE={typeStr}");

                    IList<FillGrid> grids = fp.GetFillGrids();
                    foreach (FillGrid grid in grids)
                    {
                        double angleDeg = grid.Angle * 180.0 / Math.PI;
                        double originX = grid.Origin.U * scaleToMM;
                        double originY = grid.Origin.V * scaleToMM;
                        double shift = grid.Shift * scaleToMM;
                        double offset = grid.Offset * scaleToMM;

                        string gridLine = string.Format("{0:0.######}, {1:0.######}, {2:0.######}, {3:0.######}, {4:0.######}", 
                            angleDeg, originX, originY, shift, offset);

                        IList<double> segments = grid.GetSegments();
                        if (segments != null && segments.Count > 0)
                        {
                            StringBuilder segSb = new StringBuilder();
                            for (int i = 0; i < segments.Count; i++)
                            {
                                double segVal = segments[i] * scaleToMM;
                                
                                if (i % 2 != 0)
                                {
                                    segVal = -Math.Abs(segVal);
                                }
                                else
                                {
                                    segVal = Math.Abs(segVal);
                                }
                                
                                segSb.AppendFormat(", {0:0.######}", segVal);
                            }
                            gridLine += segSb.ToString();
                        }
                        sb.AppendLine(gridLine);
                    }

                    string safeName = string.Join("_", fpe.Name.Split(Path.GetInvalidFileNameChars()));
                    string filePath = Path.Combine(SelectedPath, safeName + ".pat");
                    
                    File.WriteAllText(filePath, sb.ToString());
                    exportedCount++;
                }
                catch
                {
                }
            }

            TaskDialog.Show("Thành công", $"Đã xuất hàng loạt {exportedCount} mẫu Hatch ra thư mục:\n{SelectedPath}");
        }
        #endregion
    }

    public class MixedExportSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is FilledRegion || elem is TextNote || elem is CurveElement;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}

