
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RincoNhan.Tools.StairDetail;

/// <summary>
/// Core logic for StairDetail tool — generates rebar, dimensions, and tags
/// for stair sections in Revit.
/// </summary>
public class StairDetailLogic
{
    // Unit conversion: 1 foot = 304.8 mm
    private const double MmToFeet = 304.8;

    // Common dimension constants (in feet)
    private static readonly double Feet150mm = 150.0 / MmToFeet;
    private static readonly double Feet200mm = 200.0 / MmToFeet;
    private static readonly double Feet250mm = 250.0 / MmToFeet;
    private static readonly double Feet300mm = 300.0 / MmToFeet;
    private static readonly double Feet2000mm = 2000.0 / MmToFeet;
    private static readonly double Feet20mm = 20.0 / MmToFeet;
    private static readonly double Feet50mm = 50.0 / MmToFeet;
    private static readonly double Feet70mm = 70.0 / MmToFeet;
    private static readonly double Feet100mm = 100.0 / MmToFeet;

    /// <summary>
    /// Execute the stair detail workflow.
    /// </summary>
    /// <param name="commandData">Revit command data</param>
    /// <param name="thepChuName">Name of main rebar type</param>
    /// <param name="thepPhuName">Name of secondary rebar type</param>
    /// <param name="thepChuSpacing">Main rebar spacing in mm</param>
    /// <param name="thepPhuSpacing">Secondary rebar spacing in mm</param>
    /// <param name="includeRebarLandingTop">Whether to include rebar at landing top</param>
    /// <param name="includeRebarLandingBot">Whether to include rebar at landing bottom</param>
    /// <returns>Result indicating success or failure</returns>
    public Result Run(
        ExternalCommandData commandData,
        string thepChuName,
        string thepPhuName,
        int thepChuSpacing,
        int thepPhuSpacing,
        bool includeRebarLandingTop,
        bool includeRebarLandingBot)
    {
        Guid guid = new Guid("a4fc0b15-1982-498a-b24a-c5e09fcd6b7b");
        Guid currentGuid = commandData.Application.ActiveAddInId.GetGUID();
        UIApplication application = commandData.Application;
        UIDocument activeUIDocument = application.ActiveUIDocument;
        Document document = activeUIDocument.Document;

        StairInfo info = new StairInfo();
        info.ChieuDaiToiThieu = Feet2000mm;

        // Get all rebar bar types
        List<Element> rebarTypes = new FilteredElementCollector(document)
            .OfClass(typeof(RebarBarType))
            .WhereElementIsElementType()
            .ToList();

        // Assign rebar types from names
        foreach (Element item in rebarTypes)
        {
            if (string.Compare(item.Name, thepChuName) == 0)
            {
                info.ThepChu = (RebarBarType)(object)((item is RebarBarType) ? item : null);
            }
            if (string.Compare(item.Name, thepPhuName) == 0)
            {
                info.ThepGiaCuong = (RebarBarType)(object)((item is RebarBarType) ? item : null);
            }
        }

        // Set spacing
        info.ThepChu_KhoangRai = (double)thepChuSpacing / MmToFeet;
        info.ThepGiaCuong_KhoangRai = (double)thepPhuSpacing / MmToFeet;

        if (info.ThepChu == null)
        {
            TaskDialog.Show("Error", "Không tìm thấy loại thép chính!");
            return Result.Failed;
        }

        // Calculate anchorage length
        double doanNeo = Math.Round(30.0 * info.ThepChu.GetBarDiameter() * 12.0 * 25.4 / 50.0) * 50.0 / MmToFeet;
        info.DoanNeo = doanNeo;

        // Find or create the 3D view for geometry intersection
        Element view3dElement = TinhToan.FindElementByName(document, typeof(View3D), "IBIM_OriginView_No-DELETE");
        View3D view3d = (View3D)(object)((view3dElement is View3D) ? view3dElement : null);
        View activeView = document.ActiveView;

        if (view3d == null)
        {
            Transaction createTx = new Transaction(document, "Create 3D");
            createTx.Start();
            FilteredElementCollector collector = new FilteredElementCollector(document);
            View3D templateView = ((IEnumerable)collector.OfClass(typeof(View3D))).Cast<View3D>()
                .First(v3 => !((View)v3).IsTemplate);
            view3d = View3D.CreateIsometric(document, ((Element)templateView).GetTypeId());
            ((View)view3d).DetailLevel = ViewDetailLevel.Fine;
            ((View)view3d).AreAnalyticalModelCategoriesHidden = true;
            ((View)view3d).DisplayStyle = DisplayStyle.HLR;
            ((Element)view3d).Name = "IBIM_OriginView_No-DELETE";
            createTx.Commit();
        }

        // Switch views to initialize geometry
        activeUIDocument.ActiveView = (View)(object)view3d;
        activeUIDocument.ActiveView = activeView;

        // Disable updaters if this addin owns them
        if (currentGuid == guid)
        {
            try { TinhToan.DisableUpdaters(new AddInId(guid), "all"); }
            catch (Exception) { }
        }

        // Find the stair structural framing element in the current view
        FilteredElementCollector framingCollector = new FilteredElementCollector(document, ((Element)document.ActiveView).Id);
        framingCollector.OfCategory(BuiltInCategory.OST_StructuralFraming)
            .OfClass(typeof(FamilyInstance))
            .WhereElementIsNotElementType();
        IList<Element> framingElements = framingCollector.ToElements();

        FamilyInstance stairInstance = null;
        ReferenceIntersector refIntersector = new ReferenceIntersector(
            (ElementFilter)new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),
            FindReferenceTarget.All, view3d);

        Options geomOptions = new Options();
        geomOptions.ComputeReferences = true;
        geomOptions.View = (View)(object)view3d;

        Solid cropSolid = TinhToan.CreateSolidFromBoundingBox(activeView.CropBox);
        int matchCount = 0;
        Solid intersectedSolid = null;
        Solid originSolid = null;
        Solid intersectedSolid2 = null;
        Solid originSolid2 = null;

        foreach (Element framingElement in framingElements)
        {
            List<Solid> instanceSolids = TinhToan.GetSolidByGeometry(framingElement.get_Geometry(geomOptions), instance: true);
            List<Solid> symbolSolids = TinhToan.GetSolidByGeometry(framingElement.get_Geometry(geomOptions), instance: false);

            foreach (Solid solid in instanceSolids)
            {
                Solid boolResult = null;
                try
                {
                    boolResult = BooleanOperationsUtils.ExecuteBooleanOperation(solid, cropSolid, BooleanOperationsType.Intersect);
                }
                catch (Exception) { continue; }

                if ((GeometryObject)(object)boolResult != (GeometryObject)null && boolResult.Volume != 0.0)
                {
                    intersectedSolid = boolResult;
                    matchCount++;
                    originSolid = solid;
                    stairInstance = (FamilyInstance)(object)((framingElement is FamilyInstance) ? framingElement : null);
                }
            }

            foreach (Solid solid in symbolSolids)
            {
                Solid boolResult = null;
                try
                {
                    boolResult = BooleanOperationsUtils.ExecuteBooleanOperation(solid, cropSolid, BooleanOperationsType.Intersect);
                }
                catch (Exception) { continue; }

                if ((GeometryObject)(object)boolResult != (GeometryObject)null && boolResult.Volume != 0.0)
                {
                    intersectedSolid2 = boolResult;
                    originSolid2 = solid;
                }
            }
        }

        if (matchCount != 1)
        {
            TaskDialog.Show("Error", "Không Xác Định Được Cấu Kiện Thang !");
            return Result.Failed;
        }

        info.Host = (Element)(object)stairInstance;
        info.Partition = ((Element)stairInstance).LookupParameter("Mark").AsString();

        Element coverElement = document.GetElement(((Element)stairInstance).LookupParameter("Rebar Cover").AsElementId());
        RebarCoverType coverType = (RebarCoverType)(object)((coverElement is RebarCoverType) ? coverElement : null);
        info.Cover = coverType != null ? coverType.CoverDistance : Feet20mm;

        // Analyze stair geometry
        GetFaceInfo(document, geomOptions, originSolid, intersectedSolid, ref info, refIntersector);

        if (info.Type == -1)
        {
            TaskDialog.Show("Error", "Không Phải Loại Cầu Thang Điển Hình !");
            return Result.Failed;
        }

        // Clone info for symbol geometry
        StairInfo info2 = info.Clone() as StairInfo;
        GetFaceInfo(document, geomOptions, originSolid2, intersectedSolid2, ref info2, null);

        // Start main transaction
        Transaction tx = new Transaction(document, "Stair Detail");
        tx.Start();

        // Find rebar tag family symbols
        foreach (Element tagElement in new FilteredElementCollector(document)
            .WhereElementIsElementType()
            .OfCategory(BuiltInCategory.OST_RebarTags)
            .ToElements())
        {
            FamilySymbol fs = (FamilySymbol)(object)((tagElement is FamilySymbol) ? tagElement : null);
            if (fs != null && ((ElementType)fs).FamilyName == "IBIM_Tag_Rebar (Right)")
            {
                if (((Element)fs).Name == "ĐK@KC_Diagonal")
                    info.Symbol_Main = ((Element)fs).Id;
                if (((Element)fs).Name == "ĐK@KC_Open Dot")
                    info.Symbol_GC = ((Element)fs).Id;
            }
        }

        // Load family if symbols not found
        if (info.Symbol_Main == (ElementId)null || info.Symbol_GC == (ElementId)null)
        {
            string familyPath = "X:\\00. GENE_MANAGEMENT\\IBIM_Tools\\Family\\Template\\IBIM_Tag_Rebar (Right).rfa";
            Family loadedFamily = default(Family);
            document.LoadFamily(familyPath, (IFamilyLoadOptions)(object)new FamilyLoadOption(), out loadedFamily);
            document.Regenerate();

            if (loadedFamily != null)
            {
                foreach (ElementId familySymbolId in loadedFamily.GetFamilySymbolIds())
                {
                    Element fsElement = document.GetElement(familySymbolId);
                    if (fsElement.Name == "ĐK@KC_Diagonal")
                        info.Symbol_Main = familySymbolId;
                    if (fsElement.Name == "ĐK@KC_Open Dot")
                        info.Symbol_GC = familySymbolId;
                }
            }
        }

        // Place dimensions and rebar
        if (info.Type > 0)
        {
            PlaceDim(document, info, info2, "Up");
            PlaceDim(document, info, info2, "Down");

            XYZ rightPoint = XYZ.Zero;
            XYZ leftPoint = XYZ.Zero;

            if (info.IsLeftToRight == 1)
            {
                rightPoint = PlaceDim(document, info, info2, "Right");
                leftPoint = PlaceDim(document, info, info2, "Left");
            }
            else
            {
                leftPoint = PlaceDim(document, info, info2, "Right");
                rightPoint = PlaceDim(document, info, info2, "Left");
            }

            PlaceDim(document, info, info2, "Center");

            // Place spot elevations
            PlaceSpotElevations(document, info, info2, rightPoint, leftPoint);

            // Place beam tags
            if ((GeometryObject)(object)info.BeamDown_Bot != (GeometryObject)null)
                PlaceBeamTag(document, info.BeamDown_Bot);
            if ((GeometryObject)(object)info.BeamUp_Bot != (GeometryObject)null)
                PlaceBeamTag(document, info.BeamUp_Bot);

            // Model rebar
            ModelThepLandingTop(document, info, refIntersector, includeRebarLandingTop);
            ModelThepUpDeck(document, info);
            ModelThepGiaCuongDeck(document, info);
            ModelThepDeck(document, info, info2);

            if (info.Type >= 3)
                ModelThepWall(document, info);

            if (info.Type == 1 || info.Type == 4)
            {
                ModelThepLandingBot(document, info, refIntersector, includeRebarLandingBot);
                ModelThepDownDeck(document, info);
            }
        }

        tx.Commit();

        // Close the temp 3D view
        foreach (UIView openUIView in activeUIDocument.GetOpenUIViews())
        {
            if (openUIView.ViewId == ((Element)view3d).Id)
                openUIView.Close();
        }

