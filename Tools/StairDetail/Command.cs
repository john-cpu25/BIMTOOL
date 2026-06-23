using HuyAddin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Autodesk.Revit.Attributes;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;



namespace RincoNhan.Tools.StairDetail;

[Transaction(TransactionMode.Manual)]


public class StairDetail : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_0369: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Expected O, but got Unknown
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Expected O, but got Unknown
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Expected O, but got Unknown
		//IL_05dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e3: Expected O, but got Unknown
		//IL_05e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0350: Expected O, but got Unknown
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b02: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b66: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b75: Expected O, but got Unknown
		//IL_0b70: Unknown result type (might be due to invalid IL or missing references)
		if (true)
		{
			Guid guid = new Guid("a4fc0b15-1982-498a-b24a-c5e09fcd6b7b");
			Guid gUID = commandData.Application.ActiveAddInId.GetGUID();
			UIApplication application = commandData.Application;
			UIDocument activeUIDocument = application.ActiveUIDocument;
			Document document = commandData.Application.ActiveUIDocument.Document;
			StairInfo info = new StairInfo();
			_ = DateTime.Now;
			info.ChieuDaiToiThieu = 6.561679790026248;
			bool flag = true;
			bool flag2 = false;
			List<Element> list = ((IEnumerable<Element>)new FilteredElementCollector(document).OfCategory((BuiltInCategory)(-2009000)).OfClass(typeof(RebarBarType)).WhereElementIsElementType()).ToList();
			frm_StairDetail frm_StairDetail2 = new frm_StairDetail(list.OrderBy((Element e) => e.Name).ToList());
			if (frm_StairDetail2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				foreach (Element item in list)
				{
					if (string.Compare(item.Name, frm_StairDetail2.cb_ThepChinh.Text) == 0)
					{
						info.ThepChu = (RebarBarType)(object)((item is RebarBarType) ? item : null);
					}
					if (string.Compare(item.Name, frm_StairDetail2.cb_ThepPhu.Text) == 0)
					{
						info.ThepGiaCuong = (RebarBarType)(object)((item is RebarBarType) ? item : null);
					}
				}
				int result = 0;
				if (int.TryParse(frm_StairDetail2.txtThepChinh_KhoangRai.Text, out result))
				{
					info.ThepChu_KhoangRai = (double)result / 304.79999999999995;
				}
				else
				{
					info.ThepChu_KhoangRai = 0.4921259842519686;
				}
				if (int.TryParse(frm_StairDetail2.txtThepPhu_KhoangRai.Text, out result))
				{
					info.ThepGiaCuong_KhoangRai = (double)result / 304.79999999999995;
				}
				else
				{
					info.ThepGiaCuong_KhoangRai = 0.6561679790026248;
				}
				if (info.ThepChu != null)
				{
					double doanNeo = Math.Round(30.0 * info.ThepChu.BarModelDiameter * 12.0 * 25.4 / 50.0) * 50.0 / 304.79999999999995;
					info.DoanNeo = doanNeo;
					flag = frm_StairDetail2.chb_RebarLandingTop.Checked;
					flag2 = frm_StairDetail2.chb_RebarLandingBot.Checked;
					Element obj = TinhToan.FindElementByName(document, typeof(View3D), "IBIM_OriginView_No-DELETE");
					View3D val = (View3D)(object)((obj is View3D) ? obj : null);
					View activeView = document.ActiveView;
					if (val == null)
					{
						Transaction val2 = new Transaction(document, "Create 3D");
						val2.Start();
						FilteredElementCollector val3 = new FilteredElementCollector(document);
						View3D val4 = Enumerable.First(predicate: (View3D v3) => !((View)v3).IsTemplate, source: ((IEnumerable)val3.OfClass(typeof(View3D))).Cast<View3D>());
						val = View3D.CreateIsometric(document, ((Element)val4).GetTypeId());
						((View)val).DetailLevel = (ViewDetailLevel)3;
						((View)val).AreAnalyticalModelCategoriesHidden = true;
						((View)val).DisplayStyle = (DisplayStyle)2;
						((Element)val).Name = "IBIM_OriginView_No-DELETE";
						val2.Commit();
					}
					application.ActiveUIDocument.ActiveView = (View)(object)val;
					application.ActiveUIDocument.ActiveView = activeView;
					if (gUID == guid)
					{
						try
						{
							TinhToan.DisableUpdaters(new AddInId(guid), "all");
						}
						catch (Exception)
						{
						}
					}
					FilteredElementCollector val5 = new FilteredElementCollector(document, ((Element)document.ActiveView).Id);
					val5.OfCategory((BuiltInCategory)(-2000032)).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();
					IList<Element> list2 = val5.ToElements();
					FamilyInstance val6 = null;
					ReferenceIntersector refIntersector = new ReferenceIntersector((ElementFilter)new ElementCategoryFilter((BuiltInCategory)(-2000032)), (FindReferenceTarget)16, val);
					Options val7 = new Options();
					val7.ComputeReferences = true;
					val7.View = (View)(object)val;
					Solid val8 = TinhToan.CreateSolidFromBoundingBox(activeView.CropBox);
					int num = 0;
					Solid solid = null;
					Solid origin_solid = null;
					Solid solid2 = null;
					Solid origin_solid2 = null;
					foreach (Element item2 in list2)
					{
						List<Solid> solidByGeometry = TinhToan.GetSolidByGeometry(item2.get_Geometry(val7), instance: true);
						List<Solid> solidByGeometry2 = TinhToan.GetSolidByGeometry(item2.get_Geometry(val7), instance: false);
						foreach (Solid item3 in solidByGeometry)
						{
							Solid val9 = null;
							try
							{
								val9 = BooleanOperationsUtils.ExecuteBooleanOperation(item3, val8, (BooleanOperationsType)2);
							}
							catch (Exception)
							{
								continue;
							}
							if ((GeometryObject)(object)val9 != (GeometryObject)null && val9.Volume != 0.0)
							{
								solid = val9;
								num++;
								origin_solid = item3;
								val6 = (FamilyInstance)(object)((item2 is FamilyInstance) ? item2 : null);
							}
						}
						foreach (Solid item4 in solidByGeometry2)
						{
							Solid val10 = null;
							try
							{
								val10 = BooleanOperationsUtils.ExecuteBooleanOperation(item4, val8, (BooleanOperationsType)2);
							}
							catch (Exception)
							{
								continue;
							}
							if ((GeometryObject)(object)val10 != (GeometryObject)null && val10.Volume != 0.0)
							{
								solid2 = val10;
								origin_solid2 = item4;
							}
						}
					}
					if (num != 1)
					{
						System.Windows.Forms.MessageBox.Show("Không Xác Định Được Cấu Kiện Thang !", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Hand);
						return (Result)1;
					}
					info.Host = (Element)(object)val6;
					info.Partition = ((Element)val6).LookupParameter("Mark").AsString();
					Element element = document.GetElement(((Element)val6).LookupParameter("Rebar Cover").AsElementId());
					RebarCoverType val11 = (RebarCoverType)(object)((element is RebarCoverType) ? element : null);
					if (val11 != null)
					{
						info.Cover = val11.CoverDistance;
					}
					else
					{
						info.Cover = 0.06561679790026248;
					}
					GetFaceInfo(document, val7, origin_solid, solid, ref info, refIntersector);
					if (info.Type == -1)
					{
						System.Windows.Forms.MessageBox.Show("Không Phải Loại Cầu Thang Điển Hình !", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Hand);
						return (Result)1;
					}
					StairInfo info2 = info.Clone() as StairInfo;
					GetFaceInfo(document, val7, origin_solid2, solid2, ref info2, null);
					Transaction val12 = new Transaction(document, "Stair Detail");
					val12.Start();
					foreach (Element item5 in (IEnumerable<Element>)((IEnumerable<Element>)new FilteredElementCollector(document).WhereElementIsElementType().OfCategory((BuiltInCategory)(-2009020))).ToList())
					{
						FamilySymbol val13 = (FamilySymbol)(object)((item5 is FamilySymbol) ? item5 : null);
						if (val13 != null && !(((ElementType)val13).FamilyName != "IBIM_Tag_Rebar (Right)"))
						{
							if (((Element)val13).Name == "ĐK@KC_Diagonal")
							{
								info.Symbol_Main = ((Element)val13).Id;
							}
							if (((Element)val13).Name == "ĐK@KC_Open Dot")
							{
								info.Symbol_GC = ((Element)val13).Id;
							}
						}
					}
					if (info.Symbol_Main == (ElementId)null || info.Symbol_GC == (ElementId)null)
					{
						string text = "X:\\00. GENE_MANAGEMENT\\IBIM_Tools\\Family\\Template\\IBIM_Tag_Rebar (Right).rfa";
						Family val14 = default(Family);
						document.LoadFamily(text, (IFamilyLoadOptions)(object)new FamilyLoadOption(), out val14);
						document.Regenerate();
						foreach (ElementId familySymbolId in val14.GetFamilySymbolIds())
						{
							Element element2 = document.GetElement(familySymbolId);
							Element obj2 = ((element2 is FamilySymbol) ? element2 : null);
							if (obj2.Name == "ĐK@KC_Diagonal")
							{
								info.Symbol_Main = familySymbolId;
							}
							if (obj2.Name == "ĐK@KC_Open Dot")
							{
								info.Symbol_GC = familySymbolId;
							}
						}
					}
					if (info.Type > 0)
					{
						PlaceDim(document, info, info2, "Up");
						PlaceDim(document, info, info2, "Down");
						XYZ zero = XYZ.Zero;
						XYZ zero2 = XYZ.Zero;
						if (info.IsLeftToRight == 1)
						{
							zero = PlaceDim(document, info, info2, "Right");
							zero2 = PlaceDim(document, info, info2, "Left");
						}
						else
						{
							zero2 = PlaceDim(document, info, info2, "Right");
							zero = PlaceDim(document, info, info2, "Left");
						}
						PlaceDim(document, info, info2, "Center");
						if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
						{
							SpotDimension obj3 = PlaceSpotElevation(document, info.BeamUp_Top, zero);
							((Dimension)obj3).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)obj3).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(info.IsLeftToRight * 250) / 304.79999999999995));
						}
						else
						{
							SpotDimension val15 = PlaceSpotElevation(document, info2.UpTop, zero);
							if (val15 != null)
							{
								((Dimension)val15).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)val15).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(info.IsLeftToRight * 250) / 304.79999999999995));
							}
						}
						if (info.Type == 1 || info.Type == 4)
						{
							if ((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null)
							{
								SpotDimension obj4 = PlaceSpotElevation(document, info.BeamDown_Top, zero2);
								((Dimension)obj4).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)obj4).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / 304.79999999999995));
							}
							else
							{
								SpotDimension obj5 = PlaceSpotElevation(document, info2.DownTop, zero2);
								((Dimension)obj5).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)obj5).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / 304.79999999999995));
							}
						}
						else if (info.Type == 3)
						{
							SpotDimension obj6 = PlaceSpotElevation(document, info2.DownBot, zero2);
							((Dimension)obj6).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)obj6).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / 304.79999999999995));
						}
						else
						{
							SpotDimension obj7 = document.Create.NewSpotElevation(document.ActiveView, info2.DeckTop_EdgeBot.Reference, zero2, zero2, zero2, zero2, true);
							document.Regenerate();
							((Dimension)obj7).TextPosition = zero2.Add(document.ActiveView.UpDirection * 70.0 / 304.79999999999995);
							((Dimension)obj7).LeaderEndPosition = GeomUtil.AddXYZ(((Dimension)obj7).LeaderEndPosition, GeomUtil.MultiplyVector(document.ActiveView.RightDirection, (double)(-info.IsLeftToRight * 250) / 304.79999999999995));
						}
						if ((GeometryObject)(object)info.BeamDown_Bot != (GeometryObject)null)
						{
							PlaceBeamTag(document, info.BeamDown_Bot);
						}
						if ((GeometryObject)(object)info.BeamUp_Bot != (GeometryObject)null)
						{
							PlaceBeamTag(document, info.BeamUp_Bot);
						}
						ModelThepLandingTop(document, info, refIntersector, flag);
						ModelThepUpDeck(document, info);
						ModelThepGiaCuongDeck(document, info);
						ModelThepDeck(document, info, info2);
						if (info.Type >= 3)
						{
							ModelThepWall(document, info);
						}
						if (info.Type == 1 || info.Type == 4)
						{
							ModelThepLandingBot(document, info, refIntersector, flag2);
							ModelThepDownDeck(document, info);
						}
					}
					val12.Commit();
					foreach (UIView openUIView in activeUIDocument.GetOpenUIViews())
					{
						if (openUIView.ViewId == ((Element)val).Id)
						{
							openUIView.Close();
						}
					}
					if (gUID == guid)
					{
						try
						{
							TinhToan.EnableUpdaters(new AddInId(new Guid("a4fc0b15-1982-498a-b24a-c5e09fcd6b7b")), "all");
						}
						catch (Exception)
						{
						}
					}
					return (Result)0;
				}
				return (Result)1;
			}
			return (Result)1;
		}
		return (Result)1;
	}

	private void ModelThepWall(Document doc, StairInfo info)
	{
		double doanNeo = info.DoanNeo;
		XYZ val = doc.ActiveView.RightDirection.Negate() * (double)info.IsLeftToRight;
		Line obj = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line val2 = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line val3 = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line val4 = null;
		val4 = ((info.Type != 3) ? CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, doanNeo, sameFaceNormal: true));
		if (val3.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
		{
			Curve obj2 = ((Curve)val3).CreateReversed();
			val3 = (Line)(object)((obj2 is Line) ? obj2 : null);
		}
		XYZ val5 = TinhToan.GiaoDiem((Curve)(object)obj, (Curve)(object)val4);
		XYZ val6 = TinhToan.GiaoDiem((Curve)(object)obj, (Curve)(object)val3);
		XYZ val7 = GeomUtil.AddXYZ(val5, GeomUtil.MultiplyVector(val, doanNeo));
		XYZ val8 = GeomUtil.AddXYZ(val6, GeomUtil.MultiplyVector(val3.Direction, doanNeo));
		List<Curve> list = new List<Curve>();
		if (info.Type == 4)
		{
			list.Add((Curve)(object)Line.CreateBound(val7, val5));
		}
		list.Add((Curve)(object)Line.CreateBound(val5, val6));
		list.Add((Curve)(object)Line.CreateBound(val6, val8));
		Rebar val9 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val9).LookupParameter("Partition").Set(info.Partition);
		val9.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		XYZ tag_center = GeomUtil.Middle2Point(val5, val6);
		DatTagRebar(doc, val9, tag_center, info, new string[3] { "Main", "650", "0" });
		XYZ val10 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
		XYZ val11 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val3);
		XYZ val12 = GeomUtil.AddXYZ(val10, GeomUtil.MultiplyVector(val, doanNeo));
		XYZ val13 = GeomUtil.AddXYZ(val11, GeomUtil.MultiplyVector(val3.Direction, doanNeo));
		List<Curve> list2 = new List<Curve>();
		if (info.Type == 4)
		{
			list2.Add((Curve)(object)Line.CreateBound(val12, val10));
		}
		list2.Add((Curve)(object)Line.CreateBound(val10, val11));
		list2.Add((Curve)(object)Line.CreateBound(val11, val13));
		Rebar val14 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list2, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val14).LookupParameter("Partition").Set(info.Partition);
		val14.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		tag_center = GeomUtil.Middle2Point(val5, val6);
		DatTagRebar(doc, val14, tag_center, info, new string[3] { "Main", "650", "250" });
		Line val15 = null;
		val15 = ((info.Type != 3) ? CreateLineByFace(doc, info.DownTop, info.Mp_start, 10, info.Cover + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: true) : CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
		val2 = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line obj3 = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line val16 = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover - info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false);
		XYZ val17 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val15);
		XYZ val18 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val16);
		XYZ val19 = GeomUtil.SubXYZ(TinhToan.GiaoDiem((Curve)(object)obj3, (Curve)(object)val15), val17);
		Plane mp = CreatePlane(info.StartFace, 0.0 - info.Cover);
		Plane mp2 = CreatePlane(info.EndFace, 0.0 - info.Cover);
		XYZ val20 = TinhToan.ProjectOnPlane(val17, mp);
		XYZ val21 = TinhToan.ProjectOnPlane(val17, mp2);
		Rebar val22 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepGiaCuong, (RebarHookType)null, (RebarHookType)null, info.Host, doc.ActiveView.UpDirection, (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(val20, val21) }, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val22).LookupParameter("Partition").Set(info.Partition);
		val22.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(val17, val18), true, true, true);
		Element element = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val22).Id, val19).First());
		Rebar val23 = (Rebar)(object)((element is Rebar) ? element : null);
		tag_center = val23.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(GeomUtil.AddXYZ(val17, val19));
		DatTagRebar(doc, val23, tag_center, info, new string[3] { "GC", "650", "0" });
		IndependentTag obj4 = DatTagRebar(doc, val22, tag_center, info, new string[3] { "GC", "650", "0" });
		obj4.SetLeaderEnd(obj4.GetTaggedReferences().First(), GeomUtil.AddXYZ(tag_center, val19.Negate()));
	}

	private void ModelThepLandingTop(Document doc, StairInfo info, ReferenceIntersector refIntersector, bool incluedLandingTop)
	{
		Line val = CreateLineByFace(doc, info.UpBot, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line obj = CreateLineByFace(doc, info.Riser, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter, sameFaceNormal: false);
		Line val2 = null;
		if (info.IsLeftToRight == 1)
		{
			val2 = ((!((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null)) ? CreateLineByFace(doc, info.UpVertical, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.BeamUp_Left, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
			if (val.Direction.DotProduct(doc.ActiveView.RightDirection) > 0.0)
			{
				Curve obj2 = ((Curve)val).CreateReversed();
				val = (Line)(object)((obj2 is Line) ? obj2 : null);
			}
		}
		else
		{
			val2 = ((!((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null)) ? CreateLineByFace(doc, info.UpVertical, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.BeamUp_Right, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
			if (val.Direction.DotProduct(doc.ActiveView.RightDirection) < 0.0)
			{
				Curve obj3 = ((Curve)val).CreateReversed();
				val = (Line)(object)((obj3 is Line) ? obj3 : null);
			}
		}
		Line obj4 = CreateLineByFace(doc, info.DeckBot, info.Mp_view, 10, info.Cover - info.ThepGiaCuong.BarModelDiameter / 2.0, sameFaceNormal: false);
		XYZ val3 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val);
		XYZ val4 = TinhToan.GiaoDiem((Curve)(object)obj4, (Curve)(object)val);
		XYZ val5 = TinhToan.GiaoDiem((Curve)(object)obj, (Curve)(object)val);
		Reference start_ref = null;
		Reference end_ref = null;
		Reference start_ref2 = null;
		Reference end_ref2 = null;
		double num = FindLandingReference(doc, val5, refIntersector, info.Host, out start_ref2, out end_ref2);
		Reference start_ref3 = null;
		Reference end_ref3 = null;
		double num2 = FindLandingReference(doc, val3, refIntersector, info.Host, out start_ref3, out end_ref3);
		if (Math.Abs(num - num2) < 1E-05 || num > num2)
		{
			if (!incluedLandingTop)
			{
				return;
			}
			CreateRebarLanding(doc, info, start_ref2, end_ref2, val3, val4, includedTag: true, top: true);
			{
				foreach (Rebar item in CreateRebarLanding(doc, info, start_ref2, end_ref2, val4, val5, includedTag: false, top: true))
				{
					item.IncludeFirstBar = false;
				}
				return;
			}
		}
		Reference start_ref4 = null;
		Reference end_ref4 = null;
		double num3 = FindLandingReference(doc, val4, refIntersector, info.Host, out start_ref4, out end_ref4);
		if (Math.Abs(num - num3) < 1E-05)
		{
			foreach (Rebar item2 in CreateRebarLanding(doc, info, start_ref2, end_ref2, val4, val5, includedTag: true, top: true))
			{
				item2.IncludeFirstBar = false;
			}
			XYZ val6 = FindLandingBreakPoint(doc, info, refIntersector, val4, val3, num3, out start_ref, out end_ref);
			if (val6 != null)
			{
				foreach (Rebar item3 in CreateRebarLanding(doc, info, start_ref2, end_ref2, val6, val4, includedTag: false, top: true))
				{
					item3.IncludeFirstBar = false;
				}
				if (incluedLandingTop)
				{
					CreateRebarLanding(doc, info, start_ref, end_ref, val3, val6, includedTag: true, top: true);
				}
			}
			else if (incluedLandingTop)
			{
				CreateRebarLanding(doc, info, start_ref4, end_ref4, val3, val4, includedTag: true, top: true);
			}
			return;
		}
		XYZ val7 = FindLandingBreakPoint(doc, info, refIntersector, val5, val4, num, out start_ref, out end_ref);
		if (val7 != null)
		{
			foreach (Rebar item4 in CreateRebarLanding(doc, info, start_ref2, end_ref2, val7, val5, includedTag: false, top: true))
			{
				item4.IncludeFirstBar = false;
			}
			if (!incluedLandingTop)
			{
				return;
			}
			if (GeomUtil.KhoangCach(val7, val4) > 1E-05)
			{
				foreach (Rebar item5 in CreateRebarLanding(doc, info, start_ref, end_ref, val4, val7, includedTag: false, top: true))
				{
					item5.IncludeFirstBar = false;
				}
			}
			CreateRebarLanding(doc, info, start_ref, end_ref, val3, val4, includedTag: true, top: true);
			return;
		}
		foreach (Rebar item6 in CreateRebarLanding(doc, info, start_ref2, end_ref2, val4, val5, includedTag: false, top: true))
		{
			item6.IncludeFirstBar = false;
		}
		if (incluedLandingTop)
		{
			CreateRebarLanding(doc, info, start_ref4, end_ref4, val3, val4, includedTag: true, top: true);
		}
	}

	private XYZ FindLandingBreakPoint(Document doc, StairInfo info, ReferenceIntersector refIntersector, XYZ origin, XYZ last, double proximity, out Reference start_ref, out Reference end_ref)
	{
		Rebar val = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepGiaCuong, (RebarHookType)null, (RebarHookType)null, info.Host, GeomUtil.SubXYZ(last, origin), (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(origin, GeomUtil.AddXYZ(origin, doc.ActiveView.ViewDirection)) }, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		val.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(origin, last), true, true, true);
		doc.Regenerate();
		for (int i = 1; i < val.NumberOfBarPositions; i++)
		{
			XYZ val2 = val.GetShapeDrivenAccessor().GetBarPositionTransform(i).OfPoint(origin);
			Reference start_ref2 = null;
			Reference end_ref2 = null;
			if (Math.Abs(FindLandingReference(doc, val2, refIntersector, info.Host, out start_ref2, out end_ref2) - proximity) > 1E-05)
			{
				doc.Delete(((Element)val).Id);
				start_ref = start_ref2;
				end_ref = end_ref2;
				return val2;
			}
		}
		doc.Delete(((Element)val).Id);
		start_ref = null;
		end_ref = null;
		return null;
	}

	private List<Rebar> CreateRebarLanding(Document doc, StairInfo info, Reference start_ref, Reference end_ref, XYZ origin, XYZ last, bool includedTag, bool top)
	{
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Expected O, but got Unknown
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Expected O, but got Unknown
		//IL_0315: Unknown result type (might be due to invalid IL or missing references)
		//IL_031f: Expected O, but got Unknown
		//IL_0381: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Expected O, but got Unknown
		List<Rebar> list = new List<Rebar>();
		GeometryObject geometryObjectFromReference = doc.GetElement(start_ref).GetGeometryObjectFromReference(start_ref);
		PlanarFace upTop = (PlanarFace)(object)((geometryObjectFromReference is PlanarFace) ? geometryObjectFromReference : null);
		GeometryObject geometryObjectFromReference2 = doc.GetElement(end_ref).GetGeometryObjectFromReference(end_ref);
		PlanarFace upTop2 = (PlanarFace)(object)((geometryObjectFromReference2 is PlanarFace) ? geometryObjectFromReference2 : null);
		Plane mp = CreatePlane(upTop, 0.0 - info.Cover);
		Plane mp2 = CreatePlane(upTop2, 0.0 - info.Cover);
		XYZ val = TinhToan.ProjectOnPlane(origin, mp);
		XYZ val2 = TinhToan.ProjectOnPlane(origin, mp2);
		Plane val3 = null;
		val3 = ((!top) ? CreatePlane(info.DownTop, 0.0 - info.Cover - info.ThepChu.BarModelDiameter - info.ThepGiaCuong.BarModelDiameter * 0.5) : CreatePlane(info.UpTop, 0.0 - info.Cover - info.ThepChu.BarModelDiameter - info.ThepGiaCuong.BarModelDiameter * 0.5));
		XYZ val4 = GeomUtil.SubXYZ(TinhToan.ProjectOnPlane(origin, val3), origin);
		Rebar val5 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepGiaCuong, (RebarHookType)null, (RebarHookType)null, info.Host, GeomUtil.SubXYZ(last, origin), (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(val, val2) }, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val5).LookupParameter("Partition").Set(info.Partition);
		val5.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(origin, last), true, true, true);
		Element element = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val5).Id, val4).First());
		Rebar val6 = (Rebar)(object)((element is Rebar) ? element : null);
		list.Add(val6);
		list.Add(val5);
		if (includedTag)
		{
			XYZ val7 = origin;
			Transform val8 = val6.GetShapeDrivenAccessor().GetBarPositionTransform(1);
			if (info.IsLeftToRight == 1)
			{
				val7 = last;
				val8 = val8.Inverse;
			}
			if (!top)
			{
				val7 = last;
				val8 = val6.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse;
				if (info.IsLeftToRight == 1)
				{
					val7 = origin;
					val8 = val8.Inverse;
				}
			}
			XYZ val9 = GeomUtil.AddXYZ(val7, val4);
			IndependentTag val10 = DatTagRebar(doc, val6, val9, info, new string[3] { "GC", "600", "220" });
			val10.LeaderElbow(new XYZ(val10.LeaderEnd().X, val10.LeaderEnd().Y, val10.TagHeadPosition.Z));
			val10 = DatTagRebar(doc, val6, val9, info, new string[3] { "GC", "600", "220" });
			val10.LeaderEnd(val8.OfPoint(val9));
			val10.LeaderElbow(new XYZ(val10.LeaderEnd().X, val10.LeaderEnd().Y, val10.TagHeadPosition.Z));
			val9 = val7;
			val10 = DatTagRebar(doc, val5, val9, info, new string[3] { "GC", "600", "-270" });
			val10.LeaderElbow(new XYZ(val10.LeaderEnd().X, val10.LeaderEnd().Y, val10.TagHeadPosition.Z));
			val10 = DatTagRebar(doc, val5, val9, info, new string[3] { "GC", "600", "-270" });
			val10.LeaderEnd(val8.OfPoint(val9));
			val10.LeaderElbow(new XYZ(val10.LeaderEnd().X, val10.LeaderEnd().Y, val10.TagHeadPosition.Z));
		}
		return list;
	}

	private double FindLandingReference(Document doc, XYZ point, ReferenceIntersector refIntersector, Element host, out Reference start_ref, out Reference end_ref)
	{
		IList<ReferenceWithContext> list = (from e in refIntersector.Find(point, doc.ActiveView.ViewDirection)
			orderby e.Proximity
			select e).ToList();
		double num = 0.0;
		if (list.Count >= 3)
		{
			start_ref = list[2].GetReference();
			num = list[2].Proximity;
			Element element = doc.GetElement(start_ref);
			FamilyInstance val = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
			bool flag = Math.Abs(list[0].Proximity - list[1].Proximity) > 1E-05;
			if (val == null || !JoinGeometryUtils.AreElementsJoined(doc, (Element)(object)val, host) || flag)
			{
				start_ref = list[0].GetReference();
				num = list[0].Proximity;
			}
		}
		else
		{
			start_ref = list[0].GetReference();
			num = list[0].Proximity;
		}
		IList<ReferenceWithContext> list2 = (from e in refIntersector.Find(point, doc.ActiveView.ViewDirection.Negate())
			orderby e.Proximity
			select e).ToList();
		double num2 = 0.0;
		if (list2.Count >= 3)
		{
			end_ref = list2[2].GetReference();
			num2 = list2[2].Proximity;
			Element element2 = doc.GetElement(end_ref);
			FamilyInstance val2 = (FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null);
			bool flag2 = Math.Abs(list2[0].Proximity - list2[1].Proximity) > 1E-05;
			if (val2 == null || !JoinGeometryUtils.AreElementsJoined(doc, (Element)(object)val2, host) || flag2)
			{
				end_ref = list2[0].GetReference();
				num2 = list2[0].Proximity;
			}
		}
		else
		{
			end_ref = list2[0].GetReference();
			num2 = list2[0].Proximity;
		}
		return num + num2;
	}

	private void ModelThepLandingBot(Document doc, StairInfo info, ReferenceIntersector refIntersector, bool incluedLandingBot)
	{
		Line val = CreateLineByFace(doc, info.DownBot, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false);
		Line val2 = null;
		if (info.IsLeftToRight == 1)
		{
			val2 = ((!((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)) ? CreateLineByFace(doc, info.DownVertical, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.BeamDown_Right, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
			if (val.Direction.DotProduct(doc.ActiveView.RightDirection) < 0.0)
			{
				Curve obj = ((Curve)val).CreateReversed();
				val = (Line)(object)((obj is Line) ? obj : null);
			}
		}
		else
		{
			val2 = ((!((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null)) ? CreateLineByFace(doc, info.DownVertical, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.BeamDown_Left, info.Mp_view, 10, 0.0 - info.Cover - info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
			if (val.Direction.DotProduct(doc.ActiveView.RightDirection) > 0.0)
			{
				Curve obj2 = ((Curve)val).CreateReversed();
				val = (Line)(object)((obj2 is Line) ? obj2 : null);
			}
		}
		Line val3 = null;
		val3 = ((info.Type != 1) ? CreateLineByFace(doc, info.WallBot, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false) : CreateLineByFace(doc, info.DeckTop, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter * 0.5, sameFaceNormal: false));
		XYZ val4 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val);
		XYZ val5 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val);
		Reference start_ref = null;
		Reference end_ref = null;
		Reference start_ref2 = null;
		Reference end_ref2 = null;
		double num = FindLandingReference(doc, val5, refIntersector, info.Host, out start_ref2, out end_ref2);
		Reference start_ref3 = null;
		Reference end_ref3 = null;
		double num2 = FindLandingReference(doc, val4, refIntersector, info.Host, out start_ref3, out end_ref3);
		if (Math.Abs(num - num2) < 1E-05 || num > num2)
		{
			if (incluedLandingBot)
			{
				CreateRebarLanding(doc, info, start_ref2, end_ref2, val4, val5, includedTag: true, top: false);
			}
			return;
		}
		XYZ val6 = FindLandingBreakPoint(doc, info, refIntersector, val5, val4, num, out start_ref, out end_ref);
		if (val6 != null)
		{
			foreach (Rebar item in CreateRebarLanding(doc, info, start_ref2, end_ref2, val6, val5, includedTag: true, top: false))
			{
				item.IncludeFirstBar = false;
			}
			if (incluedLandingBot)
			{
				CreateRebarLanding(doc, info, start_ref, end_ref, val4, val6, includedTag: true, top: false);
			}
		}
		else if (incluedLandingBot)
		{
			CreateRebarLanding(doc, info, start_ref2, start_ref2, val4, val5, includedTag: true, top: false);
		}
	}

	private void ModelThepUpDeck(Document doc, StairInfo info)
	{
		double num = 0.4921259842519686 - info.DoanNeo;
		double doanNeo = info.DoanNeo;
		Line val = CreateLineByFace(doc, info.Riser, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val2 = CreateLineByFace(doc, info.UpTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val3 = CreateLineByFace(doc, info.UpBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line obj = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover, sameFaceNormal: false);
		Line val4 = null;
		bool flag = true;
		if (info.IsLeftToRight == 1)
		{
			if ((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null)
			{
				val4 = CreateLineByFace(doc, info.BeamUp_Right, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			}
			else
			{
				flag = false;
				val4 = CreateLineByFace(doc, info.UpVertical, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
				XYZ obj2 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
				XYZ val5 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val4);
				num = 0.0 - GeomUtil.KhoangCach(obj2, val5);
			}
		}
		else if ((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null)
		{
			val4 = CreateLineByFace(doc, info.BeamUp_Left, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		}
		else
		{
			flag = false;
			val4 = CreateLineByFace(doc, info.UpVertical, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			XYZ obj3 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
			XYZ val6 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val4);
			num = 0.0 - GeomUtil.KhoangCach(obj3, val6);
		}
		XYZ val7 = TinhToan.GiaoDiem((Curve)(object)obj, (Curve)(object)val);
		XYZ val8 = TinhToan.GiaoDiem((Curve)(object)val, (Curve)(object)val2);
		XYZ val9 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
		XYZ val10 = GeomUtil.AddXYZ(val9, doc.ActiveView.UpDirection * num);
		List<Curve> list = new List<Curve>();
		list.Add((Curve)(object)Line.CreateBound(val7, val8));
		list.Add((Curve)(object)Line.CreateBound(val8, val9));
		list.Add((Curve)(object)Line.CreateBound(val9, val10));
		Rebar val11 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val11).LookupParameter("Partition").Set(info.Partition);
		val11.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		XYZ tag_center = GeomUtil.Middle2Point(val8, val9);
		DatTagRebar(doc, val11, tag_center, info, new string[3] { "Main", "600", "100" });
		Line val12 = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		if (val12.Direction.DotProduct(doc.ActiveView.UpDirection) > 0.0)
		{
			Curve obj4 = ((Curve)val12).CreateReversed();
			val12 = (Line)(object)((obj4 is Line) ? obj4 : null);
		}
		XYZ val13 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val4);
		XYZ val14 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val12);
		if (!flag)
		{
			num = 0.0 - num;
		}
		XYZ val15 = GeomUtil.AddXYZ(val13, doc.ActiveView.UpDirection * num);
		XYZ val16 = GeomUtil.AddXYZ(val14, val12.Direction * doanNeo);
		List<Curve> list2 = new List<Curve>();
		list2.Add((Curve)(object)Line.CreateBound(val15, val13));
		list2.Add((Curve)(object)Line.CreateBound(val13, val14));
		list2.Add((Curve)(object)Line.CreateBound(val14, val16));
		Rebar val17 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list2, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val17).LookupParameter("Partition").Set(info.Partition);
		val17.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		tag_center = GeomUtil.Middle2Point(val13, val14);
		DatTagRebar(doc, val17, tag_center, info, new string[3] { "Main", "400", "-150" });
	}

	private void ModelThepDownDeck(Document doc, StairInfo info)
	{
		double num = 0.4921259842519686 - info.DoanNeo;
		double doanNeo = info.DoanNeo;
		Line val = CreateLineByFace(doc, info.DownTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val2 = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val3 = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		if (info.Type == 4)
		{
			val3 = CreateLineByFace(doc, info.WallBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		}
		if (val3.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
		{
			Curve obj = ((Curve)val3).CreateReversed();
			val3 = (Line)(object)((obj is Line) ? obj : null);
		}
		Line val4 = null;
		bool flag = true;
		if (info.IsLeftToRight == 1)
		{
			if ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null)
			{
				val4 = CreateLineByFace(doc, info.BeamDown_Left, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			}
			else
			{
				flag = false;
				val4 = CreateLineByFace(doc, info.DownVertical, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
				XYZ obj2 = TinhToan.GiaoDiem((Curve)(object)val, (Curve)(object)val4);
				XYZ val5 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
				num = 0.0 - GeomUtil.KhoangCach(obj2, val5);
			}
		}
		else if ((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
		{
			val4 = CreateLineByFace(doc, info.BeamDown_Right, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		}
		else
		{
			flag = false;
			val4 = CreateLineByFace(doc, info.DownVertical, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			XYZ obj3 = TinhToan.GiaoDiem((Curve)(object)val, (Curve)(object)val4);
			XYZ val6 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
			num = 0.0 - GeomUtil.KhoangCach(obj3, val6);
		}
		XYZ val7 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val);
		XYZ val8 = TinhToan.GiaoDiem((Curve)(object)val, (Curve)(object)val4);
		XYZ val9 = GeomUtil.AddXYZ(val7, val3.Direction * doanNeo);
		XYZ val10 = GeomUtil.AddXYZ(val8, doc.ActiveView.UpDirection * num);
		List<Curve> list = new List<Curve>();
		list.Add((Curve)(object)Line.CreateBound(val9, val7));
		list.Add((Curve)(object)Line.CreateBound(val7, val8));
		list.Add((Curve)(object)Line.CreateBound(val8, val10));
		Rebar val11 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val11).LookupParameter("Partition").Set(info.Partition);
		val11.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		XYZ tag_center = 0.5 * val7 + 0.5 * val8;
		DatTagRebar(doc, val11, tag_center, info, new string[3] { "Main", "600", "100" });
		XYZ val12 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
		XYZ val13 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val3);
		if (!flag)
		{
			num = 0.0 - num;
		}
		XYZ val14 = GeomUtil.AddXYZ(val12, doc.ActiveView.UpDirection * num);
		XYZ val15 = GeomUtil.AddXYZ(val13, val3.Direction * doanNeo);
		List<Curve> list2 = new List<Curve>();
		list2.Add((Curve)(object)Line.CreateBound(val14, val12));
		list2.Add((Curve)(object)Line.CreateBound(val12, val13));
		list2.Add((Curve)(object)Line.CreateBound(val13, val15));
		Rebar val16 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list2, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val16).LookupParameter("Partition").Set(info.Partition);
		val16.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		tag_center = 0.5 * val13 + 0.5 * val12;
		DatTagRebar(doc, val16, tag_center, info, new string[3] { "Main", "400", "-150" });
	}

	private void PlaceBeamTag(Document doc, PlanarFace face)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		BoundingBoxUV boundingBox = ((Face)face).GetBoundingBox();
		XYZ val = ((Face)face).Evaluate((boundingBox.Max + boundingBox.Min) / 2.0);
		IndependentTag val2 = IndependentTag.Create(doc, ((Element)doc.ActiveView).Id, new Reference(doc.GetElement(((Face)face).Reference)), true, (TagMode)0, (TagOrientation)0, val);
		doc.Regenerate();
		val2.TagHeadPosition = val + doc.ActiveView.UpDirection.Negate() * 100.0 / 304.79999999999995;
		BoundingBoxXYZ val3 = val2.get_BoundingBox(doc.ActiveView);
		XYZ val4 = val3.Max * 0.5 + val3.Min * 0.5;
		val2.TagHeadPosition = new XYZ(2.0 * val2.TagHeadPosition.X - val4.X, 2.0 * val2.TagHeadPosition.Y - val4.Y, val2.TagHeadPosition.Z);
	}

	private void ModelThepGiaCuongDeck(Document doc, StairInfo info)
	{
		Line val = CreateLineByFace(doc, info.DeckTop, info.Mp_view, 0, 0.0, sameFaceNormal: false);
		double num = Math.Ceiling(((Curve)val).Length * 25.4 * 12.0 / 200.0) * 50.0 / 304.79999999999995;
		Line val2 = CreateLineByFace(doc, info.DeckBot, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter / 2.0, sameFaceNormal: false);
		if (val2.Direction.DotProduct(doc.ActiveView.UpDirection) > 0.0)
		{
			Curve obj = ((Curve)val2).CreateReversed();
			val2 = (Line)(object)((obj is Line) ? obj : null);
		}
		CreateLineByFace(doc, info.UpTop, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter, sameFaceNormal: false);
		Line input_cv = CreateLineByFace(doc, info.Riser, info.Mp_view, 10, info.Cover - info.ThepGiaCuong.BarModelDiameter / 2.0, sameFaceNormal: false);
		Plane mp = CreatePlane(info.DeckTop, 0.0 - info.Cover - info.ThepChu.BarModelDiameter - info.ThepGiaCuong.BarModelDiameter / 2.0);
		XYZ val3 = TinhToan.PlanIntersect(mp, (Curve)(object)input_cv);
		Plane mp2 = CreatePlane(info.StartFace, 0.0 - info.Cover);
		Plane mp3 = CreatePlane(info.EndFace, 0.0 - info.Cover);
		XYZ obj2 = TinhToan.PlanIntersect(info.Mp_view, info.DeckTop_EdgeTop.AsCurve()) + val2.Direction * (num - 2.0 * info.ThepChu.BarModelDiameter);
		XYZ val4 = obj2 - info.DeckTop.FaceNormal * 10.0;
		XYZ val5 = TinhToan.GiaoDiem((Curve)(object)Line.CreateBound(obj2, val4), (Curve)(object)val2);
		XYZ val6 = TinhToan.ProjectOnPlane(val5, mp2);
		XYZ val7 = TinhToan.ProjectOnPlane(val5, mp3);
		XYZ val8 = TinhToan.ProjectOnPlane(val5, mp) - val5;
		info.Thickness = val8.GetLength() + 2.0 * info.Cover + 2.0 * info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter;
		XYZ obj3 = TinhToan.PlanIntersect(info.Mp_view, info.DeckTop_EdgeBot.AsCurve()) - val2.Direction * (num - 2.0 * info.ThepChu.BarModelDiameter);
		XYZ val9 = obj3 - info.DeckTop.FaceNormal * 10.0;
		XYZ val10 = TinhToan.GiaoDiem((Curve)(object)Line.CreateBound(obj3, val9), (Curve)(object)val2);
		Line val11 = null;
		val11 = ((info.Type == 1) ? CreateLineByFace(doc, info.DownTop, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter / 2.0, sameFaceNormal: false) : ((info.Type != 2) ? CreateLineByFace(doc, info.WallTop, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter + info.ThepGiaCuong.BarModelDiameter / 2.0, sameFaceNormal: false) : CreateLineByFace(doc, info.DownBot, info.Mp_view, 10, info.Cover + info.ThepChu.BarModelDiameter, sameFaceNormal: false)));
		XYZ val12 = GeomUtil.AddXYZ(TinhToan.PlanIntersect(mp, (Curve)(object)val11), GeomUtil.MultiplyVector(val8, -1.0));
		val3 = GeomUtil.AddXYZ(val3, GeomUtil.MultiplyVector(val8, -1.0));
		Rebar val13 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepGiaCuong, (RebarHookType)null, (RebarHookType)null, info.Host, val2.Direction, (IList<Curve>)new List<Curve> { (Curve)(object)Line.CreateBound(val6, val7) }, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val13).LookupParameter("Partition").Set(info.Partition);
		val13.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(val3, val5), false, true, true);
		doc.Regenerate();
		val3 = val13.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(val3);
		val13.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(val3, val5), false, true, true);
		Element element = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val13).Id, val8).First());
		Rebar val14 = (Rebar)(object)((element is Rebar) ? element : null);
		val14.IncludeLastBar = false;
		XYZ val15 = GeomUtil.SubXYZ(val10, val5);
		Element element2 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val13).Id, val15).First());
		Rebar val16 = (Rebar)(object)((element2 is Rebar) ? element2 : null);
		val16.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(val5, val10), false, false, false);
		doc.Regenerate();
		if (((Curve)val).Length < info.ChieuDaiToiThieu)
		{
			ElementTransformUtils.CopyElement(doc, ((Element)val16).Id, val8);
		}
		val15 = val12 - val10;
		Element element3 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val16).Id, val15).First());
		Rebar val17 = (Rebar)(object)((element3 is Rebar) ? element3 : null);
		val17.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepGiaCuong_KhoangRai, GeomUtil.KhoangCach(val10, val12), false, true, true);
		Element element4 = doc.GetElement(ElementTransformUtils.CopyElement(doc, ((Element)val17).Id, val8).First());
		Rebar val18 = (Rebar)(object)((element4 is Rebar) ? element4 : null);
		if (info.Type >= 2)
		{
			val17.IncludeFirstBar = false;
		}
		XYZ tag_center = val14.GetShapeDrivenAccessor().GetBarPositionTransform(1).OfPoint(GeomUtil.AddXYZ(val5, val8));
		DatTagRebar(doc, val14, tag_center, info, new string[1] { "GC" });
		DatTagRebar(doc, val14, tag_center, info, new string[1] { "GC" }).LeaderEnd(GeomUtil.AddXYZ(val5, val8));
		tag_center = val16.GetShapeDrivenAccessor().GetBarPositionTransform(val16.Quantity / 2).OfPoint(val10);
		DatTagRebar(doc, val16, tag_center, info, new string[1] { "GC" });
		DatTagRebar(doc, val16, tag_center, info, new string[1] { "GC" }).LeaderEnd(val16.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse.OfPoint(tag_center));
		if (((Curve)val).Length >= info.ChieuDaiToiThieu)
		{
			tag_center = GeomUtil.AddXYZ(val10, val8);
			DatTagRebar(doc, val18, tag_center, info, new string[1] { "GC" });
			DatTagRebar(doc, val18, tag_center, info, new string[1] { "GC" }).LeaderEnd(val18.GetShapeDrivenAccessor().GetBarPositionTransform(1).Inverse.OfPoint(tag_center));
		}
	}

	private void ModelThepDeck(Document doc, StairInfo info, StairInfo info_symbol)
	{
		//IL_0859: Unknown result type (might be due to invalid IL or missing references)
		//IL_0860: Expected O, but got Unknown
		//IL_087d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0884: Expected O, but got Unknown
		//IL_08a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ae: Expected O, but got Unknown
		List<List<Curve>> list = new List<List<Curve>>();
		double doanNeo = info.DoanNeo;
		XYZ rightDirection = doc.ActiveView.RightDirection;
		Line val = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 0, 0.0, sameFaceNormal: false);
		if (val.Direction.DotProduct(doc.ActiveView.UpDirection) < 0.0)
		{
			Curve obj = ((Curve)val).CreateReversed();
			val = (Line)(object)((obj is Line) ? obj : null);
		}
		Line val2 = CreateLineByFace(doc, info.DeckTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val3 = CreateLineByFace(doc, info.DeckBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		Line val4 = null;
		if (info.Type == 1)
		{
			val4 = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			rightDirection = (double)(-info.IsLeftToRight) * doc.ActiveView.RightDirection;
		}
		else if (info.Type == 2)
		{
			GeomUtil.AddXYZ(TinhToan.PlanIntersect(info.Mp_start, info.DeckTop_EdgeBot.AsCurve()), GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)info.IsLeftToRight * info.Cover + info.ThepChu.BarModelDiameter / 2.0));
			if ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null && (GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
			{
				rightDirection = doc.ActiveView.UpDirection.Negate();
				val4 = ((info.IsLeftToRight != 1) ? CreateLineByFace(doc, info.BeamDown_Right, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false) : CreateLineByFace(doc, info.BeamDown_Left, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false));
			}
			else
			{
				double thickness = info.Thickness;
				val4 = CreateLineByFace(doc, info.DownBot, info.Mp_start, 10, thickness - info.Cover - info.ThepChu.BarModelDiameter * 0.5, sameFaceNormal: true);
				rightDirection = doc.ActiveView.RightDirection.Negate() * (double)info.IsLeftToRight;
			}
		}
		else
		{
			val4 = CreateLineByFace(doc, info.WallTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
			rightDirection = doc.ActiveView.UpDirection.Negate();
		}
		Line val5 = CreateLineByFace(doc, info.UpTop, info.Mp_start, 10, info.Cover + info.ThepChu.BarModelDiameter / 2.0, sameFaceNormal: false);
		XYZ val6 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val4);
		XYZ val7 = TinhToan.GiaoDiem((Curve)(object)val2, (Curve)(object)val5);
		XYZ val8 = val6 + rightDirection * doanNeo;
		XYZ val9 = val7 + (double)info.IsLeftToRight * doc.ActiveView.RightDirection * doanNeo;
		if (((Curve)val).Length >= info.ChieuDaiToiThieu)
		{
			double num = Math.Ceiling(((Curve)val).Length * 25.4 * 12.0 / 200.0) * 50.0 / 304.79999999999995;
			List<Curve> list2 = new List<Curve>();
			XYZ val10 = ((Curve)val).GetEndPoint(0) + val.Direction * num;
			XYZ val11 = val10 - GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, 10.0);
			Line obj2 = Line.CreateBound(val10, val11);
			val10 = TinhToan.GiaoDiem((Curve)(object)obj2, (Curve)(object)val2);
			val11 = TinhToan.GiaoDiem((Curve)(object)obj2, (Curve)(object)val3);
			val11 -= GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, info.ThepChu.BarModelDiameter / 2.0);
			list2.Add((Curve)(object)Line.CreateBound(val8, val6));
			list2.Add((Curve)(object)Line.CreateBound(val6, val10));
			list2.Add((Curve)(object)Line.CreateBound(val10, val11));
			list.Add(list2);
			List<Curve> list3 = new List<Curve>();
			XYZ val12 = ((Curve)val).GetEndPoint(1) - val.Direction * num;
			XYZ val13 = val12 - GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, 10.0);
			Line obj3 = Line.CreateBound(val12, val13);
			val12 = TinhToan.GiaoDiem((Curve)(object)obj3, (Curve)(object)val2);
			val13 = TinhToan.GiaoDiem((Curve)(object)obj3, (Curve)(object)val3);
			val13 -= GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, info.ThepChu.BarModelDiameter / 2.0);
			list3.Add((Curve)(object)Line.CreateBound(val13, val12));
			list3.Add((Curve)(object)Line.CreateBound(val12, val7));
			list3.Add((Curve)(object)Line.CreateBound(val7, val9));
			list.Add(list3);
		}
		else
		{
			List<Curve> list4 = new List<Curve>();
			list4.Add((Curve)(object)Line.CreateBound(val8, val6));
			list4.Add((Curve)(object)Line.CreateBound(val6, val7));
			list4.Add((Curve)(object)Line.CreateBound(val7, val9));
			list.Add(list4);
		}
		List<Rebar> list5 = new List<Rebar>();
		XYZ zero = XYZ.Zero;
		foreach (List<Curve> item in list)
		{
			Rebar val14 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)item, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
			((Element)val14).LookupParameter("Partition").Set(info.Partition);
			val14.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
			list5.Add(val14);
			zero = item[1].GetEndPoint(0) * 0.4 + item[1].GetEndPoint(1) * 0.6;
			DatTagRebar(doc, val14, zero, info, new string[1] { "Main" });
		}
		XYZ val15 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val4);
		XYZ val16 = TinhToan.GiaoDiem((Curve)(object)val3, (Curve)(object)val5);
		XYZ val17 = val15 + rightDirection * doanNeo;
		XYZ val18 = val16 + (double)info.IsLeftToRight * doc.ActiveView.RightDirection * doanNeo;
		List<Curve> list6 = new List<Curve>();
		list6.Add((Curve)(object)Line.CreateBound(val17, val15));
		list6.Add((Curve)(object)Line.CreateBound(val15, val16));
		list6.Add((Curve)(object)Line.CreateBound(val16, val18));
		Rebar val19 = Rebar.CreateFromCurves(doc, (RebarStyle)0, info.ThepChu, (RebarHookType)null, (RebarHookType)null, info.Host, info.Rebar_Direction, (IList<Curve>)list6, (RebarHookOrientation)(-1), (RebarHookOrientation)(-1), true, true);
		((Element)val19).LookupParameter("Partition").Set(info.Partition);
		val19.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(info.ThepChu_KhoangRai, info.Rebar_range, true, true, true);
		zero = ((list.Count != 1) ? (val15 * 0.5 + val16 * 0.5) : (val15 * 0.8 + val16 * 0.2));
		DatTagRebar(doc, val19, zero, info, new string[1] { "Main" });
		XYZ zero2 = XYZ.Zero;
		zero2 = ((info.IsLeftToRight != 1) ? GeomUtil.AddXYZ(info.DeckBot.Origin, GeomUtil.MultiplyVector(info.DeckBot.FaceNormal, 0.6561679790026248)) : GeomUtil.AddXYZ(info.DeckTop.Origin, GeomUtil.MultiplyVector(info.DeckTop.FaceNormal, 0.6561679790026248)));
		Line val20 = Line.CreateUnbound(zero2, doc.ActiveView.ViewDirection.CrossProduct(info.DeckTop.FaceNormal));
		doc.Regenerate();
		if (list.Count != 2)
		{
			return;
		}
		Options val21 = new Options();
		val21.ComputeReferences = true;
		val21.View = doc.ActiveView;
		val21.IncludeNonVisibleObjects = true;
		ReferenceArray val22 = new ReferenceArray();
		val22.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
		ReferenceArray val23 = new ReferenceArray();
		val23.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
		val22.Append(GetRebarReference(list5[0], list.First().Last(), val21));
		val23.Append(GetRebarReference(list5[1], list.Last().First().CreateReversed(), val21));
		try
		{
			((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, val20, val22);
		}
		catch (Exception)
		{
			if (val22.get_Item(0) == null)
			{
				System.Windows.Forms.MessageBox.Show("bot_edge");
			}
			if (val22.get_Item(1) == null)
			{
				System.Windows.Forms.MessageBox.Show("bot_rebar");
			}
		}
		try
		{
			((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, val20, val23);
		}
		catch (Exception)
		{
			if (val23.get_Item(0) == null)
			{
				System.Windows.Forms.MessageBox.Show("top_edge");
			}
			if (val23.get_Item(1) == null)
			{
				System.Windows.Forms.MessageBox.Show("top_rebar");
			}
		}
	}

	private Reference GetRebarReference(Rebar rb, Curve direction, Options geomOption)
	{
		foreach (GeometryObject item in rb.get_Geometry(geomOption))
		{
			if (((item is Solid) ? item : null) != (GeometryObject)null)
			{
				continue;
			}
			Line val = (Line)(object)((item is Line) ? item : null);
			if (!((GeometryObject)(object)val != (GeometryObject)null) || ((Curve)val).Reference == null)
			{
				continue;
			}
			Line val2 = (Line)(object)((direction is Line) ? direction : null);
			if (GeomUtil.IsSameDirection(val.Direction, val2.Direction))
			{
				XYZ val3 = ((Curve)val2).GetEndPoint(1) - ((Curve)val).GetEndPoint(1);
				if (val3.IsZeroLength() || GeomUtil.IsOppositeDirection(val3, ((Element)rb).Document.ActiveView.ViewDirection) || GeomUtil.IsSameDirection(val3, ((Element)rb).Document.ActiveView.ViewDirection))
				{
					return ((Curve)val).Reference;
				}
			}
			if (GeomUtil.IsOppositeDirection(val.Direction, val2.Direction))
			{
				XYZ val4 = ((Curve)val2).GetEndPoint(1) - ((Curve)val).GetEndPoint(0);
				if (val4.IsZeroLength() || GeomUtil.IsOppositeDirection(val4, ((Element)rb).Document.ActiveView.ViewDirection) || GeomUtil.IsSameDirection(val4, ((Element)rb).Document.ActiveView.ViewDirection))
				{
					return ((Curve)val).Reference;
				}
			}
		}
		return null;
	}

	private Line CreateLineByFace(Document doc, PlanarFace deckTop, Plane mp, int scale, double offset, bool sameFaceNormal)
	{
		int num = 1;
		if (!sameFaceNormal)
		{
			num = -1;
		}
		XYZEqual xYZEqual = new XYZEqual();
		Line val = TinhToan.ChieuFaceLenPlane(deckTop, mp, xYZEqual);
		XYZ obj = ((Curve)val).GetEndPoint(0) + (double)num * deckTop.FaceNormal * offset - val.Direction * (double)scale;
		XYZ val2 = ((Curve)val).GetEndPoint(1) + (double)num * deckTop.FaceNormal * offset + val.Direction * (double)scale;
		return Line.CreateBound(obj, val2);
	}

	private SpotDimension PlaceSpotElevation(Document doc, PlanarFace face, XYZ center)
	{
		if (center == null || center.GetLength() < 1E-05)
		{
			center = TinhToan.GetCenterOfFace(face);
		}
		try
		{
			SpotDimension obj = doc.Create.NewSpotElevation(doc.ActiveView, ((Face)face).Reference, center, center, center, center, true);
			doc.Regenerate();
			((Dimension)obj).TextPosition = center.Add(doc.ActiveView.UpDirection * 70.0 / 304.79999999999995);
			return obj;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private XYZ PlaceDim(Document doc, StairInfo info, StairInfo info_symbol, string location)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Expected O, but got Unknown
		//IL_0cb7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0cbe: Expected O, but got Unknown
		//IL_0dbe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dc5: Expected O, but got Unknown
		XYZ zero = XYZ.Zero;
		XYZ val = XYZ.Zero;
		Line val2 = null;
		ReferenceArray val3 = new ReferenceArray();
		FilteredElementCollector val4 = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id);
		val4.WhereElementIsNotElementType().OfCategory((BuiltInCategory)(-2000220));
		switch (location)
		{
		case "Up":
			foreach (Grid item in val4.ToElements())
			{
				Grid val6 = item;
				val3.Append(new Reference((Element)(object)val6));
			}
			zero = GeomUtil.AddXYZ(info.UpTop.Origin, 0.9842519685039371 * doc.ActiveView.UpDirection);
			val2 = Line.CreateUnbound(zero, doc.ActiveView.RightDirection);
			val3.Append(((GeometryObject)(object)info.UpVertical != (GeometryObject)null) ? ((Face)info_symbol.UpVertical).Reference : null);
			val3.Append(((GeometryObject)(object)info.DownVertical != (GeometryObject)null) ? ((Face)info_symbol.DownVertical).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null) ? ((Face)info.BeamUp_Left).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null) ? ((Face)info.BeamUp_Right).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null) ? ((Face)info.BeamDown_Left).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null) ? ((Face)info.BeamDown_Right).Reference : null);
			val3.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
			val3.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
			break;
		case "Down":
			foreach (Element item2 in val4.ToElements())
			{
				val3.Append(new Reference(item2));
			}
			zero = ((!((GeometryObject)(object)info.BeamDown_Bot != (GeometryObject)null)) ? GeomUtil.AddXYZ(info.DownBot.Origin, -0.820209973753281 * doc.ActiveView.UpDirection) : GeomUtil.AddXYZ(info.BeamDown_Bot.Origin, -0.820209973753281 * doc.ActiveView.UpDirection));
			val2 = Line.CreateUnbound(zero, doc.ActiveView.RightDirection);
			val3.Append(((GeometryObject)(object)info.UpVertical != (GeometryObject)null) ? ((Face)info_symbol.UpVertical).Reference : null);
			val3.Append(((GeometryObject)(object)info.DownVertical != (GeometryObject)null) ? ((Face)info_symbol.DownVertical).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamUp_Left != (GeometryObject)null) ? ((Face)info.BeamUp_Left).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamUp_Right != (GeometryObject)null) ? ((Face)info.BeamUp_Right).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null) ? ((Face)info.BeamDown_Left).Reference : null);
			val3.Append(((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null) ? ((Face)info.BeamDown_Right).Reference : null);
			val3.Append(((GeometryObject)(object)info.DeckBot_EdgeBot != (GeometryObject)null) ? info_symbol.DeckBot_EdgeBot.Reference : null);
			val3.Append(((GeometryObject)(object)info.DeckBot_EdgeTop != (GeometryObject)null) ? info_symbol.DeckBot_EdgeTop.Reference : null);
			break;
		case "Right":
			if (info.IsLeftToRight == 1)
			{
				if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
				{
					Plane mp = Plane.CreateByNormalAndOrigin(info.BeamUp_Right.FaceNormal, info.BeamUp_Right.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamUp_Top), mp);
					zero = GeomUtil.AddXYZ(info.BeamUp_Right.Origin, 0.4921259842519686 * doc.ActiveView.RightDirection);
					val3.Append(((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null) ? ((Face)info.BeamUp_Top).Reference : null);
				}
				else
				{
					Plane mp2 = Plane.CreateByNormalAndOrigin(info.UpVertical.FaceNormal, info.UpVertical.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.UpTop), mp2);
					zero = GeomUtil.AddXYZ(info.UpVertical.Origin, 0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				val3.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.UpBot != (GeometryObject)null) ? ((Face)info_symbol.UpBot).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
				if (info.Type >= 3)
				{
					val3.Append(((GeometryObject)(object)info.DeckBot_EdgeBot != (GeometryObject)null) ? info_symbol.DeckBot_EdgeBot.Reference : null);
				}
			}
			else
			{
				if ((GeometryObject)(object)info.BeamDown_Right != (GeometryObject)null)
				{
					Plane mp3 = Plane.CreateByNormalAndOrigin(info.BeamDown_Right.FaceNormal, info.BeamDown_Right.Origin);
					val = ((info.Type != 2 && info.Type != 3) ? TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamDown_Top), mp3) : TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp3));
					zero = GeomUtil.AddXYZ(info.BeamDown_Right.Origin, 0.4921259842519686 * doc.ActiveView.RightDirection);
					val3.Append(((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null) ? ((Face)info.BeamDown_Top).Reference : null);
				}
				else if (info.Type == 1 || info.Type == 4)
				{
					Plane mp4 = Plane.CreateByNormalAndOrigin(info.DownVertical.FaceNormal, info.DownVertical.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownTop), mp4);
					zero = GeomUtil.AddXYZ(info.DownVertical.Origin, 0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				else if (info.Type == 3)
				{
					Plane mp5 = Plane.CreateByNormalAndOrigin(doc.ActiveView.RightDirection, info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0));
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp5);
					zero = GeomUtil.AddXYZ(val, 0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				else
				{
					val = info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0);
					zero = GeomUtil.AddXYZ(info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0), 0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				val3.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
				val3.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
				if (info.Type >= 3)
				{
					val3.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
				}
			}
			val2 = Line.CreateUnbound(zero, doc.ActiveView.UpDirection);
			break;
		case "Left":
			if (info.IsLeftToRight == 1)
			{
				if ((GeometryObject)(object)info.BeamDown_Left != (GeometryObject)null)
				{
					Plane mp6 = Plane.CreateByNormalAndOrigin(info.BeamDown_Left.FaceNormal, info.BeamDown_Left.Origin);
					val = ((info.Type != 2 && info.Type != 3) ? TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamDown_Top), mp6) : TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp6));
					zero = GeomUtil.AddXYZ(info.BeamDown_Left.Origin, -0.4921259842519686 * doc.ActiveView.RightDirection);
					val3.Append(((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null) ? ((Face)info.BeamDown_Top).Reference : null);
				}
				else if (info.Type == 1 || info.Type == 4)
				{
					Plane mp7 = Plane.CreateByNormalAndOrigin(info.DownVertical.FaceNormal, info.DownVertical.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownTop), mp7);
					zero = GeomUtil.AddXYZ(info.DownVertical.Origin, -0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				else if (info.Type == 3)
				{
					Plane mp8 = Plane.CreateByNormalAndOrigin(doc.ActiveView.RightDirection, info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0));
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.DownBot), mp8);
					zero = GeomUtil.AddXYZ(val, -0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				else
				{
					val = info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0);
					zero = GeomUtil.AddXYZ(info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0), -0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				val3.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DeckTop_EdgeTop != (GeometryObject)null) ? info_symbol.DeckTop_EdgeTop.Reference : null);
				val3.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
				if (info.Type >= 3)
				{
					val3.Append(((GeometryObject)(object)info.DeckTop_EdgeBot != (GeometryObject)null) ? info_symbol.DeckTop_EdgeBot.Reference : null);
				}
			}
			else
			{
				if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
				{
					Plane mp9 = Plane.CreateByNormalAndOrigin(info.BeamUp_Left.FaceNormal, info.BeamUp_Left.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.BeamUp_Top), mp9);
					zero = GeomUtil.AddXYZ(info.BeamUp_Left.Origin, -0.4921259842519686 * doc.ActiveView.RightDirection);
					val3.Append(((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null) ? ((Face)info.BeamUp_Top).Reference : null);
				}
				else
				{
					Plane mp10 = Plane.CreateByNormalAndOrigin(info.UpVertical.FaceNormal, info.UpVertical.Origin);
					val = TinhToan.ProjectOnPlane(TinhToan.GetCenterOfFace(info.UpTop), mp10);
					zero = GeomUtil.AddXYZ(info.UpVertical.Origin, -0.4921259842519686 * doc.ActiveView.RightDirection);
				}
				val3.Append(((GeometryObject)(object)info.UpTop != (GeometryObject)null) ? ((Face)info_symbol.UpTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.UpBot != (GeometryObject)null) ? ((Face)info_symbol.UpBot).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownTop != (GeometryObject)null) ? ((Face)info_symbol.DownTop).Reference : null);
				val3.Append(((GeometryObject)(object)info.DownBot != (GeometryObject)null) ? ((Face)info_symbol.DownBot).Reference : null);
				if (info.Type >= 3)
				{
					val3.Append(((GeometryObject)(object)info.DeckBot_EdgeBot != (GeometryObject)null) ? info_symbol.DeckBot_EdgeBot.Reference : null);
				}
			}
			val2 = Line.CreateUnbound(zero, doc.ActiveView.UpDirection);
			break;
		case "Center":
		{
			zero = GeomUtil.Middle2Point(info.DeckTop_EdgeBot.AsCurve().GetEndPoint(0), info.DeckTop_EdgeTop.AsCurve().GetEndPoint(0));
			XYZ val5 = new XYZ(0.167, 0.167, 0.167);
			zero = GeomUtil.AddXYZ(zero, val5);
			val2 = Line.CreateUnbound(zero, info.DeckTop.FaceNormal);
			val3.Append(((GeometryObject)(object)info.DeckTop != (GeometryObject)null) ? ((Face)info_symbol.DeckTop).Reference : null);
			val3.Append(((GeometryObject)(object)info.DeckBot != (GeometryObject)null) ? ((Face)info_symbol.DeckBot).Reference : null);
			break;
		}
		}
		try
		{
			Dimension val7 = ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewDimension(doc.ActiveView, val2, val3);
			doc.Regenerate();
			if (location == "Center")
			{
				double num = 0.0;
				num = (val7.Value.HasValue ? Convert.ToDouble(val7.Value) : 0.4921259842519686);
				val7.TextPosition = val7.TextPosition.Add(GeomUtil.MultiplyVector(info.DeckBot.FaceNormal, num));
			}
			foreach (DimensionSegment segment in val7.Segments)
			{
				DimensionSegment val8 = segment;
				if (val8.ValueString == "0")
				{
					int utf = int.Parse("200E", NumberStyles.HexNumber);
					val8.ValueOverride = char.ConvertFromUtf32(utf);
				}
			}
		}
		catch (Exception)
		{
		}
		return val;
	}

	private void GetFaceInfo(Document doc, Options geomOption, Solid origin_solid, Solid solid, ref StairInfo info, ReferenceIntersector refIntersector)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		//IL_19f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_19ff: Expected O, but got Unknown
		//IL_1573: Unknown result type (might be due to invalid IL or missing references)
		//IL_1578: Unknown result type (might be due to invalid IL or missing references)
		//IL_157f: Unknown result type (might be due to invalid IL or missing references)
		//IL_16d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_16db: Expected O, but got Unknown
		//IL_1854: Unknown result type (might be due to invalid IL or missing references)
		//IL_185b: Expected O, but got Unknown
		//IL_066a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0671: Expected O, but got Unknown
		//IL_0bbd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bc4: Expected O, but got Unknown
		//IL_120c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1213: Expected O, but got Unknown
		List<PlanarFace> list = new List<PlanarFace>();
		List<PlanarFace> list2 = new List<PlanarFace>();
		List<PlanarFace> list3 = new List<PlanarFace>();
		List<PlanarFace> list4 = new List<PlanarFace>();
		View activeView = doc.ActiveView;
		_ = activeView.CropBox;
		foreach (Face face in solid.Faces)
		{
			object obj = (object)face;
			PlanarFace val = (PlanarFace)((obj is PlanarFace) ? obj : null);
			if (!((GeometryObject)(object)val != (GeometryObject)null))
			{
				continue;
			}
			double num = val.FaceNormal.DotProduct(activeView.UpDirection);
			double num2 = val.FaceNormal.DotProduct(activeView.RightDirection);
			double num3 = val.FaceNormal.DotProduct(activeView.ViewDirection);
			if (!(Math.Abs(num3 - 1.0) < 1E-05) && !(Math.Abs(num3 + 1.0) < 1E-05))
			{
				PlanarFace item = FindOriginGeometry(origin_solid, val);
				if (Math.Abs(num - 1.0) < 1E-05)
				{
					list.Add(item);
				}
				else if (Math.Abs(num + 1.0) < 1E-05)
				{
					list2.Add(item);
				}
				else if (Math.Abs(num2 - 1.0) < 1E-05 || Math.Abs(num2 + 1.0) < 1E-05)
				{
					list3.Add(item);
				}
				else
				{
					list4.Add(item);
				}
			}
		}
		int num4 = list.Count + list2.Count + list3.Count + list4.Count;
		if (num4 == 11)
		{
			info.Type = 4;
			if (list.Count == 2)
			{
				if (list[0].Origin.Z > list[1].Origin.Z)
				{
					info.UpTop = list[0];
					info.DownTop = list[1];
				}
				else
				{
					info.UpTop = list[1];
					info.DownTop = list[0];
				}
				if (list2.Count == 2)
				{
					if (list2[0].Origin.Z > list2[1].Origin.Z)
					{
						info.UpBot = list2[0];
						info.DownBot = list2[1];
					}
					else
					{
						info.UpBot = list2[1];
						info.DownBot = list2[0];
					}
					if (list4.Count == 2)
					{
						if (list4[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
						{
							info.DeckTop = list4[0];
							info.DeckBot = list4[1];
						}
						else
						{
							info.DeckTop = list4[1];
							info.DeckBot = list4[0];
						}
						if (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0)
						{
							info.IsLeftToRight = -1;
						}
						else
						{
							info.IsLeftToRight = 1;
						}
						if (list3.Count == 5)
						{
							Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
							Line obj2 = Line.CreateUnbound(doc.ActiveView.Origin, doc.ActiveView.RightDirection);
							XYZ xYZPoint = ((Curve)obj2).Project(list3[0].Origin).XYZPoint;
							XYZ xYZPoint2 = ((Curve)obj2).Project(list3[1].Origin).XYZPoint;
							XYZ xYZPoint3 = ((Curve)obj2).Project(list3[2].Origin).XYZPoint;
							XYZ xYZPoint4 = ((Curve)obj2).Project(list3[3].Origin).XYZPoint;
							XYZ xYZPoint5 = ((Curve)obj2).Project(list3[4].Origin).XYZPoint;
							List<XYZ> list5 = new List<XYZ> { xYZPoint, xYZPoint2, xYZPoint3, xYZPoint4, xYZPoint5 };
							List<XYZ> list6 = TinhToan.OrderPointOnLine(list5, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)info.IsLeftToRight), new XYZEqual());
							for (int i = 0; i < list5.Count; i++)
							{
								if (GeomUtil.KhoangCach(list5[i], list6[3]) < 1E-05)
								{
									info.Riser = list3[i];
								}
								else if (GeomUtil.KhoangCach(list5[i], list6[1]) < 1E-05)
								{
									info.WallTop = list3[i];
								}
								else if (GeomUtil.KhoangCach(list5[i], list6[2]) < 1E-05)
								{
									info.WallBot = list3[i];
								}
								else if (GeomUtil.KhoangCach(list5[i], list6[0]) < 1E-05)
								{
									info.DownVertical = list3[i];
								}
								else if (GeomUtil.KhoangCach(list5[i], list6[4]) < 1E-05)
								{
									info.UpVertical = list3[i];
								}
							}
							Plane mp = CreatePlane(info.UpBot, 0.0);
							Plane mp2 = CreatePlane(info.Riser, 0.0);
							Plane mp3 = CreatePlane(info.DeckTop, 0.0);
							Plane mp4 = CreatePlane(info.DeckBot, 0.0);
							Plane mp5 = CreatePlane(info.WallTop, 0.0);
							Plane mp6 = CreatePlane(info.WallBot, 0.0);
							foreach (Edge edge in solid.Edges)
							{
								Edge val2 = edge;
								Curve obj3 = val2.AsCurve();
								Line val3 = (Line)(object)((obj3 is Line) ? obj3 : null);
								if ((GeometryObject)(object)val3 != (GeometryObject)null && (GeomUtil.IsSameDirection(val3.Direction, doc.ActiveView.ViewDirection) || GeomUtil.IsOppositeDirection(val3.Direction, doc.ActiveView.ViewDirection)))
								{
									XYZ endPoint = ((Curve)val3).GetEndPoint(0);
									if (RbTinhToan.PointInPlane(mp3, endPoint) && RbTinhToan.PointInPlane(mp2, endPoint))
									{
										Edge deckTop_EdgeTop = FindOriginGeometry(origin_solid, val2);
										info.DeckTop_EdgeTop = deckTop_EdgeTop;
									}
									else if (RbTinhToan.PointInPlane(mp3, endPoint) && RbTinhToan.PointInPlane(mp5, endPoint))
									{
										Edge deckTop_EdgeBot = FindOriginGeometry(origin_solid, val2);
										info.DeckTop_EdgeBot = deckTop_EdgeBot;
									}
									else if (RbTinhToan.PointInPlane(mp4, endPoint) && RbTinhToan.PointInPlane(mp, endPoint))
									{
										Edge deckBot_EdgeTop = FindOriginGeometry(origin_solid, val2);
										info.DeckBot_EdgeTop = deckBot_EdgeTop;
									}
									else if (RbTinhToan.PointInPlane(mp4, endPoint) && RbTinhToan.PointInPlane(mp6, endPoint))
									{
										Edge deckBot_EdgeBot = FindOriginGeometry(origin_solid, val2);
										info.DeckBot_EdgeBot = deckBot_EdgeBot;
									}
								}
							}
						}
						else
						{
							info.Type = -1;
						}
					}
					else
					{
						info.Type = -1;
					}
				}
				else
				{
					info.Type = -1;
				}
			}
			else
			{
				info.Type = -1;
			}
		}
		if (num4 == 9)
		{
			info.Type = 1;
			if (list3.Count != 3)
			{
				info.Type = -1;
			}
			else if (list.Count == 2)
			{
				if (list[0].Origin.Z > list[1].Origin.Z)
				{
					info.UpTop = list[0];
					info.DownTop = list[1];
				}
				else
				{
					info.UpTop = list[1];
					info.DownTop = list[0];
				}
				if (list2.Count == 2)
				{
					if (list2[0].Origin.Z > list2[1].Origin.Z)
					{
						info.UpBot = list2[0];
						info.DownBot = list2[1];
					}
					else
					{
						info.UpBot = list2[1];
						info.DownBot = list2[0];
					}
					if (list4.Count == 2)
					{
						if (list4[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
						{
							info.DeckTop = list4[0];
							info.DeckBot = list4[1];
						}
						else
						{
							info.DeckTop = list4[1];
							info.DeckBot = list4[0];
						}
						if (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0)
						{
							info.IsLeftToRight = -1;
						}
						else
						{
							info.IsLeftToRight = 1;
						}
						if (list3.Count == 3)
						{
							Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
							Line obj4 = Line.CreateUnbound(doc.ActiveView.Origin, doc.ActiveView.RightDirection);
							XYZ xYZPoint6 = ((Curve)obj4).Project(list3[0].Origin).XYZPoint;
							XYZ xYZPoint7 = ((Curve)obj4).Project(list3[1].Origin).XYZPoint;
							XYZ xYZPoint8 = ((Curve)obj4).Project(list3[2].Origin).XYZPoint;
							List<XYZ> list7 = new List<XYZ> { xYZPoint6, xYZPoint7, xYZPoint8 };
							List<XYZ> list8 = TinhToan.OrderPointOnLine(list7, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (double)info.IsLeftToRight), new XYZEqual());
							for (int j = 0; j < list7.Count; j++)
							{
								if (GeomUtil.KhoangCach(list7[j], list8[1]) < 1E-05)
								{
									info.Riser = list3[j];
								}
								else if (GeomUtil.KhoangCach(list7[j], list8[0]) < 1E-05)
								{
									info.DownVertical = list3[j];
								}
								else if (GeomUtil.KhoangCach(list7[j], list8[2]) < 1E-05)
								{
									info.UpVertical = list3[j];
								}
							}
							Plane mp7 = CreatePlane(info.UpBot, 0.0);
							Plane mp8 = CreatePlane(info.Riser, 0.0);
							Plane mp9 = CreatePlane(info.DeckTop, 0.0);
							Plane mp10 = CreatePlane(info.DeckBot, 0.0);
							Plane mp11 = CreatePlane(info.DownTop, 0.0);
							Plane mp12 = CreatePlane(info.DownBot, 0.0);
							foreach (Edge edge2 in solid.Edges)
							{
								Edge val4 = edge2;
								Curve obj5 = val4.AsCurve();
								Line val5 = (Line)(object)((obj5 is Line) ? obj5 : null);
								if ((GeometryObject)(object)val5 != (GeometryObject)null && (GeomUtil.IsSameDirection(val5.Direction, doc.ActiveView.ViewDirection) || GeomUtil.IsOppositeDirection(val5.Direction, doc.ActiveView.ViewDirection)))
								{
									XYZ endPoint2 = ((Curve)val5).GetEndPoint(0);
									if (RbTinhToan.PointInPlane(mp9, endPoint2) && RbTinhToan.PointInPlane(mp8, endPoint2))
									{
										Edge deckTop_EdgeTop2 = FindOriginGeometry(origin_solid, val4);
										info.DeckTop_EdgeTop = deckTop_EdgeTop2;
									}
									else if (RbTinhToan.PointInPlane(mp9, endPoint2) && RbTinhToan.PointInPlane(mp11, endPoint2))
									{
										Edge deckTop_EdgeBot2 = FindOriginGeometry(origin_solid, val4);
										info.DeckTop_EdgeBot = deckTop_EdgeBot2;
									}
									else if (RbTinhToan.PointInPlane(mp10, endPoint2) && RbTinhToan.PointInPlane(mp7, endPoint2))
									{
										Edge deckBot_EdgeTop2 = FindOriginGeometry(origin_solid, val4);
										info.DeckBot_EdgeTop = deckBot_EdgeTop2;
									}
									else if (RbTinhToan.PointInPlane(mp10, endPoint2) && RbTinhToan.PointInPlane(mp12, endPoint2))
									{
										Edge deckBot_EdgeBot2 = FindOriginGeometry(origin_solid, val4);
										info.DeckBot_EdgeBot = deckBot_EdgeBot2;
									}
								}
							}
						}
						else
						{
							info.Type = -1;
						}
					}
					else
					{
						info.Type = -1;
					}
				}
				else
				{
					info.Type = -1;
				}
			}
			else
			{
				info.Type = -1;
			}
		}
		if ((num4 == 7 || num4 == 9) && info.Type == -1)
		{
			info.Type = 2;
			if (list.Count == 1)
			{
				info.UpTop = list[0];
				if (list2.Count == 2)
				{
					if (list2[0].Origin.Z > list2[1].Origin.Z)
					{
						info.UpBot = list2[0];
						info.DownBot = list2[1];
					}
					else
					{
						info.UpBot = list2[1];
						info.DownBot = list2[0];
					}
					if (list4.Count == 2)
					{
						if (list4[0].FaceNormal.DotProduct(activeView.UpDirection) > 0.0)
						{
							info.DeckTop = list4[0];
							info.DeckBot = list4[1];
						}
						else
						{
							info.DeckTop = list4[1];
							info.DeckBot = list4[0];
						}
						if (info.DeckTop.FaceNormal.DotProduct(activeView.RightDirection) > 0.0)
						{
							info.IsLeftToRight = -1;
						}
						else
						{
							info.IsLeftToRight = 1;
						}
						if (list3.Count == 2)
						{
							foreach (PlanarFace item2 in list3)
							{
								if (item2.FaceNormal.DotProduct(info.DeckTop.FaceNormal) > 0.0)
								{
									info.Riser = item2;
								}
								else
								{
									info.UpVertical = item2;
								}
							}
						}
						else
						{
							if (list3.Count != 4)
							{
								info.Type = -1;
								goto IL_1345;
							}
							info.Type = 3;
							List<PlanarFace> list9 = new List<PlanarFace>();
							List<PlanarFace> list10 = new List<PlanarFace>();
							foreach (PlanarFace item3 in list3)
							{
								if (item3.FaceNormal.DotProduct(info.DeckTop.FaceNormal) > 0.0)
								{
									list9.Add(item3);
								}
								else
								{
									list10.Add(item3);
								}
							}
							XYZ obj6 = ((Face)list9[0]).Evaluate((((Face)list9[0]).GetBoundingBox().Max + ((Face)list9[0]).GetBoundingBox().Min) / 2.0);
							XYZ val6 = ((Face)list9[1]).Evaluate((((Face)list9[1]).GetBoundingBox().Max + ((Face)list9[1]).GetBoundingBox().Min) / 2.0);
							XYZ val7 = ((Face)list10[0]).Evaluate((((Face)list10[0]).GetBoundingBox().Max + ((Face)list10[0]).GetBoundingBox().Min) / 2.0);
							XYZ val8 = ((Face)list10[1]).Evaluate((((Face)list10[1]).GetBoundingBox().Max + ((Face)list10[1]).GetBoundingBox().Min) / 2.0);
							if (obj6.Z > val6.Z)
							{
								info.Riser = list9[0];
								info.WallTop = list9[1];
							}
							else
							{
								info.Riser = list9[1];
								info.WallTop = list9[0];
							}
							if (val7.Z > val8.Z)
							{
								info.UpVertical = list10[0];
								info.WallBot = list10[1];
							}
							else
							{
								info.UpVertical = list10[1];
								info.WallBot = list10[0];
							}
						}
						Plane mp13 = CreatePlane(info.UpBot, 0.0);
						Plane mp14 = CreatePlane(info.Riser, 0.0);
						Plane mp15 = CreatePlane(info.DeckTop, 0.0);
						Plane mp16 = CreatePlane(info.DeckBot, 0.0);
						Plane val9 = CreatePlane(info.DownBot, 0.0);
						Plane mp17 = val9;
						if (info.Type == 3)
						{
							mp17 = CreatePlane(info.WallTop, 0.0);
							val9 = CreatePlane(info.WallBot, 0.0);
						}
						foreach (Edge edge3 in solid.Edges)
						{
							Edge val10 = edge3;
							Curve obj7 = val10.AsCurve();
							Line val11 = (Line)(object)((obj7 is Line) ? obj7 : null);
							if ((GeometryObject)(object)val11 != (GeometryObject)null && (GeomUtil.IsSameDirection(val11.Direction, doc.ActiveView.ViewDirection) || GeomUtil.IsOppositeDirection(val11.Direction, doc.ActiveView.ViewDirection)))
							{
								XYZ endPoint3 = ((Curve)val11).GetEndPoint(0);
								if (RbTinhToan.PointInPlane(mp15, endPoint3) && RbTinhToan.PointInPlane(mp14, endPoint3))
								{
									Edge deckTop_EdgeTop3 = FindOriginGeometry(origin_solid, val10);
									info.DeckTop_EdgeTop = deckTop_EdgeTop3;
								}
								else if (RbTinhToan.PointInPlane(mp15, endPoint3) && RbTinhToan.PointInPlane(mp17, endPoint3))
								{
									Edge deckTop_EdgeBot3 = FindOriginGeometry(origin_solid, val10);
									info.DeckTop_EdgeBot = deckTop_EdgeBot3;
								}
								else if (RbTinhToan.PointInPlane(mp16, endPoint3) && RbTinhToan.PointInPlane(mp13, endPoint3))
								{
									Edge deckBot_EdgeTop3 = FindOriginGeometry(origin_solid, val10);
									info.DeckBot_EdgeTop = deckBot_EdgeTop3;
								}
								else if (RbTinhToan.PointInPlane(mp16, endPoint3) && RbTinhToan.PointInPlane(val9, endPoint3))
								{
									Edge deckBot_EdgeBot3 = FindOriginGeometry(origin_solid, val10);
									info.DeckBot_EdgeBot = deckBot_EdgeBot3;
								}
							}
						}
					}
					else
					{
						info.Type = -1;
					}
				}
				else
				{
					info.Type = -1;
				}
			}
			else
			{
				info.Type = -1;
			}
		}
		goto IL_1345;
		IL_1345:
		if (refIntersector != null)
		{
			if ((GeometryObject)(object)info.DeckBot != (GeometryObject)null && (GeometryObject)(object)info.DeckTop != (GeometryObject)null)
			{
				XYZ val12 = ((Face)info.DeckTop).Evaluate((((Face)info.DeckTop).GetBoundingBox().Max + ((Face)info.DeckTop).GetBoundingBox().Min) / 2.0) * 0.5 + ((Face)info.DeckBot).Evaluate((((Face)info.DeckBot).GetBoundingBox().Max + ((Face)info.DeckBot).GetBoundingBox().Min) / 2.0) * 0.5;
				StairInfo obj8 = info;
				GeometryObject geometryObjectFromReference = info.Host.GetGeometryObjectFromReference(refIntersector.FindNearest(val12, doc.ActiveView.ViewDirection).GetReference());
				obj8.StartFace = (PlanarFace)(object)((geometryObjectFromReference is PlanarFace) ? geometryObjectFromReference : null);
				StairInfo obj9 = info;
				GeometryObject geometryObjectFromReference2 = info.Host.GetGeometryObjectFromReference(refIntersector.FindNearest(val12, doc.ActiveView.ViewDirection.Negate()).GetReference());
				obj9.EndFace = (PlanarFace)(object)((geometryObjectFromReference2 is PlanarFace) ? geometryObjectFromReference2 : null);
				info.Mp_view = Plane.CreateByNormalAndOrigin(doc.ActiveView.ViewDirection, doc.ActiveView.Origin);
				info.Mp_start = CreatePlane(info.StartFace, 0.0 - info.Cover - info.ThepChu.BarModelDiameter / 2.0);
				XYZ val13 = TinhToan.ProjectOnPlane(info.EndFace.Origin, info.Mp_start);
				XYZ val14 = GeomUtil.SubXYZ(info.EndFace.Origin, val13);
				info.Rebar_Direction = val14;
				info.Rebar_range = val14.GetLength() - info.Cover - info.ThepChu.BarModelDiameter / 2.0;
			}
			else
			{
				info.Type = -1;
			}
		}
		if (info.Type <= 0)
		{
			return;
		}
		FilteredElementCollector val15 = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id);
		val15.WhereElementIsNotElementType();
		val15.OfCategory((BuiltInCategory)(-2001320));
		IList<Element> list11 = val15.ToElements();
		List<Solid> list12 = new List<Solid>();
		List<Solid> list13 = new List<Solid>();
		Solid val16 = TinhToan.CreateSolidFromBoundingBox(doc.ActiveView.CropBox);
		foreach (Element item4 in list11)
		{
			Solid val17 = null;
			Solid val18 = null;
			try
			{
				val18 = TinhToan.GetSolidByGeometry(item4.get_Geometry(geomOption), instance: false)[0];
				val17 = BooleanOperationsUtils.ExecuteBooleanOperation(val16, val18, (BooleanOperationsType)2);
			}
			catch (Exception)
			{
			}
			if ((GeometryObject)(object)val17 != (GeometryObject)null && val17.Volume != 0.0)
			{
				list12.Add(val17);
				list13.Add(val18);
			}
		}
		if (list12.Count == 2)
		{
			Solid val19;
			Solid origin_solid2;
			Solid val20;
			Solid origin_solid3;
			if (list12[0].ComputeCentroid().Z < list12[1].ComputeCentroid().Z)
			{
				val19 = list12[1];
				origin_solid2 = list13[1];
				val20 = list12[0];
				origin_solid3 = list13[0];
			}
			else
			{
				val19 = list12[0];
				origin_solid2 = list13[0];
				val20 = list12[1];
				origin_solid3 = list13[1];
			}
			foreach (Face face2 in val19.Faces)
			{
				Face val21 = face2;
				PlanarFace val22 = FindOriginGeometry(origin_solid2, (PlanarFace)(object)((val21 is PlanarFace) ? val21 : null));
				if (!((GeometryObject)(object)val22 != (GeometryObject)null))
				{
					continue;
				}
				double num5 = val22.FaceNormal.DotProduct(activeView.ViewDirection);
				if (Math.Abs(num5 - 1.0) < 1E-05 || Math.Abs(num5 + 1.0) < 1E-05)
				{
					continue;
				}
				double num6 = val22.FaceNormal.DotProduct(activeView.UpDirection);
				if (Math.Abs(num6 - 1.0) < 1E-05)
				{
					info.BeamUp_Top = val22;
					continue;
				}
				if (Math.Abs(num6 + 1.0) < 1E-05)
				{
					info.BeamUp_Bot = val22;
					continue;
				}
				double num7 = val22.FaceNormal.DotProduct(activeView.RightDirection);
				if (Math.Abs(num7 - 1.0) < 1E-05)
				{
					info.BeamUp_Right = val22;
				}
				else if (Math.Abs(num7 + 1.0) < 1E-05)
				{
					info.BeamUp_Left = val22;
				}
			}
			foreach (Face face3 in val20.Faces)
			{
				Face val23 = face3;
				PlanarFace val24 = FindOriginGeometry(origin_solid3, (PlanarFace)(object)((val23 is PlanarFace) ? val23 : null));
				if (!((GeometryObject)(object)val24 != (GeometryObject)null))
				{
					continue;
				}
				double num8 = val24.FaceNormal.DotProduct(activeView.ViewDirection);
				if (Math.Abs(num8 - 1.0) < 1E-05 || Math.Abs(num8 + 1.0) < 1E-05)
				{
					continue;
				}
				double num9 = val24.FaceNormal.DotProduct(activeView.UpDirection);
				if (Math.Abs(num9 - 1.0) < 1E-05)
				{
					info.BeamDown_Top = val24;
					continue;
				}
				if (Math.Abs(num9 + 1.0) < 1E-05)
				{
					info.BeamDown_Bot = val24;
					continue;
				}
				double num10 = val24.FaceNormal.DotProduct(activeView.RightDirection);
				if (Math.Abs(num10 - 1.0) < 1E-05)
				{
					info.BeamDown_Right = val24;
				}
				else if (Math.Abs(num10 + 1.0) < 1E-05)
				{
					info.BeamDown_Left = val24;
				}
			}
		}
		else if (list12.Count == 1)
		{
			Solid val19 = list12[0];
			Solid origin_solid2 = list13[0];
			foreach (Face face4 in val19.Faces)
			{
				Face val25 = face4;
				PlanarFace val26 = FindOriginGeometry(origin_solid2, (PlanarFace)(object)((val25 is PlanarFace) ? val25 : null));
				if (!((GeometryObject)(object)val26 != (GeometryObject)null))
				{
					continue;
				}
				double num11 = val26.FaceNormal.DotProduct(activeView.ViewDirection);
				if (Math.Abs(num11 - 1.0) < 1E-05 || Math.Abs(num11 + 1.0) < 1E-05)
				{
					continue;
				}
				double num12 = val26.FaceNormal.DotProduct(activeView.UpDirection);
				if (Math.Abs(num12 - 1.0) < 1E-05)
				{
					info.BeamUp_Top = val26;
					info.BeamDown_Top = val26;
					continue;
				}
				if (Math.Abs(num12 + 1.0) < 1E-05)
				{
					info.BeamUp_Bot = val26;
					info.BeamDown_Bot = val26;
					continue;
				}
				double num13 = val26.FaceNormal.DotProduct(activeView.RightDirection);
				if (Math.Abs(num13 - 1.0) < 1E-05)
				{
					info.BeamUp_Right = val26;
					info.BeamDown_Right = val26;
				}
				else if (Math.Abs(num13 + 1.0) < 1E-05)
				{
					info.BeamUp_Left = val26;
					info.BeamDown_Left = val26;
				}
			}
		}
		if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null)
		{
			if (info.IsLeftToRight == 1)
			{
				XYZ centerOfFace = TinhToan.GetCenterOfFace(info.UpTop);
				centerOfFace = TinhToan.ProjectOnPlane(centerOfFace, CreatePlane(info.UpVertical, 0.0));
				IntersectionResult val27 = ((Face)info.BeamUp_Left).Project(centerOfFace);
				if (val27 == null || GeomUtil.KhoangCach(centerOfFace, val27.XYZPoint) > 1E-05)
				{
					info.BeamUp_Bot = null;
					info.BeamUp_Top = null;
					info.BeamUp_Left = null;
					info.BeamUp_Right = null;
				}
			}
			else
			{
				XYZ centerOfFace2 = TinhToan.GetCenterOfFace(info.UpTop);
				centerOfFace2 = TinhToan.ProjectOnPlane(centerOfFace2, CreatePlane(info.UpVertical, 0.0));
				IntersectionResult val28 = ((Face)info.BeamUp_Right).Project(centerOfFace2);
				if (val28 == null || GeomUtil.KhoangCach(centerOfFace2, val28.XYZPoint) > 1E-05)
				{
					info.BeamUp_Bot = null;
					info.BeamUp_Top = null;
					info.BeamUp_Left = null;
					info.BeamUp_Right = null;
				}
			}
		}
		if (!((GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null))
		{
			return;
		}
		if (info.Type == 1 || info.Type == 4)
		{
			if (info.IsLeftToRight == 1)
			{
				XYZ centerOfFace3 = TinhToan.GetCenterOfFace(info.DownTop);
				centerOfFace3 = TinhToan.ProjectOnPlane(centerOfFace3, CreatePlane(info.DownVertical, 0.0));
				IntersectionResult val29 = ((Face)info.BeamDown_Right).Project(centerOfFace3);
				if (val29 == null || GeomUtil.KhoangCach(centerOfFace3, val29.XYZPoint) > 1E-05)
				{
					info.BeamDown_Bot = null;
					info.BeamDown_Top = null;
					info.BeamDown_Left = null;
					info.BeamDown_Right = null;
				}
			}
			else
			{
				XYZ centerOfFace4 = TinhToan.GetCenterOfFace(info.DownTop);
				centerOfFace4 = TinhToan.ProjectOnPlane(centerOfFace4, CreatePlane(info.DownVertical, 0.0));
				IntersectionResult val30 = ((Face)info.BeamDown_Left).Project(centerOfFace4);
				if (val30 == null || GeomUtil.KhoangCach(centerOfFace4, val30.XYZPoint) > 1E-05)
				{
					info.BeamDown_Bot = null;
					info.BeamDown_Top = null;
					info.BeamDown_Left = null;
					info.BeamDown_Right = null;
				}
			}
		}
		if (info.Type != 2 && info.Type != 3)
		{
			return;
		}
		if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null && ((Face)info.BeamDown_Top).Reference.ConvertToStableRepresentation(doc) == ((Face)info.BeamUp_Top).Reference.ConvertToStableRepresentation(doc))
		{
			info.BeamDown_Bot = null;
			info.BeamDown_Top = null;
			info.BeamDown_Left = null;
			info.BeamDown_Right = null;
		}
		if ((GeometryObject)(object)info.BeamUp_Top != (GeometryObject)null && (GeometryObject)(object)info.BeamDown_Top != (GeometryObject)null)
		{
			Plane mp18 = Plane.CreateByNormalAndOrigin(info.BeamUp_Left.FaceNormal, info.BeamUp_Left.Origin);
			XYZ val31 = TinhToan.ProjectOnPlane(info.BeamDown_Left.Origin, mp18);
			if (GeomUtil.KhoangCach(info.BeamDown_Left.Origin, val31) < 1E-05)
			{
				info.BeamDown_Bot = null;
				info.BeamDown_Top = null;
				info.BeamDown_Left = null;
				info.BeamDown_Right = null;
			}
		}
	}

	private Edge FindOriginGeometry(Solid origin_solid, Edge edge)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		foreach (Edge edge2 in origin_solid.Edges)
		{
			Edge val = edge2;
			Curve obj = val.AsCurve();
			Line line = (Line)(object)((obj is Line) ? obj : null);
			Curve obj2 = edge.AsCurve();
			Line val2 = (Line)(object)((obj2 is Line) ? obj2 : null);
			if (TinhToan.DiemThuocDuongThang(((Curve)val2).GetEndPoint(0), line) && TinhToan.DiemThuocDuongThang(((Curve)val2).GetEndPoint(1), line))
			{
				return val;
			}
		}
		return edge;
	}

	private PlanarFace FindOriginGeometry(Solid origin_solid, PlanarFace face)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		foreach (PlanarFace face2 in origin_solid.Faces)
		{
			PlanarFace val = face2;
			if (TinhToan.CheckFaceInFace((Face)(object)face, (Face)(object)val))
			{
				return val;
			}
		}
		return face;
	}

	private Plane CreatePlane(PlanarFace upTop, double offset)
	{
		XYZ val = upTop.Origin + upTop.FaceNormal * offset;
		return Plane.CreateByNormalAndOrigin(upTop.FaceNormal, val);
	}

	private IndependentTag DatTagRebar(Document doc, Rebar rb, XYZ tag_center, StairInfo info, string[] typetag)
	{
		double num = 600.0;
		double num2 = -200.0;
		int num3 = info.IsLeftToRight;
		try
		{
			num = double.Parse(typetag[1]);
			num2 = double.Parse(typetag[2]);
			num3 = 1;
		}
		catch (Exception)
		{
		}
		IndependentTag val = null;
		if (typetag[0] == "GC")
		{
			XYZ val2 = GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.UpDirection, num2 / 304.79999999999995 * (double)num3));
			val = IndependentTag.Create(doc, info.Symbol_GC, ((Element)doc.ActiveView).Id, ((Element)rb).GetSubelements().FirstOrDefault().GetReference(), true, (TagOrientation)0, val2);
			val.LeaderEndCondition = (LeaderEndCondition)1;
			val.LeaderEnd(tag_center);
			val.TagHeadPosition = val2 + GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, num / 304.79999999999995);
		}
		if (typetag[0] == "Main")
		{
			tag_center = GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.UpDirection, num2 / 304.79999999999995 * (double)num3));
			val = IndependentTag.Create(doc, info.Symbol_Main, ((Element)doc.ActiveView).Id, ((Element)rb).GetSubelements().FirstOrDefault().GetReference(), true, (TagOrientation)0, tag_center);
			val.TagHeadPosition = tag_center + GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, num / 304.79999999999995);
			val.LeaderElbow(GeomUtil.AddXYZ(tag_center, GeomUtil.MultiplyVector(doc.ActiveView.RightDirection, (num - 400.0) / 304.79999999999995)));
		}
		return val;
	}
}