        // Re-enable updaters
        if (currentGuid == guid)
        {
            try { TinhToan.EnableUpdaters(new AddInId(new Guid("a4fc0b15-1982-498a-b24a-c5e09fcd6b7b")), "all"); }
            catch (Exception) { }
        }

        return Result.Succeeded;
    }

    #region Spot Elevations

    private void PlaceSpotElevations(Document doc, StairInfo info, StairInfo info2, XYZ rightPoint, XYZ leftPoint)
    {
        // Upper spot elevation
        if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
        {
            SpotDimension sd = PlaceSpotElevation(doc, info.BeamUp_Top, rightPoint);
            if (sd != null)
                ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                    ((Dimension)sd).LeaderEndPosition,
                    GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(info.IsLeftToRight * 250) / MmToFeet));
        }
        else
        {
            SpotDimension sd = PlaceSpotElevation(doc, info2.UpTop, rightPoint);
            if (sd != null)
                ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                    ((Dimension)sd).LeaderEndPosition,
                    GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(info.IsLeftToRight * 250) / MmToFeet));
        }

        // Lower spot elevation
        if (info.Type == 1 || info.Type == 4)
        {
            if ((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null)
            {
                SpotDimension sd = PlaceSpotElevation(doc, info.BeamDown_Top, leftPoint);
                if (sd != null)
                    ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                        ((Dimension)sd).LeaderEndPosition,
                        GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / MmToFeet));
            }
            else
            {
                SpotDimension sd = PlaceSpotElevation(doc, info2.DownTop, leftPoint);
                if (sd != null)
                    ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                        ((Dimension)sd).LeaderEndPosition,
                        GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / MmToFeet));
            }
        }
        else if (info.Type == 3)
        {
            SpotDimension sd = PlaceSpotElevation(doc, info2.DownBot, leftPoint);
            if (sd != null)
                ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                    ((Dimension)sd).LeaderEndPosition,
                    GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / MmToFeet));
        }
        else
        {
            SpotDimension sd = doc.Create.NewSpotElevation(
                doc.ActiveView, info2.DeckTop_EdgeBot.Reference,
                leftPoint, leftPoint, leftPoint, leftPoint, true);
            doc.Regenerate();
            ((Dimension)sd).TextPosition = leftPoint.Add(doc.ActiveView.UpDirection * Feet70mm);
            ((Dimension)sd).LeaderEndPosition = GeomUtil.AddXYZ(
                ((Dimension)sd).LeaderEndPosition,
                GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / MmToFeet));
        }
    }

    #endregion

    #region Rebar Modeling — Wall

    private void ModelThepWall(Document doc, StairInfo info)
    {
        double doanNeo = info.DoanNeo;
        XYZ negDirection = doc.ActiveView.RightDirection.Negate() * (double)info.IsLeftToRight;

        Line wallTopLine = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() * 0.5, sameFaceNormal: false);
        Line wallBotLine = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() * 0.5, sameFaceNormal: false);
        Line deckTopLine = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() * 0.5, sameFaceNormal: false);

        Line downBotLine;
        if (info.Type == 3)
            downBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, doanNeo, sameFaceNormal: true);
        else
            downBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() * 0.5, sameFaceNormal: false);

        if (deckTopLine.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
        {
            Curve reversed = ((Curve)deckTopLine).CreateReversed();
            deckTopLine = (Line)(object)((reversed is Line) ? reversed : null);
        }

        // Top rebar along wall
        XYZ pt1 = TinhToan.GiaoDiem((Curve)(object)wallTopLine, (Curve)(object)downBotLine);
        XYZ pt2 = TinhToan.GiaoDiem((Curve)(object)wallTopLine, (Curve)(object)deckTopLine);
        XYZ pt1_ext = GeomUtil.AddXYZ(pt1, GeomUtil.MultiplyVector(negDirection, doanNeo));
        XYZ pt2_ext = GeomUtil.AddXYZ(pt2, GeomUtil.MultiplyVector(deckTopLine.Direction, doanNeo));

        List<Curve> curves1 = new List<Curve>();
        if (info.Type == 4)
            curves1.Add((Curve)(object)Line.CreateBound(pt1_ext, pt1));
        curves1.Add((Curve)(object)Line.CreateBound(pt1, pt2));
        curves1.Add((Curve)(object)Line.CreateBound(pt2, pt2_ext));

        Rebar rb1 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)curves1, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)rb1).LookupParameter("Partition").Set(info.Partition);
        rb1.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        XYZ tagCenter = GeomUtil.Middle2Point(pt1, pt2);
        DatTagRebar(doc, rb1, tagCenter, info, new string[] { "Main", "650", "0" });

        // Bottom rebar along wall
        XYZ pt3 = TinhToan.GiaoDiem((Curve)(object)wallBotLine, (Curve)(object)downBotLine);
        XYZ pt4 = TinhToan.GiaoDiem((Curve)(object)wallBotLine, (Curve)(object)deckTopLine);
        XYZ pt3_ext = GeomUtil.AddXYZ(pt3, GeomUtil.MultiplyVector(negDirection, doanNeo));
        XYZ pt4_ext = GeomUtil.AddXYZ(pt4, GeomUtil.MultiplyVector(deckTopLine.Direction, doanNeo));

        List<Curve> curves2 = new List<Curve>();
        if (info.Type == 4)
            curves2.Add((Curve)(object)Line.CreateBound(pt3_ext, pt3));
        curves2.Add((Curve)(object)Line.CreateBound(pt3, pt4));
        curves2.Add((Curve)(object)Line.CreateBound(pt4, pt4_ext));

        Rebar rb2 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)curves2, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)rb2).LookupParameter("Partition").Set(info.Partition);
        rb2.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        tagCenter = GeomUtil.Middle2Point(pt1, pt2);
        DatTagRebar(doc, rb2, tagCenter, info, new string[] { "Main", "650", "250" });

        // Gia cuong rebar along wall
        Line downLine2;
        if (info.Type == 3)
            downLine2 = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
        else
            downLine2 = CreateLineByFace(doc, info.DownTop, info.Mp_start, 10, info.Cover + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: true);

        wallBotLine = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
        Line wallTopLine2 = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
        Line deckBotLine = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10,
            info.Cover - info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);

        XYZ gcPt1 = TinhToan.GiaoDiem((Curve)(object)wallBotLine, (Curve)(object)downLine2);
        XYZ gcPt2 = TinhToan.GiaoDiem((Curve)(object)wallBotLine, (Curve)(object)deckBotLine);
        XYZ gcOffset = GeomUtil.SubXYZ(TinhToan.GiaoDiem((Curve)(object)wallTopLine2, (Curve)(object)downLine2), gcPt1);

        Plane startPlane = CreatePlane(info.StartFace, 0.0 - info.Cover);
        Plane endPlane = CreatePlane(info.EndFace, 0.0 - info.Cover);
        XYZ gcStart = TinhToan.ProjectOnPlane(gcPt1, startPlane);
        XYZ gcEnd = TinhToan.ProjectOnPlane(gcPt1, endPlane);

        Rebar gcRb1 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepGiaCuong,
            null, null, info.Host, doc.ActiveView.UpDirection,
            (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(gcStart, gcEnd) },
            (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)gcRb1).LookupParameter("Partition").Set(info.Partition);
        gcRb1.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai,
            GeomUtil.KhoangCach(gcPt1, gcPt2), true, true, true);

        Element copiedElement = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)gcRb1).Id, gcOffset).First());
        Rebar gcRb2 = (Rebar)(object)((copiedElement is Rebar) ? copiedElement : null);

        tagCenter = gcRb2.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(GeomUtil.AddXYZ(gcPt1, gcOffset));
        DatTagRebar(doc, gcRb2, tagCenter, info, new string[] { "GC", "650", "0" });
        IndependentTag gcTag = DatTagRebar(doc, gcRb1, tagCenter, info, new string[] { "GC", "650", "0" });
        gcTag.LeaderEnd(GeomUtil.AddXYZ(tagCenter, gcOffset.Negate()));
    }

    #endregion

    #region Rebar Modeling — Landing Top

    private void ModelThepLandingTop(Document doc, StairInfo info, ReferenceIntersector refIntersector, bool includeRebarLandingTop)
    {
        Line upBotLine = CreateLineByFace(doc, info.UpBot, info.Mp_view, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
        Line riserLine = CreateLineByFace(doc, info.Riser, info.Mp_view, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter(), sameFaceNormal: false);

        Line verticalLine;
        if (info.IsLeftToRight == 1)
        {
            verticalLine = ((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null)
                ? CreateLineByFace(doc, info.BeamUp_Left, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false)
                : CreateLineByFace(doc, info.UpVertical, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
            if (upBotLine.Direction.DotProduct(doc.ActiveView.RightDirection) > 0.0)
            {
                Curve rev = ((Curve)upBotLine).CreateReversed();
                upBotLine = (Line)(object)((rev is Line) ? rev : null);
            }
        }
        else
        {
            verticalLine = ((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null)
                ? CreateLineByFace(doc, info.BeamUp_Right, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false)
                : CreateLineByFace(doc, info.UpVertical, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
            if (upBotLine.Direction.DotProduct(doc.ActiveView.RightDirection) < 0.0)
            {
                Curve rev = ((Curve)upBotLine).CreateReversed();
                upBotLine = (Line)(object)((rev is Line) ? rev : null);
            }
        }

        Line deckBotLine = CreateLineByFace(doc, info.DeckBot, info.Mp_view, 10,
            info.Cover - info.ThepGiaCuong.GetBarDiameter() / 2.0, sameFaceNormal: false);

        XYZ ptVertical = TinhToan.GiaoDiem((Curve)(object)verticalLine, (Curve)(object)upBotLine);
        XYZ ptDeckBot = TinhToan.GiaoDiem((Curve)(object)deckBotLine, (Curve)(object)upBotLine);
        XYZ ptRiser = TinhToan.GiaoDiem((Curve)(object)riserLine, (Curve)(object)upBotLine);

        Reference startRef, endRef, startRef2, endRef2;
        double proximity1 = FindLandingReference(doc, ptRiser, refIntersector, info.Host, out startRef2, out endRef2);

        Reference startRef3, endRef3;
        double proximity2 = FindLandingReference(doc, ptVertical, refIntersector, info.Host, out startRef3, out endRef3);

        if (Math.Abs(proximity1 - proximity2) < 1E-05 || proximity1 > proximity2)
        {
            if (!includeRebarLandingTop) return;
            CreateRebarLanding(doc, info, startRef2, endRef2, ptVertical, ptDeckBot, includedTag: true, top: true);
            foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, ptDeckBot, ptRiser, includedTag: false, top: true))
                rb.IncludeFirstBar = false;
            return;
        }

        Reference startRef4, endRef4;
        double proximity3 = FindLandingReference(doc, ptDeckBot, refIntersector, info.Host, out startRef4, out endRef4);

        if (Math.Abs(proximity1 - proximity3) < 1E-05)
        {
            foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, ptDeckBot, ptRiser, includedTag: true, top: true))
                rb.IncludeFirstBar = false;

            XYZ breakPt = FindLandingBreakPoint(doc, info, refIntersector, ptDeckBot, ptVertical, proximity3, out startRef, out endRef);
            if (breakPt != null)
            {
                foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, breakPt, ptDeckBot, includedTag: false, top: true))
                    rb.IncludeFirstBar = false;
                if (includeRebarLandingTop)
                    CreateRebarLanding(doc, info, startRef, endRef, ptVertical, breakPt, includedTag: true, top: true);
            }
            else if (includeRebarLandingTop)
            {
                CreateRebarLanding(doc, info, startRef4, endRef4, ptVertical, ptDeckBot, includedTag: true, top: true);
            }
            return;
        }

        XYZ breakPt2 = FindLandingBreakPoint(doc, info, refIntersector, ptRiser, ptDeckBot, proximity1, out startRef, out endRef);
        if (breakPt2 != null)
        {
            foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, breakPt2, ptRiser, includedTag: false, top: true))
                rb.IncludeFirstBar = false;

            if (!includeRebarLandingTop) return;

            if (GeomUtil.KhoangCach(breakPt2, ptDeckBot) > 1E-05)
            {
                foreach (Rebar rb in CreateRebarLanding(doc, info, startRef, endRef, ptDeckBot, breakPt2, includedTag: false, top: true))
                    rb.IncludeFirstBar = false;
            }
            CreateRebarLanding(doc, info, startRef, endRef, ptVertical, ptDeckBot, includedTag: true, top: true);
            return;
        }

        foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, ptDeckBot, ptRiser, includedTag: false, top: true))
            rb.IncludeFirstBar = false;

        if (includeRebarLandingTop)
            CreateRebarLanding(doc, info, startRef4, endRef4, ptVertical, ptDeckBot, includedTag: true, top: true);
    }

    #endregion

    #region Rebar Modeling — Landing Bot

    private void ModelThepLandingBot(Document doc, StairInfo info, ReferenceIntersector refIntersector, bool includeRebarLandingBot)
    {
        Line downBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_view, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);

        Line verticalLine;
        if (info.IsLeftToRight == 1)
        {
            verticalLine = ((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
                ? CreateLineByFace(doc, info.BeamDown_Right, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false)
                : CreateLineByFace(doc, info.DownVertical, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
            if (downBotLine.Direction.DotProduct(doc.ActiveView.RightDirection) < 0.0)
            {
                Curve rev = ((Curve)downBotLine).CreateReversed();
                downBotLine = (Line)(object)((rev is Line) ? rev : null);
            }
        }
        else
        {
            verticalLine = ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null)
                ? CreateLineByFace(doc, info.BeamDown_Left, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false)
                : CreateLineByFace(doc, info.DownVertical, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
            if (downBotLine.Direction.DotProduct(doc.ActiveView.RightDirection) > 0.0)
            {
                Curve rev = ((Curve)downBotLine).CreateReversed();
                downBotLine = (Line)(object)((rev is Line) ? rev : null);
            }
        }

        Line refLine;
        if (info.Type == 1)
            refLine = CreateLineByFace(doc, info.DeckTop, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);
        else
            refLine = CreateLineByFace(doc, info.WallBot, info.Mp_view, 10, info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() * 0.5, sameFaceNormal: false);

        XYZ ptVertical = TinhToan.GiaoDiem((Curve)(object)verticalLine, (Curve)(object)downBotLine);
        XYZ ptRef = TinhToan.GiaoDiem((Curve)(object)refLine, (Curve)(object)downBotLine);

        Reference startRef, endRef, startRef2, endRef2;
        double proximity1 = FindLandingReference(doc, ptRef, refIntersector, info.Host, out startRef2, out endRef2);

        Reference startRef3, endRef3;
        double proximity2 = FindLandingReference(doc, ptVertical, refIntersector, info.Host, out startRef3, out endRef3);

        if (Math.Abs(proximity1 - proximity2) < 1E-05 || proximity1 > proximity2)
        {
            if (includeRebarLandingBot)
                CreateRebarLanding(doc, info, startRef2, endRef2, ptVertical, ptRef, includedTag: true, top: false);
            return;
        }

        XYZ breakPt = FindLandingBreakPoint(doc, info, refIntersector, ptRef, ptVertical, proximity1, out startRef, out endRef);
        if (breakPt != null)
        {
            foreach (Rebar rb in CreateRebarLanding(doc, info, startRef2, endRef2, breakPt, ptRef, includedTag: true, top: false))
                rb.IncludeFirstBar = false;
            if (includeRebarLandingBot)
                CreateRebarLanding(doc, info, startRef, endRef, ptVertical, breakPt, includedTag: true, top: false);
        }
        else if (includeRebarLandingBot)
        {
            CreateRebarLanding(doc, info, startRef2, startRef2, ptVertical, ptRef, includedTag: true, top: false);
        }
    }

    #endregion

    #region Rebar Modeling — Up Deck

    private void ModelThepUpDeck(Document doc, StairInfo info)
    {
        double extraLength = Feet150mm - info.DoanNeo;
        double doanNeo = info.DoanNeo;

        Line riserLine = CreateLineByFace(doc, info.Riser, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line upTopLine = CreateLineByFace(doc, info.UpTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line upBotLine = CreateLineByFace(doc, info.UpBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line deckBotLine = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover, sameFaceNormal: false);

        Line vertLine;
        bool hasBeam = true;
        if (info.IsLeftToRight == 1)
        {
            if ((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null)
                vertLine = CreateLineByFace(doc, info.BeamUp_Right, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            else
            {
                hasBeam = false;
                vertLine = CreateLineByFace(doc, info.UpVertical, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
                XYZ ptTop = TinhToan.GiaoDiem((Curve)(object)upTopLine, (Curve)(object)vertLine);
                XYZ ptBot = TinhToan.GiaoDiem((Curve)(object)upBotLine, (Curve)(object)vertLine);
                extraLength = 0.0 - GeomUtil.KhoangCach(ptTop, ptBot);
            }
        }
        else
        {
            if ((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null)
                vertLine = CreateLineByFace(doc, info.BeamUp_Left, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            else
            {
                hasBeam = false;
                vertLine = CreateLineByFace(doc, info.UpVertical, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
                XYZ ptTop = TinhToan.GiaoDiem((Curve)(object)upTopLine, (Curve)(object)vertLine);
                XYZ ptBot = TinhToan.GiaoDiem((Curve)(object)upBotLine, (Curve)(object)vertLine);
                extraLength = 0.0 - GeomUtil.KhoangCach(ptTop, ptBot);
            }
        }

        // Top rebar
        XYZ ptDeckBot = TinhToan.GiaoDiem((Curve)(object)deckBotLine, (Curve)(object)riserLine);
        XYZ ptRiser = TinhToan.GiaoDiem((Curve)(object)riserLine, (Curve)(object)upTopLine);
        XYZ ptUpTop = TinhToan.GiaoDiem((Curve)(object)upTopLine, (Curve)(object)vertLine);
        XYZ ptExtended = GeomUtil.AddXYZ(ptUpTop, doc.ActiveView.UpDirection * extraLength);

        List<Curve> topCurves = new List<Curve>();
        topCurves.Add((Curve)(object)Line.CreateBound(ptDeckBot, ptRiser));
        topCurves.Add((Curve)(object)Line.CreateBound(ptRiser, ptUpTop));
        topCurves.Add((Curve)(object)Line.CreateBound(ptUpTop, ptExtended));

        Rebar topRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)topCurves, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)topRb).LookupParameter("Partition").Set(info.Partition);
        topRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        XYZ tagCenter = GeomUtil.Middle2Point(ptRiser, ptUpTop);
        DatTagRebar(doc, topRb, tagCenter, info, new string[] { "Main", "600", "100" });

        // Bottom rebar
        Line deckTopLine = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        if (deckTopLine.Direction.DotProduct(doc.ActiveView.UpDirection) > 0.0)
        {
            Curve rev = ((Curve)deckTopLine).CreateReversed();
            deckTopLine = (Line)(object)((rev is Line) ? rev : null);
        }

        XYZ ptBotVert = TinhToan.GiaoDiem((Curve)(object)upBotLine, (Curve)(object)vertLine);
        XYZ ptBotDeck = TinhToan.GiaoDiem((Curve)(object)upBotLine, (Curve)(object)deckTopLine);
        if (!hasBeam) extraLength = 0.0 - extraLength;

        XYZ ptBotExtended = GeomUtil.AddXYZ(ptBotVert, doc.ActiveView.UpDirection * extraLength);
        XYZ ptBotNeo = GeomUtil.AddXYZ(ptBotDeck, deckTopLine.Direction * doanNeo);

        List<Curve> botCurves = new List<Curve>();
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotExtended, ptBotVert));
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotVert, ptBotDeck));
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotDeck, ptBotNeo));

        Rebar botRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)botCurves, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)botRb).LookupParameter("Partition").Set(info.Partition);
        botRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        tagCenter = GeomUtil.Middle2Point(ptBotVert, ptBotDeck);
        DatTagRebar(doc, botRb, tagCenter, info, new string[] { "Main", "400", "-150" });
    }

    #endregion

    #region Rebar Modeling — Down Deck

    private void ModelThepDownDeck(Document doc, StairInfo info)
    {
        double extraLength = Feet150mm - info.DoanNeo;
        double doanNeo = info.DoanNeo;

        Line downTopLine = CreateLineByFace(doc, info.DownTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line downBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line refLine = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);

        if (info.Type == 4)
            refLine = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);

        if (refLine.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
        {
            Curve rev = ((Curve)refLine).CreateReversed();
            refLine = (Line)(object)((rev is Line) ? rev : null);
        }

        Line vertLine;
        bool hasBeam = true;
        if (info.IsLeftToRight == 1)
        {
            if ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null)
                vertLine = CreateLineByFace(doc, info.BeamDown_Left, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            else
            {
                hasBeam = false;
                vertLine = CreateLineByFace(doc, info.DownVertical, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
                XYZ ptT = TinhToan.GiaoDiem((Curve)(object)downTopLine, (Curve)(object)vertLine);
                XYZ ptB = TinhToan.GiaoDiem((Curve)(object)downBotLine, (Curve)(object)vertLine);
                extraLength = 0.0 - GeomUtil.KhoangCach(ptT, ptB);
            }
        }
        else
        {
            if ((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
                vertLine = CreateLineByFace(doc, info.BeamDown_Right, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            else
            {
                hasBeam = false;
                vertLine = CreateLineByFace(doc, info.DownVertical, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
                XYZ ptT = TinhToan.GiaoDiem((Curve)(object)downTopLine, (Curve)(object)vertLine);
                XYZ ptB = TinhToan.GiaoDiem((Curve)(object)downBotLine, (Curve)(object)vertLine);
                extraLength = 0.0 - GeomUtil.KhoangCach(ptT, ptB);
            }
        }

        // Top rebar
        XYZ ptTopRef = TinhToan.GiaoDiem((Curve)(object)refLine, (Curve)(object)downTopLine);
        XYZ ptTopVert = TinhToan.GiaoDiem((Curve)(object)downTopLine, (Curve)(object)vertLine);
        XYZ ptTopRefExt = GeomUtil.AddXYZ(ptTopRef, refLine.Direction * doanNeo);
        XYZ ptTopVertExt = GeomUtil.AddXYZ(ptTopVert, doc.ActiveView.UpDirection * extraLength);

        List<Curve> topCurves = new List<Curve>();
        topCurves.Add((Curve)(object)Line.CreateBound(ptTopRefExt, ptTopRef));
        topCurves.Add((Curve)(object)Line.CreateBound(ptTopRef, ptTopVert));
        topCurves.Add((Curve)(object)Line.CreateBound(ptTopVert, ptTopVertExt));

        Rebar topRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)topCurves, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)topRb).LookupParameter("Partition").Set(info.Partition);
        topRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        XYZ tagCenter = 0.5 * ptTopRef + 0.5 * ptTopVert;
        DatTagRebar(doc, topRb, tagCenter, info, new string[] { "Main", "600", "100" });

        // Bottom rebar
        XYZ ptBotVert = TinhToan.GiaoDiem((Curve)(object)downBotLine, (Curve)(object)vertLine);
        XYZ ptBotRef = TinhToan.GiaoDiem((Curve)(object)downBotLine, (Curve)(object)refLine);
        if (!hasBeam) extraLength = 0.0 - extraLength;

        XYZ ptBotVertExt = GeomUtil.AddXYZ(ptBotVert, doc.ActiveView.UpDirection * extraLength);
        XYZ ptBotRefExt = GeomUtil.AddXYZ(ptBotRef, refLine.Direction * doanNeo);

        List<Curve> botCurves = new List<Curve>();
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotVertExt, ptBotVert));
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotVert, ptBotRef));
        botCurves.Add((Curve)(object)Line.CreateBound(ptBotRef, ptBotRefExt));

        Rebar botRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)botCurves, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)botRb).LookupParameter("Partition").Set(info.Partition);
        botRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        tagCenter = 0.5 * ptBotRef + 0.5 * ptBotVert;
        DatTagRebar(doc, botRb, tagCenter, info, new string[] { "Main", "400", "-150" });
    }

    #endregion

    // The remaining methods (ModelThepGiaCuongDeck, ModelThepDeck, PlaceDim, PlaceBeamTag,
    // PlaceSpotElevation, GetFaceInfo, FindOriginGeometry, CreateLineByFace, CreatePlane,
    // DatTagRebar, CreateRebarLanding, FindLandingReference, FindLandingBreakPoint,
    // GetRebarReference) are preserved from the original Command.cs with IL comments removed
    // and magic numbers replaced. They are included below.

    #region Rebar Modeling — Gia Cuong Deck

    private void ModelThepGiaCuongDeck(Document doc, StairInfo info)
    {
        Line deckTopRaw = CreateLineByFace(doc, info.DeckTop, info.Mp_view, 0, 0.0, sameFaceNormal: false);
        double lapLength = Math.Ceiling(((Curve)deckTopRaw).Length * 25.4 * 12.0 / 200.0) * Feet50mm;

        Line deckBotLine = CreateLineByFace(doc, info.DeckBot, info.Mp_view, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() / 2.0, sameFaceNormal: false);
        if (deckBotLine.Direction.DotProduct(doc.ActiveView.UpDirection) > 0.0)
        {
            Curve rev = ((Curve)deckBotLine).CreateReversed();
            deckBotLine = (Line)(object)((rev is Line) ? rev : null);
        }

        CreateLineByFace(doc, info.UpTop, info.Mp_view, 10,
            info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter(), sameFaceNormal: false);
        Line riserLine = CreateLineByFace(doc, info.Riser, info.Mp_view, 10,
            info.Cover - info.ThepGiaCuong.GetBarDiameter() / 2.0, sameFaceNormal: false);

        Plane deckTopOffsetPlane = CreatePlane(info.DeckTop,
            0.0 - info.Cover - info.ThepChu.GetBarDiameter() - info.ThepGiaCuong.GetBarDiameter() / 2.0);
        XYZ ptRiser = TinhToan.PlanIntersect(deckTopOffsetPlane, (Curve)(object)riserLine);

        Plane startPlane = CreatePlane(info.StartFace, 0.0 - info.Cover);
        Plane endPlane = CreatePlane(info.EndFace, 0.0 - info.Cover);

        XYZ edgeTopPt = TinhToan.PlanIntersect(info.Mp_view, info.DeckTop_EdgeTop.AsCurve())
            + deckBotLine.Direction * (lapLength - 2.0 * info.ThepChu.GetBarDiameter());
        XYZ edgeTopProjPt = edgeTopPt - info.DeckTop.FaceNormal * 10.0;
        XYZ ptTop = TinhToan.GiaoDiem((Curve)(object)Line.CreateBound(edgeTopPt, edgeTopProjPt), (Curve)(object)deckBotLine);
        XYZ ptTopStart = TinhToan.ProjectOnPlane(ptTop, startPlane);
        XYZ ptTopEnd = TinhToan.ProjectOnPlane(ptTop, endPlane);
        XYZ offsetVector = TinhToan.ProjectOnPlane(ptTop, deckTopOffsetPlane) - ptTop;

        info.Thickness = offsetVector.GetLength() + 2.0 * info.Cover + 2.0 * info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter();

        XYZ edgeBotPt = TinhToan.PlanIntersect(info.Mp_view, info.DeckTop_EdgeBot.AsCurve())
            - deckBotLine.Direction * (lapLength - 2.0 * info.ThepChu.GetBarDiameter());
        XYZ edgeBotProjPt = edgeBotPt - info.DeckTop.FaceNormal * 10.0;
        XYZ ptBot = TinhToan.GiaoDiem((Curve)(object)Line.CreateBound(edgeBotPt, edgeBotProjPt), (Curve)(object)deckBotLine);

        Line refLine;
        if (info.Type == 1)
            refLine = CreateLineByFace(doc, info.DownTop, info.Mp_view, 10,
                info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() / 2.0, sameFaceNormal: false);
        else if (info.Type == 2)
            refLine = CreateLineByFace(doc, info.DownBot, info.Mp_view, 10,
                info.Cover + info.ThepChu.GetBarDiameter(), sameFaceNormal: false);
        else
            refLine = CreateLineByFace(doc, info.WallTop, info.Mp_view, 10,
                info.Cover + info.ThepChu.GetBarDiameter() + info.ThepGiaCuong.GetBarDiameter() / 2.0, sameFaceNormal: false);

        XYZ ptRefEnd = GeomUtil.AddXYZ(TinhToan.PlanIntersect(deckTopOffsetPlane, (Curve)(object)refLine),
            GeomUtil.MultiplyVector(offsetVector, -1.0));
        ptRiser = GeomUtil.AddXYZ(ptRiser, GeomUtil.MultiplyVector(offsetVector, -1.0));

        // Create gia cuong rebars
        Rebar gcRb1 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepGiaCuong,
            null, null, info.Host, deckBotLine.Direction,
            (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(ptTopStart, ptTopEnd) },
            (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)gcRb1).LookupParameter("Partition").Set(info.Partition);
        gcRb1.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai,
            GeomUtil.KhoangCach(ptRiser, ptTop), false, true, true);
        doc.Regenerate();

        ptRiser = gcRb1.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(ptRiser);
        gcRb1.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai,
            GeomUtil.KhoangCach(ptRiser, ptTop), false, true, true);

        Element copiedEl = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)gcRb1).Id, offsetVector).First());
        Rebar gcRb2 = (Rebar)(object)((copiedEl is Rebar) ? copiedEl : null);
        gcRb2.IncludeLastBar = false;

        XYZ botOffset = GeomUtil.SubXYZ(ptBot, ptTop);
        Element copiedEl2 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)gcRb1).Id, botOffset).First());
        Rebar gcRb3 = (Rebar)(object)((copiedEl2 is Rebar) ? copiedEl2 : null);
        gcRb3.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai,
            GeomUtil.KhoangCach(ptTop, ptBot), false, false, false);
        doc.Regenerate();

        if (((Curve)deckTopRaw).Length < info.ChieuDaiToiThieu)
            ElementTransformUtils.CopyElement(doc, ((Element)gcRb3).Id, offsetVector);

        XYZ endOffset = ptRefEnd - ptBot;
        Element copiedEl3 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)gcRb3).Id, endOffset).First());
        Rebar gcRb4 = (Rebar)(object)((copiedEl3 is Rebar) ? copiedEl3 : null);
        gcRb4.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai,
            GeomUtil.KhoangCach(ptBot, ptRefEnd), false, true, true);

        Element copiedEl4 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)gcRb4).Id, offsetVector).First());
        Rebar gcRb5 = (Rebar)(object)((copiedEl4 is Rebar) ? copiedEl4 : null);

        if (info.Type >= 2)
            gcRb4.IncludeFirstBar = false;

        // Tags
        XYZ tagCenter = gcRb2.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(GeomUtil.AddXYZ(ptTop, offsetVector));
        DatTagRebar(doc, gcRb2, tagCenter, info, new string[] { "GC" });
        DatTagRebar(doc, gcRb2, tagCenter, info, new string[] { "GC" }).LeaderEnd(GeomUtil.AddXYZ(ptTop, offsetVector));

        tagCenter = gcRb3.GetShapeDrivenAccessor().GetBarPositionTransform(gcRb3.Quantity / 2).OfPoint(ptBot);
        DatTagRebar(doc, gcRb3, tagCenter, info, new string[] { "GC" });
        DatTagRebar(doc, gcRb3, tagCenter, info, new string[] { "GC" }).LeaderEnd(
            gcRb3.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse.OfPoint(tagCenter));

        if (((Curve)deckTopRaw).Length >= info.ChieuDaiToiThieu)
        {
            tagCenter = GeomUtil.AddXYZ(ptBot, offsetVector);
            DatTagRebar(doc, gcRb5, tagCenter, info, new string[] { "GC" });
            DatTagRebar(doc, gcRb5, tagCenter, info, new string[] { "GC" }).LeaderEnd(
                gcRb5.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse.OfPoint(tagCenter));
        }
    }

    #endregion

    #region Rebar Modeling — Main Deck

    // ModelThepDeck is very long — preserving the original logic with cleaned constants
    // This method is too large to refactor further without risking functional changes
    // Keeping it as-is from the original with IL comments removed and constants applied
    private void ModelThepDeck(Document doc, StairInfo info, StairInfo info_symbol)
    {
        List<List<Curve>> allCurveSets = new List<List<Curve>>();
        double doanNeo = info.DoanNeo;
        XYZ rightDirection = doc.ActiveView.RightDirection;

        Line deckTopRaw = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 0, 0.0, sameFaceNormal: false);
        if (deckTopRaw.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
        {
            Curve rev = ((Curve)deckTopRaw).CreateReversed();
            deckTopRaw = (Line)(object)((rev is Line) ? rev : null);
        }

        Line deckTopLine = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
        Line deckBotLine = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);

        Line refBotLine;
        if (info.Type == 1)
        {
            refBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            rightDirection = (double)(-info.IsLeftToRight) * doc.ActiveView.RightDirection;
        }
        else if (info.Type == 2)
        {
            if ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null && (GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
            {
                rightDirection = doc.ActiveView.UpDirection.Negate();
                refBotLine = (info.IsLeftToRight == 1)
                    ? CreateLineByFace(doc, info.BeamDown_Left, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false)
                    : CreateLineByFace(doc, info.BeamDown_Right, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            }
            else
            {
                double thickness = info.Thickness;
                refBotLine = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, thickness - info.Cover - info.ThepChu.GetBarDiameter() * 0.5, sameFaceNormal: true);
                rightDirection = doc.ActiveView.RightDirection.Negate() * (double)info.IsLeftToRight;
            }
        }
        else
        {
            refBotLine = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);
            rightDirection = doc.ActiveView.UpDirection.Negate();
        }

        Line upTopLine = CreateLineByFace(doc, info.UpTop, info.Mp_start, 10, info.Cover + info.ThepChu.GetBarDiameter() / 2.0, sameFaceNormal: false);

        XYZ ptBot = TinhToan.GiaoDiem((Curve)(object)deckTopLine, (Curve)(object)refBotLine);
        XYZ ptTop = TinhToan.GiaoDiem((Curve)(object)deckTopLine, (Curve)(object)upTopLine);
        XYZ ptBotExt = ptBot + rightDirection * doanNeo;
        XYZ ptTopExt = ptTop + (double)info.IsLeftToRight * doc.ActiveView.RightDirection * doanNeo;

        if (((Curve)deckTopRaw).Length >= info.ChieuDaiToiThieu)
        {
            double lapLen = Math.Ceiling(((Curve)deckTopRaw).Length * 25.4 * 12.0 / 200.0) * Feet50mm;

            // Bottom set
            List<Curve> botSet = new List<Curve>();
            XYZ lapBotPt = ((Curve)deckTopRaw).GetEndPoint(0) + deckTopRaw.Direction * lapLen;
            XYZ lapBotProj = lapBotPt - GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, 10.0);
            Line lapBotLine = Line.CreateBound(lapBotPt, lapBotProj);
            XYZ lapBotTop = TinhToan.GiaoDiem((Curve)(object)lapBotLine, (Curve)(object)deckTopLine);
            XYZ lapBotBot = TinhToan.GiaoDiem((Curve)(object)lapBotLine, (Curve)(object)deckBotLine);
            lapBotBot -= GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, info.ThepChu.GetBarDiameter() / 2.0);
            botSet.Add((Curve)(object)Line.CreateBound(ptBotExt, ptBot));
            botSet.Add((Curve)(object)Line.CreateBound(ptBot, lapBotTop));
            botSet.Add((Curve)(object)Line.CreateBound(lapBotTop, lapBotBot));
            allCurveSets.Add(botSet);

            // Top set
            List<Curve> topSet = new List<Curve>();
            XYZ lapTopPt = ((Curve)deckTopRaw).GetEndPoint(1) - deckTopRaw.Direction * lapLen;
            XYZ lapTopProj = lapTopPt - GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, 10.0);
            Line lapTopLine = Line.CreateBound(lapTopPt, lapTopProj);
            XYZ lapTopTop = TinhToan.GiaoDiem((Curve)(object)lapTopLine, (Curve)(object)deckTopLine);
            XYZ lapTopBot = TinhToan.GiaoDiem((Curve)(object)lapTopLine, (Curve)(object)deckBotLine);
            lapTopBot -= GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, info.ThepChu.GetBarDiameter() / 2.0);
            topSet.Add((Curve)(object)Line.CreateBound(lapTopBot, lapTopTop));
            topSet.Add((Curve)(object)Line.CreateBound(lapTopTop, ptTop));
            topSet.Add((Curve)(object)Line.CreateBound(ptTop, ptTopExt));
            allCurveSets.Add(topSet);
        }
        else
        {
            List<Curve> singleSet = new List<Curve>();
            singleSet.Add((Curve)(object)Line.CreateBound(ptBotExt, ptBot));
            singleSet.Add((Curve)(object)Line.CreateBound(ptBot, ptTop));
            singleSet.Add((Curve)(object)Line.CreateBound(ptTop, ptTopExt));
            allCurveSets.Add(singleSet);
        }

        // Create rebar for each curve set
        List<Rebar> rebarList = new List<Rebar>();
        XYZ lastTagCenter = XYZ.Zero;
        foreach (List<Curve> curveSet in allCurveSets)
        {
            Rebar rb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
                null, null, info.Host, info.Rebar_Direction,
                (IList<Curve>)curveSet, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
            ((Element)rb).LookupParameter("Partition").Set(info.Partition);
            rb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
            rebarList.Add(rb);
            lastTagCenter = curveSet[1].GetEndPoint(0) * 0.4 + curveSet[1].GetEndPoint(1) * 0.6;
            DatTagRebar(doc, rb, lastTagCenter, info, new string[] { "Main" });
        }

        // Bottom chord rebar
        XYZ ptBotBot = TinhToan.GiaoDiem((Curve)(object)deckBotLine, (Curve)(object)refBotLine);
        XYZ ptBotTop2 = TinhToan.GiaoDiem((Curve)(object)deckBotLine, (Curve)(object)upTopLine);
        XYZ ptBotBotExt = ptBotBot + rightDirection * doanNeo;
        XYZ ptBotTopExt2 = ptBotTop2 + (double)info.IsLeftToRight * doc.ActiveView.RightDirection * doanNeo;

        List<Curve> botChordCurves = new List<Curve>();
        botChordCurves.Add((Curve)(object)Line.CreateBound(ptBotBotExt, ptBotBot));
        botChordCurves.Add((Curve)(object)Line.CreateBound(ptBotBot, ptBotTop2));
        botChordCurves.Add((Curve)(object)Line.CreateBound(ptBotTop2, ptBotTopExt2));

        Rebar botChordRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepChu,
            null, null, info.Host, info.Rebar_Direction,
            (IList<Curve>)botChordCurves, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)botChordRb).LookupParameter("Partition").Set(info.Partition);
        botChordRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);

        XYZ botTagCenter = (allCurveSets.Count != 1) ? (ptBotBot * 0.5 + ptBotTop2 * 0.5) : (ptBotBot * 0.8 + ptBotTop2 * 0.2);
        DatTagRebar(doc, botChordRb, botTagCenter, info, new string[] { "Main" });

        // Dimension lines for lap length
        doc.Regenerate();
        if (allCurveSets.Count != 2) return;

        // Place dimensions for lap lengths
        XYZ dimOrigin;
        if (info.IsLeftToRight == 1)
            dimOrigin = GeomUtil.AddXYZ(info.DeckTop.Origin, GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, Feet200mm));
        else
            dimOrigin = GeomUtil.AddXYZ(info.DeckBot.Origin, GeomUtil.MultiplyVector(info.DeckBot.FaceNormal, Feet200mm));

        Options dimOptions = new Options();
        dimOptions.ComputeReferences = true;
        dimOptions.View = doc.ActiveView;
        dimOptions.IncludeNonVisibleObjects = true;

        ReferenceArray refArray1 = new ReferenceArray();
        refArray1.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
        ReferenceArray refArray2 = new ReferenceArray();
        refArray2.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);

        refArray1.Append(GetRebarReference(rebarList[0], allCurveSets.First().Last(), dimOptions));
        refArray2.Append(GetRebarReference(rebarList[1], allCurveSets.Last().First().CreateReversed(), dimOptions));

        Line dimLine = Line.CreateUnbound(dimOrigin,
            doc.ActiveView.ViewDirection.CrossProduct(info.DeckTop.FaceNormal));

        try { ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, dimLine, refArray1); }
        catch (Exception) { }

        try { ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, dimLine, refArray2); }
        catch (Exception) { }
    }

    #endregion

    #region Helper Methods — Geometry & References

    private Reference GetRebarReference(Rebar rb, Curve direction, Options geomOption)
    {
        foreach (GeometryObject geomObj in rb.get_Geometry(geomOption))
        {
            if (((geomObj is Solid) ? geomObj : null) != (GeometryObject)null) continue;

            Line line = (Line)(object)((geomObj is Line) ? geomObj : null);
            if (!((GeometryObject)(object)line != (GeometryObject)null) || ((Curve)line).Reference == null)
                continue;

            Line dirLine = (Line)(object)((direction is Line) ? direction : null);
            if (GeomUtil.IsSameDirection(line.Direction, dirLine.Direction))
            {
                XYZ diff = ((Curve)dirLine).GetEndPoint(1) - ((Curve)line).GetEndPoint(1);
                if (diff.IsZeroLength() || GeomUtil.IsOppositeDirection(diff, ((Element)rb).Document.ActiveView.ViewDirection) || GeomUtil.IsSameDirection(diff, ((Element)rb).Document.ActiveView.ViewDirection))
                    return ((Curve)line).Reference;
            }
            if (GeomUtil.IsOppositeDirection(line.Direction, dirLine.Direction))
            {
                XYZ diff = ((Curve)dirLine).GetEndPoint(1) - ((Curve)line).GetEndPoint(0);
                if (diff.IsZeroLength() || GeomUtil.IsOppositeDirection(diff, ((Element)rb).Document.ActiveView.ViewDirection) || GeomUtil.IsSameDirection(diff, ((Element)rb).Document.ActiveView.ViewDirection))
                    return ((Curve)line).Reference;
            }
        }
        return null;
    }

    private Line CreateLineByFace(Document doc, PlanarFace face, Plane mp, int scale, double offset, bool sameFaceNormal)
    {
        int direction = sameFaceNormal ? 1 : -1;
        XYZEqual xyzEqual = new XYZEqual();
        Line projectedLine = TinhToan.ChieuFaceLenPlane(face, mp, xyzEqual);
        XYZ start = ((Curve)projectedLine).GetEndPoint(0) + (double)direction * face.FaceNormal * offset - projectedLine.Direction * (double)scale;
        XYZ end = ((Curve)projectedLine).GetEndPoint(1) + (double)direction * face.FaceNormal * offset + projectedLine.Direction * (double)scale;
        return Line.CreateBound(start, end);
    }

    private SpotDimension PlaceSpotElevation(Document doc, PlanarFace face, XYZ center)
    {
        if (center == null || center.GetLength() < 1E-05)
            center = TinhToan.GetCenterOfFace(face);
        try
        {
            SpotDimension sd = doc.Create.NewSpotElevation(doc.ActiveView, ((Face)face).Reference, center, center, center, center, true);
            doc.Regenerate();
            ((Dimension)sd).TextPosition = center.Add(doc.ActiveView.UpDirection * Feet70mm);
            return sd;
        }
        catch (Exception) { return null; }
    }

    private Plane CreatePlane(PlanarFace face, double offset)
    {
        XYZ origin = face.Origin + face.FaceNormal * offset;
        return Plane.CreateByNormalAndOrigin(face.FaceNormal, origin);
    }

    private void PlaceBeamTag(Document doc, PlanarFace face)
    {
        BoundingBoxUV bb = ((Face)face).GetBoundingBox();
        XYZ center = ((Face)face).Evaluate((bb.Max + bb.Min) / 2.0);
        IndependentTag tag = IndependentTag.Create(doc, ((Element)doc.ActiveView).Id,
            new Reference(doc.GetElement(((Face)face).Reference)), true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, center);
        doc.Regenerate();

        tag.TagHeadPosition = center + doc.ActiveView.UpDirection.Negate() * (100.0 / MmToFeet);
        BoundingBoxXYZ tagBB = tag.get_BoundingBox(doc.ActiveView);
        XYZ tagCenter = tagBB.Max * 0.5 + tagBB.Min * 0.5;
        tag.TagHeadPosition = new XYZ(
            2.0 * tag.TagHeadPosition.X - tagCenter.X,
            2.0 * tag.TagHeadPosition.Y - tagCenter.Y,
            tag.TagHeadPosition.Z);
    }

    #endregion

    #region Dimensions

    private XYZ PlaceDim(Document doc, StairInfo info, StairInfo info_symbol, string location)
    {
        XYZ dimOrigin = XYZ.Zero;
        XYZ resultPoint = XYZ.Zero;
        Line dimLine = null;
        ReferenceArray refArray = new ReferenceArray();

        FilteredElementCollector gridCollector = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id);
        gridCollector.WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_Grids);

        switch (location)
        {
            case "Up":
                foreach (Grid grid in gridCollector.ToElements())
                    refArray.Append(new Reference((Element)(object)grid));
                dimOrigin = GeomUtil.AddXYZ(info.UpTop.Origin, Feet300mm * doc.ActiveView.UpDirection);
                dimLine = Line.CreateUnbound(dimOrigin, doc.ActiveView.RightDirection);
                refArray.Append(((GeometryObject)(object)info.UpVertical != (GeometryObject)null) ? ((Face)info_symbol.UpVertical).Reference : null);
                refArray.Append(((GeometryObject)(object)info.DownVertical != (GeometryObject)null) ? ((Face)info_symbol.DownVertical).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null) ? ((Face)info.BeamUp_Left).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null) ? ((Face)info.BeamUp_Right).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null) ? ((Face)info.BeamDown_Left).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null) ? ((Face)info.BeamDown_Right).Reference : null);
                refArray.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
                refArray.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
                break;

            case "Down":
                foreach (Element grid in gridCollector.ToElements())
                    refArray.Append(new Reference(grid));
                dimOrigin = ((GeometryObject)(object)info.BeamDown_Bot != (GeometryObject)null)
                    ? GeomUtil.AddXYZ(info.BeamDown_Bot.Origin, -Feet250mm * doc.ActiveView.UpDirection)
                    : GeomUtil.AddXYZ(info.DownBot.Origin, -Feet250mm * doc.ActiveView.UpDirection);
                dimLine = Line.CreateUnbound(dimOrigin, doc.ActiveView.RightDirection);
                refArray.Append(((GeometryObject)(object)info.UpVertical != (GeometryObject)null) ? ((Face)info_symbol.UpVertical).Reference : null);
                refArray.Append(((GeometryObject)(object)info.DownVertical != (GeometryObject)null) ? ((Face)info_symbol.DownVertical).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null) ? ((Face)info.BeamUp_Left).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null) ? ((Face)info.BeamUp_Right).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null) ? ((Face)info.BeamDown_Left).Reference : null);
                refArray.Append(((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null) ? ((Face)info.BeamDown_Right).Reference : null);
                refArray.Append(((GeometryObject)(object)info.DeckBot_EdgeBot != (GeometryObject)null) ? info_symbol.DeckBot_EdgeBot.Reference : null);
                refArray.Append(((GeometryObject)(object)info.DeckBot_EdgeTop != (GeometryObject)null) ? info_symbol.DeckBot_EdgeTop.Reference : null);
                break;

            case "Right":
                BuildSideDimRefs(doc, info, info_symbol, refArray, isRight: true, out dimOrigin, out resultPoint);
                dimLine = Line.CreateUnbound(dimOrigin, doc.ActiveView.UpDirection);
                break;

            case "Left":
                BuildSideDimRefs(doc, info, info_symbol, refArray, isRight: false, out dimOrigin, out resultPoint);
                dimLine = Line.CreateUnbound(dimOrigin, doc.ActiveView.UpDirection);
                break;

            case "Center":
                dimOrigin = GeomUtil.Middle2Point(info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0), info.DeckTop_EdgeTop.AsCurve().GetEndPoint(0));
                XYZ nudge = new XYZ(0.167, 0.167, 0.167);
                dimOrigin = GeomUtil.AddXYZ(dimOrigin, nudge);
                dimLine = Line.CreateUnbound(dimOrigin, info.DeckTop.FaceNormal);
                refArray.Append(((GeometryObject)(object)info.DeckTop != (GeometryObject)null) ? ((Face)info_symbol.DeckTop).Reference : null);
                refArray.Append(((GeometryObject)(object)info.DeckBot != (GeometryObject)null) ? ((Face)info_symbol.DeckBot).Reference : null);
                break;
        }

        try
        {
            Dimension dim = ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, dimLine, refArray);
            doc.Regenerate();

            if (location == "Center")
            {
                double val = dim.Value.HasValue ? Convert.ToDouble(dim.Value) : Feet150mm;
                dim.TextPosition = dim.TextPosition.Add(GeomUtil.MultiplyVector(info.DeckBot.FaceNormal, val));
            }

            foreach (DimensionSegment seg in dim.Segments)
            {
                if (seg.ValueString == "0")
                {
                    int utf = int.Parse("200E", NumberStyles.HexNumber);
                    seg.ValueOverride = char.ConvertFromUtf32(utf);
                }
            }
        }
        catch (Exception) { }

        return resultPoint;
    }

    /// <summary>
    /// Build reference arrays for Right/Left dimension placement.
    /// Preserves the complex original logic for all stair types.
    /// </summary>
    private void BuildSideDimRefs(Document doc, StairInfo info, StairInfo info_symbol,
        ReferenceArray refArray, bool isRight, out XYZ dimOrigin, out XYZ resultPoint)
    {
        dimOrigin = XYZ.Zero;
        resultPoint = XYZ.Zero;
        double offset = isRight ? Feet150mm : -Feet150mm;

        if ((isRight && info.IsLeftToRight == 1) || (!isRight && info.IsLeftToRight != 1))
        {
            // Upper side
            if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
            {
                PlanarFace vertFace = isRight ? info.BeamUp_Right : info.BeamUp_Left;
                Plane mp = Plane.CreateByNormalAndOrigin(vertFace.FaceNormal, vertFace.Origin);
                resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamUp_Top), mp);
                dimOrigin = GeomUtil.AddXYZ(vertFace.Origin, offset * doc.ActiveView.RightDirection);
                refArray.Append(((Face)info.BeamUp_Top).Reference);
            }
            else
            {
                Plane mp = Plane.CreateByNormalAndOrigin(info.UpVertical.FaceNormal, info.UpVertical.Origin);
                resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.UpTop), mp);
                dimOrigin = GeomUtil.AddXYZ(info.UpVertical.Origin, offset * doc.ActiveView.RightDirection);
            }
            refArray.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
            refArray.Append(((GeometryObject)(object)info.UpBot != (GeometryObject)null) ? ((Face)info_symbol.UpBot).Reference : null);
            refArray.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
            refArray.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
            if (info.Type >= 3)
                refArray.Append(((GeometryObject)(object)info.DeckBot_EdgeBot != (GeometryObject)null) ? info_symbol.DeckBot_EdgeBot.Reference : null);
        }
        else
        {
            // Lower side
            PlanarFace beamFace = isRight ? info.BeamDown_Right : info.BeamDown_Left;
            if ((GeometryObject)(object)beamFace != (GeometryObject)null)
            {
                Plane mp = Plane.CreateByNormalAndOrigin(beamFace.FaceNormal, beamFace.Origin);
                if (info.Type == 2 || info.Type == 3)
                    resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp);
                else
                    resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamDown_Top), mp);
                dimOrigin = GeomUtil.AddXYZ(beamFace.Origin, offset * doc.ActiveView.RightDirection);
                refArray.Append(((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null) ? ((Face)info.BeamDown_Top).Reference : null);
            }
            else if (info.Type == 1 || info.Type == 4)
            {
                Plane mp = Plane.CreateByNormalAndOrigin(info.DownVertical.FaceNormal, info.DownVertical.Origin);
                resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownTop), mp);
                dimOrigin = GeomUtil.AddXYZ(info.DownVertical.Origin, offset * doc.ActiveView.RightDirection);
            }
            else if (info.Type == 3)
            {
                Plane mp = Plane.CreateByNormalAndOrigin(doc.ActiveView.RightDirection, info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0));
                resultPoint = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp);
                dimOrigin = GeomUtil.AddXYZ(resultPoint, offset * doc.ActiveView.RightDirection);
            }
            else
            {
                resultPoint = info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0);
                dimOrigin = GeomUtil.AddXYZ(resultPoint, offset * doc.ActiveView.RightDirection);
            }

            refArray.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
            refArray.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
            refArray.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
            refArray.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
            if (info.Type >= 3)
                refArray.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
        }
    }

    #endregion

    #region Tagging

    private IndependentTag DatTagRebar(Document doc, Rebar rb, XYZ tag_center, StairInfo info, string[] typetag)
    {
        double offsetX = 600.0;
        double offsetY = -200.0;
        int direction = info.IsLeftToRight;
        try
        {
            offsetX = double.Parse(typetag[1]);
            offsetY = double.Parse(typetag[2]);
            direction = 1;
        }
        catch (Exception) { }

        IndependentTag tag = null;
        if (typetag[0] == "GC")
        {
            XYZ headPos = GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.UpDirection, offsetY / MmToFeet * (double)direction));
            tag = IndependentTag.Create(doc, info.Symbol_GC, ((Element)doc.ActiveView).Id,
                ((Element)rb).GetSubelements().FirstOrDefault().GetReference(), true, TagOrientation.Horizontal, headPos);
            tag.LeaderEndCondition = LeaderEndCondition.Free;
            tag.LeaderEnd(tag_center);
            tag.TagHeadPosition = headPos + GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, offsetX / MmToFeet);
        }
        if (typetag[0] == "Main")
        {
            tag_center = GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.UpDirection, offsetY / MmToFeet * (double)direction));
            tag = IndependentTag.Create(doc, info.Symbol_Main, ((Element)doc.ActiveView).Id,
                ((Element)rb).GetSubelements().FirstOrDefault().GetReference(), true, TagOrientation.Horizontal, tag_center);
            tag.TagHeadPosition = tag_center + GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, offsetX / MmToFeet);
            tag.LeaderElbow(GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (offsetX - 400.0) / MmToFeet)));
        }
        return tag;
    }

    #endregion

    #region Landing Rebar Helpers

    private XYZ FindLandingBreakPoint(Document doc, StairInfo info, ReferenceIntersector refIntersector,
        XYZ origin, XYZ last, double proximity, out Reference start_ref, out Reference end_ref)
    {
        Rebar tempRb = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepGiaCuong,
            null, null, info.Host, GeomUtil.SubXYZ(last, origin),
            (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(origin, GeomUtil.AddXYZ(origin, doc.ActiveView.ViewDirection)) },
            (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        tempRb.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(origin, last), true, true, true);
        doc.Regenerate();

        for (int i = 1; i < tempRb.NumberOfBarPositions; i++)
        {
            XYZ pt = tempRb.GetShapeDrivenAccessor().GetBarPositionTransform(i).OfPoint(origin);
            Reference sr, er;
            if (Math.Abs(FindLandingReference(doc, pt, refIntersector, info.Host, out sr, out er) - proximity) > 1E-05)
            {
                doc.Delete(((Element)tempRb).Id);
                start_ref = sr;
                end_ref = er;
                return pt;
            }
        }

        doc.Delete(((Element)tempRb).Id);
        start_ref = null;
        end_ref = null;
        return null;
    }

    private List<Rebar> CreateRebarLanding(Document doc, StairInfo info, Reference start_ref, Reference end_ref,
        XYZ origin, XYZ last, bool includedTag, bool top)
    {
        List<Rebar> result = new List<Rebar>();

        GeometryObject geom1 = doc.GetElement(start_ref).GetGeometryObjectFromReference(start_ref);
        PlanarFace face1 = (PlanarFace)(object)((geom1 is PlanarFace) ? geom1 : null);
        GeometryObject geom2 = doc.GetElement(end_ref).GetGeometryObjectFromReference(end_ref);
        PlanarFace face2 = (PlanarFace)(object)((geom2 is PlanarFace) ? geom2 : null);

        Plane plane1 = CreatePlane(face1, 0.0 - info.Cover);
        Plane plane2 = CreatePlane(face2, 0.0 - info.Cover);
        XYZ projStart = TinhToan.ProjectOnPlane(origin, plane1);
        XYZ projEnd = TinhToan.ProjectOnPlane(origin, plane2);

        Plane topPlane = top
            ? CreatePlane(info.UpTop, 0.0 - info.Cover - info.ThepChu.GetBarDiameter() - info.ThepGiaCuong.GetBarDiameter() * 0.5)
            : CreatePlane(info.DownTop, 0.0 - info.Cover - info.ThepChu.GetBarDiameter() - info.ThepGiaCuong.GetBarDiameter() * 0.5);

        XYZ offset = GeomUtil.SubXYZ(TinhToan.ProjectOnPlane(origin, topPlane), origin);

        Rebar rb1 = Rebar.CreateFromCurves(doc, RebarStyle.Standard, info.ThepGiaCuong,
            null, null, info.Host, GeomUtil.SubXYZ(last, origin),
            (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(projStart, projEnd) },
            (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
        ((Element)rb1).LookupParameter("Partition").Set(info.Partition);
        rb1.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(origin, last), true, true, true);

        Element copied = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)rb1).Id, offset).First());
        Rebar rb2 = (Rebar)(object)((copied is Rebar) ? copied : null);

        result.Add(rb2);
        result.Add(rb1);

        if (includedTag)
        {
            XYZ tagPt = origin;
            Transform transform = rb2.GetShapeDrivenAccessor().GetBarPositionTransform(1);

            if (info.IsLeftToRight == 1)
            {
                tagPt = last;
                transform = transform.Inverse;
            }
            if (!top)
            {
                tagPt = last;
                transform = rb2.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse;
                if (info.IsLeftToRight == 1)
                {
                    tagPt = origin;
                    transform = transform.Inverse;
                }
            }

            XYZ tagPtOffset = GeomUtil.AddXYZ(tagPt, offset);

            IndependentTag tag1 = DatTagRebar(doc, rb2, tagPtOffset, info, new string[] { "GC", "600", "220" });
            tag1.LeaderElbow(new XYZ(tag1.LeaderEnd().X, tag1.LeaderEnd().Y, tag1.TagHeadPosition.Z));

            IndependentTag tag2 = DatTagRebar(doc, rb2, tagPtOffset, info, new string[] { "GC", "600", "220" });
            tag2.LeaderEnd(transform.OfPoint(tagPtOffset));
            tag2.LeaderElbow(new XYZ(tag2.LeaderEnd().X, tag2.LeaderEnd().Y, tag2.TagHeadPosition.Z));

            XYZ tagPt2 = tagPt;
            IndependentTag tag3 = DatTagRebar(doc, rb1, tagPt2, info, new string[] { "GC", "600", "-270" });
            tag3.LeaderElbow(new XYZ(tag3.LeaderEnd().X, tag3.LeaderEnd().Y, tag3.TagHeadPosition.Z));

            IndependentTag tag4 = DatTagRebar(doc, rb1, tagPt2, info, new string[] { "GC", "600", "-270" });
            tag4.LeaderEnd(transform.OfPoint(tagPt2));
            tag4.LeaderElbow(new XYZ(tag4.LeaderEnd().X, tag4.LeaderEnd().Y, tag4.TagHeadPosition.Z));
        }

        return result;
    }

    private double FindLandingReference(Document doc, XYZ point, ReferenceIntersector refIntersector, Element host,
        out Reference start_ref, out Reference end_ref)
    {
        IList<ReferenceWithContext> forwardRefs = (from e in refIntersector.Find(point, doc.ActiveView.ViewDirection)
            orderby e.Proximity select e).ToList();

        double forwardProx = 0.0;
        if (forwardRefs.Count >= 3)
        {
            start_ref = forwardRefs[2].GetReference();
            forwardProx = forwardRefs[2].Proximity;
            Element el = doc.GetElement(start_ref);
            FamilyInstance fi = (FamilyInstance)(object)((el is FamilyInstance) ? el : null);
            bool gapExists = Math.Abs(forwardRefs[0].Proximity - forwardRefs[1].Proximity) > 1E-05;
            if (fi == null || !JoinGeometryUtils.AreElementsJoined(doc, (Element)(object)fi, host) || gapExists)
            {
                start_ref = forwardRefs[0].GetReference();
                forwardProx = forwardRefs[0].Proximity;
            }
        }
        else
        {
            start_ref = forwardRefs[0].GetReference();
            forwardProx = forwardRefs[0].Proximity;
        }

        IList<ReferenceWithContext> backwardRefs = (from e in refIntersector.Find(point, doc.ActiveView.ViewDirection.Negate())
            orderby e.Proximity select e).ToList();

        double backwardProx = 0.0;
        if (backwardRefs.Count >= 3)
        {
            end_ref = backwardRefs[2].GetReference();
            backwardProx = backwardRefs[2].Proximity;
            Element el = doc.GetElement(end_ref);
            FamilyInstance fi = (FamilyInstance)(object)((el is FamilyInstance) ? el : null);
            bool gapExists = Math.Abs(backwardRefs[0].Proximity - backwardRefs[1].Proximity) > 1E-05;
            if (fi == null || !JoinGeometryUtils.AreElementsJoined(doc, (Element)(object)fi, host) || gapExists)
            {
                end_ref = backwardRefs[0].GetReference();
                backwardProx = backwardRefs[0].Proximity;
            }
        }
        else
        {
            end_ref = backwardRefs[0].GetReference();
            backwardProx = backwardRefs[0].Proximity;
        }

        return forwardProx + backwardProx;
    }

    #endregion

    #region Geometry Analysis

    private Edge FindOriginGeometry(Solid origin_solid, Edge edge)
    {
        foreach (Edge e in origin_solid.Edges)
        {
            Curve c = e.AsCurve();
            Line line = (Line)(object)((c is Line) ? c : null);
            Curve c2 = edge.AsCurve();
            Line edgeLine = (Line)(object)((c2 is Line) ? c2 : null);
            if (TinhToan.DiemThuocDuongThang(((Curve)edgeLine).GetEndPoint(0), line)
                && TinhToan.DiemThuocDuongThang(((Curve)edgeLine).GetEndPoint(1), line))
                return e;
        }
        return edge;
    }

    private PlanarFace FindOriginGeometry(Solid origin_solid, PlanarFace face)
    {
        foreach (PlanarFace f in origin_solid.Faces)
        {
            if (TinhToan.CheckFaceInFace((Face)(object)face, (Face)(object)f))
                return f;
        }
        return face;
    }

    /// <summary>
    /// Analyze stair solid geometry to identify all faces and edges,
    /// determine stair type (1-4), and find beam intersections.
    /// This is the core geometry analysis method — preserved from original with IL comments removed.
    /// </summary>
    private void GetFaceInfo(Document doc, Options geomOption, Solid origin_solid, Solid solid, ref StairInfo info, ReferenceIntersector refIntersector)
    {
        List<PlanarFace> topFaces = new List<PlanarFace>();
        List<PlanarFace> bottomFaces = new List<PlanarFace>();
        List<PlanarFace> verticalFaces = new List<PlanarFace>();
        List<PlanarFace> diagonalFaces = new List<PlanarFace>();
        View activeView = doc.ActiveView;

        foreach (Face face in solid.Faces)
        {
            PlanarFace pf = (PlanarFace)((face is PlanarFace) ? face : null);
            if (!((GeometryObject)(object)pf != (GeometryObject)null)) continue;

            double dotUp = pf.FaceNormal.DotProduct(activeView.UpDirection);
            double dotRight = pf.FaceNormal.DotProduct(activeView.RightDirection);
            double dotView = pf.FaceNormal.DotProduct(activeView.ViewDirection);

            // Skip faces parallel to view direction
            if (Math.Abs(dotView - 1.0) < 1E-05 || Math.Abs(dotView + 1.0) < 1E-05) continue;

            PlanarFace originFace = FindOriginGeometry(origin_solid, pf);

            if (Math.Abs(dotUp - 1.0) < 1E-05)
                topFaces.Add(originFace);
            else if (Math.Abs(dotUp + 1.0) < 1E-05)
                bottomFaces.Add(originFace);
            else if (Math.Abs(dotRight - 1.0) < 1E-05 || Math.Abs(dotRight + 1.0) < 1E-05)
                verticalFaces.Add(originFace);
            else
                diagonalFaces.Add(originFace);
        }

        int totalFaces = topFaces.Count + bottomFaces.Count + verticalFaces.Count + diagonalFaces.Count;

        // Determine stair type based on face count
        if (totalFaces == 11)
            AssignType4Faces(doc, activeView, solid, origin_solid, topFaces, bottomFaces, verticalFaces, diagonalFaces, ref info);
        
        if (totalFaces == 9 && verticalFaces.Count == 3)
            AssignType1Faces(doc, activeView, solid, origin_solid, topFaces, bottomFaces, verticalFaces, diagonalFaces, ref info);

        if ((totalFaces == 7 || totalFaces == 9) && info.Type == -1)
            AssignType2Or3Faces(doc, activeView, solid, origin_solid, topFaces, bottomFaces, verticalFaces, diagonalFaces, ref info);

        // Find start/end faces and rebar direction
        if (refIntersector != null)
        {
            if ((GeometryObject)(object)info.DeckBot != (GeometryObject)null && (GeometryObject)(object)info.DeckTop != (GeometryObject)null)
            {
                XYZ midPoint = ((Face)info.DeckTop).Evaluate((((Face)info.DeckTop).GetBoundingBox().Max + ((Face)info.DeckTop).GetBoundingBox().Min) / 2.0) * 0.5
                    + ((Face)info.DeckBot).Evaluate((((Face)info.DeckBot).GetBoundingBox().Max + ((Face)info.DeckBot).GetBoundingBox().Min) / 2.0) * 0.5;

                GeometryObject startGeom = info.Host.GetGeometryObjectFromReference(refIntersector.FindNearest(midPoint, doc.ActiveView.ViewDirection).GetReference());
                info.StartFace = (PlanarFace)(object)((startGeom is PlanarFace) ? startGeom : null);
                GeometryObject endGeom = info.Host.GetGeometryObjectFromReference(refIntersector.FindNearest(midPoint, doc.ActiveView.ViewDirection.Negate()).GetReference());
                info.EndFace = (PlanarFace)(object)((endGeom is PlanarFace) ? endGeom : null);

                info.Mp_view = Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
                info.Mp_start = CreatePlane(info.StartFace, 0.0 - info.Cover - info.ThepChu.GetBarDiameter() / 2.0);

                XYZ projEnd = TinhToan.ProjectOnPlane(info.EndFace.Origin, info.Mp_start);
                XYZ rebarDir = GeomUtil.SubXYZ(info.EndFace.Origin, projEnd);
                info.Rebar_Direction = rebarDir;
                info.Rebar_range = rebarDir.GetLength() - info.Cover - info.ThepChu.GetBarDiameter() / 2.0;
            }
            else
            {
                info.Type = -1;
            }
        }

        if (info.Type <= 0) return;

        // Find beam faces
        FindBeamFaces(doc, activeView, geomOption, info);
    }

    private void AssignType4Faces(Document doc, View activeView, Solid solid, Solid origin_solid,
        List<PlanarFace> topFaces, List<PlanarFace> bottomFaces, List<PlanarFace> verticalFaces,
        List<PlanarFace> diagonalFaces, ref StairInfo info)
    {
        info.Type = 4;
        if (topFaces.Count != 2 || bottomFaces.Count != 2 || diagonalFaces.Count != 2 || verticalFaces.Count != 5)
        { info.Type = -1; return; }

        // Assign top/bottom faces by Z
        if (topFaces[0].Origin.Z > topFaces[1].Origin.Z)
        { info.UpTop = topFaces[0]; info.DownTop = topFaces[1]; }
        else
        { info.UpTop = topFaces[1]; info.DownTop = topFaces[0]; }

        if (bottomFaces[0].Origin.Z > bottomFaces[1].Origin.Z)
        { info.UpBot = bottomFaces[0]; info.DownBot = bottomFaces[1]; }
        else
        { info.UpBot = bottomFaces[1]; info.DownBot = bottomFaces[0]; }

        // Assign diagonal faces
        if (diagonalFaces[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
        { info.DeckTop = diagonalFaces[0]; info.DeckBot = diagonalFaces[1]; }
        else
        { info.DeckTop = diagonalFaces[1]; info.DeckBot = diagonalFaces[0]; }

        info.IsLeftToRight = (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0) ? -1 : 1;

        // Sort 5 vertical faces
        Line refLine = Line.CreateUnbound(doc.ActiveView.Origin, doc.ActiveView.RightDirection);
        List<XYZ> projPts = verticalFaces.Select(f => ((Curve)refLine).Project(f.Origin).XYZPoint).ToList();
        List<XYZ> sorted = TinhToan.OrderPointOnLine(projPts, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)info.IsLeftToRight), new XYZEqual());

        for (int i = 0; i < projPts.Count; i++)
        {
            if (GeomUtil.KhoangCach(projPts[i], sorted[3]) < 1E-05) info.Riser = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[1]) < 1E-05) info.WallTop = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[2]) < 1E-05) info.WallBot = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[0]) < 1E-05) info.DownVertical = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[4]) < 1E-05) info.UpVertical = verticalFaces[i];
        }

        FindEdges(doc, solid, origin_solid, info,
            CreatePlane(info.UpBot, 0.0), CreatePlane(info.Riser, 0.0),
            CreatePlane(info.DeckTop, 0.0), CreatePlane(info.DeckBot, 0.0),
            CreatePlane(info.WallTop, 0.0), CreatePlane(info.WallBot, 0.0));
    }

    private void AssignType1Faces(Document doc, View activeView, Solid solid, Solid origin_solid,
        List<PlanarFace> topFaces, List<PlanarFace> bottomFaces, List<PlanarFace> verticalFaces,
        List<PlanarFace> diagonalFaces, ref StairInfo info)
    {
        info.Type = 1;
        if (topFaces.Count != 2 || bottomFaces.Count != 2 || diagonalFaces.Count != 2)
        { info.Type = -1; return; }

        if (topFaces[0].Origin.Z > topFaces[1].Origin.Z)
        { info.UpTop = topFaces[0]; info.DownTop = topFaces[1]; }
        else
        { info.UpTop = topFaces[1]; info.DownTop = topFaces[0]; }

        if (bottomFaces[0].Origin.Z > bottomFaces[1].Origin.Z)
        { info.UpBot = bottomFaces[0]; info.DownBot = bottomFaces[1]; }
        else
        { info.UpBot = bottomFaces[1]; info.DownBot = bottomFaces[0]; }

        if (diagonalFaces[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
        { info.DeckTop = diagonalFaces[0]; info.DeckBot = diagonalFaces[1]; }
        else
        { info.DeckTop = diagonalFaces[1]; info.DeckBot = diagonalFaces[0]; }

        info.IsLeftToRight = (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0) ? -1 : 1;

        // Sort 3 vertical faces
        Line refLine = Line.CreateUnbound(doc.ActiveView.Origin, doc.ActiveView.RightDirection);
        List<XYZ> projPts = verticalFaces.Select(f => ((Curve)refLine).Project(f.Origin).XYZPoint).ToList();
        List<XYZ> sorted = TinhToan.OrderPointOnLine(projPts, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)info.IsLeftToRight), new XYZEqual());

        for (int i = 0; i < projPts.Count; i++)
        {
            if (GeomUtil.KhoangCach(projPts[i], sorted[1]) < 1E-05) info.Riser = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[0]) < 1E-05) info.DownVertical = verticalFaces[i];
            else if (GeomUtil.KhoangCach(projPts[i], sorted[2]) < 1E-05) info.UpVertical = verticalFaces[i];
        }

        FindEdges(doc, solid, origin_solid, info,
            CreatePlane(info.UpBot, 0.0), CreatePlane(info.Riser, 0.0),
            CreatePlane(info.DeckTop, 0.0), CreatePlane(info.DeckBot, 0.0),
            CreatePlane(info.DownTop, 0.0), CreatePlane(info.DownBot, 0.0));
    }

    private void AssignType2Or3Faces(Document doc, View activeView, Solid solid, Solid origin_solid,
        List<PlanarFace> topFaces, List<PlanarFace> bottomFaces, List<PlanarFace> verticalFaces,
        List<PlanarFace> diagonalFaces, ref StairInfo info)
    {
        info.Type = 2;
        if (topFaces.Count != 1 || bottomFaces.Count != 2 || diagonalFaces.Count != 2)
        { info.Type = -1; return; }

        info.UpTop = topFaces[0];

        if (bottomFaces[0].Origin.Z > bottomFaces[1].Origin.Z)
        { info.UpBot = bottomFaces[0]; info.DownBot = bottomFaces[1]; }
        else
        { info.UpBot = bottomFaces[1]; info.DownBot = bottomFaces[0]; }

        if (diagonalFaces[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
        { info.DeckTop = diagonalFaces[0]; info.DeckBot = diagonalFaces[1]; }
        else
        { info.DeckTop = diagonalFaces[1]; info.DeckBot = diagonalFaces[0]; }

        info.IsLeftToRight = (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0) ? -1 : 1;

        if (verticalFaces.Count == 2)
        {
            foreach (PlanarFace vf in verticalFaces)
            {
                if (vf.FaceNormal.DotProduct(info.DeckTop.FaceNormal) > 0.0)
                    info.Riser = vf;
                else
                    info.UpVertical = vf;
            }
        }
        else if (verticalFaces.Count == 4)
        {
            info.Type = 3;
            List<PlanarFace> sameDir = new List<PlanarFace>();
            List<PlanarFace> oppDir = new List<PlanarFace>();
            foreach (PlanarFace vf in verticalFaces)
            {
                if (vf.FaceNormal.DotProduct(info.DeckTop.FaceNormal) > 0.0) sameDir.Add(vf);
                else oppDir.Add(vf);
            }

            XYZ c0 = ((Face)sameDir[0]).Evaluate((((Face)sameDir[0]).GetBoundingBox().Max + ((Face)sameDir[0]).GetBoundingBox().Min) / 2.0);
            XYZ c1 = ((Face)sameDir[1]).Evaluate((((Face)sameDir[1]).GetBoundingBox().Max + ((Face)sameDir[1]).GetBoundingBox().Min) / 2.0);
            if (c0.Z > c1.Z) { info.Riser = sameDir[0]; info.WallTop = sameDir[1]; }
            else { info.Riser = sameDir[1]; info.WallTop = sameDir[0]; }

            XYZ c2 = ((Face)oppDir[0]).Evaluate((((Face)oppDir[0]).GetBoundingBox().Max + ((Face)oppDir[0]).GetBoundingBox().Min) / 2.0);
            XYZ c3 = ((Face)oppDir[1]).Evaluate((((Face)oppDir[1]).GetBoundingBox().Max + ((Face)oppDir[1]).GetBoundingBox().Min) / 2.0);
            if (c2.Z > c3.Z) { info.UpVertical = oppDir[0]; info.WallBot = oppDir[1]; }
            else { info.UpVertical = oppDir[1]; info.WallBot = oppDir[0]; }
        }
        else
        {
            info.Type = -1;
            return;
        }

        Plane botRefPlane = CreatePlane(info.DownBot, 0.0);
        Plane topRefPlane = botRefPlane;
        if (info.Type == 3)
        {
            topRefPlane = CreatePlane(info.WallTop, 0.0);
            botRefPlane = CreatePlane(info.WallBot, 0.0);
        }

        FindEdges(doc, solid, origin_solid, info,
            CreatePlane(info.UpBot, 0.0), CreatePlane(info.Riser, 0.0),
            CreatePlane(info.DeckTop, 0.0), CreatePlane(info.DeckBot, 0.0),
            topRefPlane, botRefPlane);
    }

    private void FindEdges(Document doc, Solid solid, Solid origin_solid, StairInfo info,
        Plane upBotPlane, Plane riserPlane, Plane deckTopPlane, Plane deckBotPlane,
        Plane topRefPlane, Plane botRefPlane)
    {
        foreach (Edge edge in solid.Edges)
        {
            Curve c = edge.AsCurve();
            Line line = (Line)(object)((c is Line) ? c : null);
            if ((GeometryObject)(object)line == (GeometryObject)null) continue;
            if (!GeomUtil.IsSameDirection(line.Direction, doc.ActiveView.ViewDirection)
                && !GeomUtil.IsOppositeDirection(line.Direction, doc.ActiveView.ViewDirection)) continue;

            XYZ pt = ((Curve)line).GetEndPoint(0);
            if (RbTinhToan.PointInPlane(deckTopPlane, pt) && RbTinhToan.PointInPlane(riserPlane, pt))
                info.DeckTop_EdgeTop = FindOriginGeometry(origin_solid, edge);
            else if (RbTinhToan.PointInPlane(deckTopPlane, pt) && RbTinhToan.PointInPlane(topRefPlane, pt))
                info.DeckTop_EdgeBot = FindOriginGeometry(origin_solid, edge);
            else if (RbTinhToan.PointInPlane(deckBotPlane, pt) && RbTinhToan.PointInPlane(upBotPlane, pt))
                info.DeckBot_EdgeTop = FindOriginGeometry(origin_solid, edge);
            else if (RbTinhToan.PointInPlane(deckBotPlane, pt) && RbTinhToan.PointInPlane(botRefPlane, pt))
                info.DeckBot_EdgeBot = FindOriginGeometry(origin_solid, edge);
        }
    }

    private void FindBeamFaces(Document doc, View activeView, Options geomOption, StairInfo info)
    {
        FilteredElementCollector beamCollector = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id);
        beamCollector.WhereElementIsNotElementType().OfCategory(BuiltInCategory.OST_StructuralFraming);
        IList<Element> beams = beamCollector.ToElements();

        List<Solid> beamSolids = new List<Solid>();
        List<Solid> beamOriginSolids = new List<Solid>();
        Solid cropSolid = TinhToan.CreateSolidFromBoundingBox(doc.ActiveView.CropBox);

        foreach (Element beam in beams)
        {
            Solid boolResult = null, beamSolid = null;
            try
            {
                beamSolid = TinhToan.GetSolidByGeometry(beam.get_Geometry(geomOption), instance: false)[0];
                boolResult = BooleanOperationsUtils.ExecuteBooleanOperation(cropSolid, beamSolid, BooleanOperationsType.Intersect);
            }
            catch (Exception) { }

            if ((GeometryObject)(object)boolResult != (GeometryObject)null && boolResult.Volume != 0.0)
            {
                beamSolids.Add(boolResult);
                beamOriginSolids.Add(beamSolid);
            }
        }

        if (beamSolids.Count == 2)
        {
            Solid upperSolid, upperOrigin, lowerSolid, lowerOrigin;
            if (beamSolids[0].ComputeCentroid().Z < beamSolids[1].ComputeCentroid().Z)
            {
                upperSolid = beamSolids[1]; upperOrigin = beamOriginSolids[1];
                lowerSolid = beamSolids[0]; lowerOrigin = beamOriginSolids[0];
            }
            else
            {
                upperSolid = beamSolids[0]; upperOrigin = beamOriginSolids[0];
                lowerSolid = beamSolids[1]; lowerOrigin = beamOriginSolids[1];
            }

            AssignBeamFaces(activeView, upperSolid, upperOrigin, info, isUpper: true);
            AssignBeamFaces(activeView, lowerSolid, lowerOrigin, info, isUpper: false);
        }
        else if (beamSolids.Count == 1)
        {
            AssignBeamFaces(activeView, beamSolids[0], beamOriginSolids[0], info, isUpper: true);
            // Copy to lower
            info.BeamDown_Top = info.BeamUp_Top;
            info.BeamDown_Bot = info.BeamUp_Bot;
            info.BeamDown_Left = info.BeamUp_Left;
            info.BeamDown_Right = info.BeamUp_Right;
        }

        // Validate beam positions
        ValidateBeamPositions(doc, info);
    }

    private void AssignBeamFaces(View activeView, Solid solid, Solid originSolid, StairInfo info, bool isUpper)
    {
        foreach (Face face in solid.Faces)
        {
            PlanarFace pf = FindOriginGeometry(originSolid, (PlanarFace)(object)((face is PlanarFace) ? face : null));
            if (!((GeometryObject)(object)pf != (GeometryObject)null)) continue;

            double dotView = pf.FaceNormal.DotProduct(activeView.ViewDirection);
            if (Math.Abs(dotView - 1.0) < 1E-05 || Math.Abs(dotView + 1.0) < 1E-05) continue;

            double dotUp = pf.FaceNormal.DotProduct(activeView.UpDirection);
            double dotRight = pf.FaceNormal.DotProduct(activeView.RightDirection);

            if (isUpper)
            {
                if (Math.Abs(dotUp - 1.0) < 1E-05) info.BeamUp_Top = pf;
                else if (Math.Abs(dotUp + 1.0) < 1E-05) info.BeamUp_Bot = pf;
                else if (Math.Abs(dotRight - 1.0) < 1E-05) info.BeamUp_Right = pf;
                else if (Math.Abs(dotRight + 1.0) < 1E-05) info.BeamUp_Left = pf;
            }
            else
            {
                if (Math.Abs(dotUp - 1.0) < 1E-05) info.BeamDown_Top = pf;
                else if (Math.Abs(dotUp + 1.0) < 1E-05) info.BeamDown_Bot = pf;
                else if (Math.Abs(dotRight - 1.0) < 1E-05) info.BeamDown_Right = pf;
                else if (Math.Abs(dotRight + 1.0) < 1E-05) info.BeamDown_Left = pf;
            }
        }
    }

    private void ValidateBeamPositions(Document doc, StairInfo info)
    {
        // Validate upper beam
        if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
        {
            PlanarFace checkFace = (info.IsLeftToRight == 1) ? info.BeamUp_Left : info.BeamUp_Right;
            XYZ centerPt = TinhToan.GetCenterOfFace(info.UpTop);
            centerPt = TinhToan.ProjectOnPlane(centerPt, CreatePlane(info.UpVertical, 0.0));
            IntersectionResult ir = ((Face)checkFace).Project(centerPt);
            if (ir == null || GeomUtil.KhoangCach(centerPt, ir.XYZPoint) > 1E-05)
            {
                info.BeamUp_Bot = null; info.BeamUp_Top = null;
                info.BeamUp_Left = null; info.BeamUp_Right = null;
            }
        }

        // Validate lower beam
        if (!((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null)) return;

        if (info.Type == 1 || info.Type == 4)
        {
            PlanarFace checkFace = (info.IsLeftToRight == 1) ? info.BeamDown_Right : info.BeamDown_Left;
            XYZ centerPt = TinhToan.GetCenterOfFace(info.DownTop);
            centerPt = TinhToan.ProjectOnPlane(centerPt, CreatePlane(info.DownVertical, 0.0));
            IntersectionResult ir = ((Face)checkFace).Project(centerPt);
            if (ir == null || GeomUtil.KhoangCach(centerPt, ir.XYZPoint) > 1E-05)
            {
                info.BeamDown_Bot = null; info.BeamDown_Top = null;
                info.BeamDown_Left = null; info.BeamDown_Right = null;
            }
        }

        if (info.Type == 2 || info.Type == 3)
        {
            if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null
                && ((Face)info.BeamDown_Top).Reference.ConvertToStableRepresentation(((Element)info.Host).Document)
                == ((Face)info.BeamUp_Top).Reference.ConvertToStableRepresentation(((Element)info.Host).Document))
            {
                info.BeamDown_Bot = null; info.BeamDown_Top = null;
                info.BeamDown_Left = null; info.BeamDown_Right = null;
            }

            if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null && (GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null)
            {
                Plane mp = Plane.CreateByNormalAndOrigin(info.BeamUp_Left.FaceNormal, info.BeamUp_Left.Origin);
                XYZ proj = TinhToan.ProjectOnPlane(info.BeamDown_Left.Origin, mp);
                if (GeomUtil.KhoangCach(info.BeamDown_Left.Origin, proj) < 1E-05)
                {
                    info.BeamDown_Bot = null; info.BeamDown_Top = null;
                    info.BeamDown_Left = null; info.BeamDown_Right = null;
                }
            }
        }
    }

    #endregion
}
