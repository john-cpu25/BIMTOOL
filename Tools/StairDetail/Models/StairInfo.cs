using HuyAddin;
using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;


namespace RincoNhan.Tools.StairDetail;

internal class StairInfo : ICloneable
{
	private PlanarFace startFace;

	private PlanarFace endFace;

	private Plane mp_start;

	private Plane mp_view;

	private double rebar_range;

	private XYZ rebar_Direction;

	private PlanarFace upVertical;

	private PlanarFace upTop;

	private PlanarFace upBot;

	private PlanarFace riser;

	private PlanarFace deckTop;

	private PlanarFace deckBot;

	private PlanarFace downVertical;

	private PlanarFace downTop;

	private PlanarFace downBot;

	private PlanarFace wallTop;

	private PlanarFace wallBot;

	private PlanarFace beamDown_Top;

	private PlanarFace beamDown_Bot;

	private PlanarFace beamDown_Left;

	private PlanarFace beamDown_Right;

	private PlanarFace beamUp_Top;

	private PlanarFace beamUp_Bot;

	private PlanarFace beamUp_Left;

	private PlanarFace beamUp_Right;

	private Edge deckTop_EdgeTop;

	private Edge deckTop_EdgeBot;

	private Edge deckBot_EdgeTop;

	private Edge deckBot_EdgeBot;

	private int type = -1;

	private int isLeftToRight = 1;

	private double thickness;

	private double cover;

	private RebarBarType thepChu;

	private double thepChu_KhoangRai;

	private double doanNeo;

	private RebarBarType thepGiaCuong;

	private double thepGiaCuong_KhoangRai;

	private double chieuDaiToiThieu;

	private ElementId symbol_Main;

	private ElementId symbol_GC;

	private Element host;

	private string partition;

	public PlanarFace UpTop
	{
		get
		{
			return upTop;
		}
		set
		{
			upTop = value;
		}
	}

	public PlanarFace UpBot
	{
		get
		{
			return upBot;
		}
		set
		{
			upBot = value;
		}
	}

	public PlanarFace Riser
	{
		get
		{
			return riser;
		}
		set
		{
			riser = value;
		}
	}

	public PlanarFace DeckTop
	{
		get
		{
			return deckTop;
		}
		set
		{
			deckTop = value;
		}
	}

	public PlanarFace DeckBot
	{
		get
		{
			return deckBot;
		}
		set
		{
			deckBot = value;
		}
	}

	public PlanarFace DownTop
	{
		get
		{
			return downTop;
		}
		set
		{
			downTop = value;
		}
	}

	public PlanarFace DownBot
	{
		get
		{
			return downBot;
		}
		set
		{
			downBot = value;
		}
	}

	public int Type
	{
		get
		{
			return type;
		}
		set
		{
			type = value;
		}
	}

	public PlanarFace BeamDown_Top
	{
		get
		{
			return beamDown_Top;
		}
		set
		{
			beamDown_Top = value;
		}
	}

	public PlanarFace BeamDown_Bot
	{
		get
		{
			return beamDown_Bot;
		}
		set
		{
			beamDown_Bot = value;
		}
	}

	public PlanarFace BeamDown_Left
	{
		get
		{
			return beamDown_Left;
		}
		set
		{
			beamDown_Left = value;
		}
	}

	public PlanarFace BeamDown_Right
	{
		get
		{
			return beamDown_Right;
		}
		set
		{
			beamDown_Right = value;
		}
	}

	public PlanarFace BeamUp_Top
	{
		get
		{
			return beamUp_Top;
		}
		set
		{
			beamUp_Top = value;
		}
	}

	public PlanarFace BeamUp_Bot
	{
		get
		{
			return beamUp_Bot;
		}
		set
		{
			beamUp_Bot = value;
		}
	}

	public PlanarFace BeamUp_Left
	{
		get
		{
			return beamUp_Left;
		}
		set
		{
			beamUp_Left = value;
		}
	}

	public PlanarFace BeamUp_Right
	{
		get
		{
			return beamUp_Right;
		}
		set
		{
			beamUp_Right = value;
		}
	}

	public Edge DeckTop_EdgeTop
	{
		get
		{
			return deckTop_EdgeTop;
		}
		set
		{
			deckTop_EdgeTop = value;
		}
	}

	public Edge DeckTop_EdgeBot
	{
		get
		{
			return deckTop_EdgeBot;
		}
		set
		{
			deckTop_EdgeBot = value;
		}
	}

	public Edge DeckBot_EdgeTop
	{
		get
		{
			return deckBot_EdgeTop;
		}
		set
		{
			deckBot_EdgeTop = value;
		}
	}

	public Edge DeckBot_EdgeBot
	{
		get
		{
			return deckBot_EdgeBot;
		}
		set
		{
			deckBot_EdgeBot = value;
		}
	}

	public double Cover
	{
		get
		{
			return cover;
		}
		set
		{
			cover = value;
		}
	}

	public RebarBarType ThepChu
	{
		get
		{
			return thepChu;
		}
		set
		{
			thepChu = value;
		}
	}

	public RebarBarType ThepGiaCuong
	{
		get
		{
			return thepGiaCuong;
		}
		set
		{
			thepGiaCuong = value;
		}
	}

	public double ThepChu_KhoangRai
	{
		get
		{
			return thepChu_KhoangRai;
		}
		set
		{
			thepChu_KhoangRai = value;
		}
	}

	public double ThepGiaCuong_KhoangRai
	{
		get
		{
			return thepGiaCuong_KhoangRai;
		}
		set
		{
			thepGiaCuong_KhoangRai = value;
		}
	}

	public Element Host
	{
		get
		{
			return host;
		}
		set
		{
			host = value;
		}
	}

	public int IsLeftToRight
	{
		get
		{
			return isLeftToRight;
		}
		set
		{
			isLeftToRight = value;
		}
	}

	public PlanarFace StartFace
	{
		get
		{
			return startFace;
		}
		set
		{
			startFace = value;
		}
	}

	public PlanarFace EndFace
	{
		get
		{
			return endFace;
		}
		set
		{
			endFace = value;
		}
	}

	public double Rebar_range
	{
		get
		{
			return rebar_range;
		}
		set
		{
			rebar_range = value;
		}
	}

	public Plane Mp_start
	{
		get
		{
			return mp_start;
		}
		set
		{
			mp_start = value;
		}
	}

	public XYZ Rebar_Direction
	{
		get
		{
			return rebar_Direction;
		}
		set
		{
			rebar_Direction = value;
		}
	}

	public double ChieuDaiToiThieu
	{
		get
		{
			return chieuDaiToiThieu;
		}
		set
		{
			chieuDaiToiThieu = value;
		}
	}

	public ElementId Symbol_Main
	{
		get
		{
			return symbol_Main;
		}
		set
		{
			symbol_Main = value;
		}
	}

	public ElementId Symbol_GC
	{
		get
		{
			return symbol_GC;
		}
		set
		{
			symbol_GC = value;
		}
	}

	public string Partition
	{
		get
		{
			return partition;
		}
		set
		{
			partition = value;
		}
	}

	public PlanarFace WallTop
	{
		get
		{
			return wallTop;
		}
		set
		{
			wallTop = value;
		}
	}

	public PlanarFace WallBot
	{
		get
		{
			return wallBot;
		}
		set
		{
			wallBot = value;
		}
	}

	public double DoanNeo
	{
		get
		{
			return doanNeo;
		}
		set
		{
			doanNeo = value;
		}
	}

	public PlanarFace UpVertical
	{
		get
		{
			return upVertical;
		}
		set
		{
			upVertical = value;
		}
	}

	public PlanarFace DownVertical
	{
		get
		{
			return downVertical;
		}
		set
		{
			downVertical = value;
		}
	}

	public Plane Mp_view
	{
		get
		{
			return mp_view;
		}
		set
		{
			mp_view = value;
		}
	}

	public double Thickness
	{
		get
		{
			return thickness;
		}
		set
		{
			thickness = value;
		}
	}

	public object Clone()
	{
		return MemberwiseClone();
	}

	public void CheckInfo(Document doc)
	{
		Curve val = null;
		if ((GeometryObject)(object)UpTop != (GeometryObject)null)
		{
			PlanarFace obj = UpTop;
			BoundingBoxUV boundingBox = ((Face)obj).GetBoundingBox();
			XYZ val2 = ((Face)obj).Evaluate((boundingBox.Max + boundingBox.Min) / 2.0);
			TextNote obj2 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val2, "UpTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj2.AddLeader((TextNoteLeaderTypes)0);
			obj2.GetLeaders().First().End = val2;
		}
		if ((GeometryObject)(object)UpBot != (GeometryObject)null)
		{
			PlanarFace obj3 = UpBot;
			BoundingBoxUV boundingBox2 = ((Face)obj3).GetBoundingBox();
			XYZ val3 = ((Face)obj3).Evaluate((boundingBox2.Max + boundingBox2.Min) / 2.0);
			TextNote obj4 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val3, "UpBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj4.AddLeader((TextNoteLeaderTypes)0);
			obj4.GetLeaders().First().End = val3;
		}
		if ((GeometryObject)(object)UpVertical != (GeometryObject)null)
		{
			PlanarFace obj5 = UpVertical;
			BoundingBoxUV boundingBox3 = ((Face)obj5).GetBoundingBox();
			XYZ val4 = ((Face)obj5).Evaluate((boundingBox3.Max + boundingBox3.Min) / 2.0);
			TextNote obj6 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val4, "UpVertical", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj6.AddLeader((TextNoteLeaderTypes)0);
			obj6.GetLeaders().First().End = val4;
		}
		if ((GeometryObject)(object)Riser != (GeometryObject)null)
		{
			PlanarFace obj7 = Riser;
			BoundingBoxUV boundingBox4 = ((Face)obj7).GetBoundingBox();
			XYZ val5 = ((Face)obj7).Evaluate((boundingBox4.Max + boundingBox4.Min) / 2.0);
			TextNote obj8 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val5, "Riser", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj8.AddLeader((TextNoteLeaderTypes)0);
			obj8.GetLeaders().First().End = val5;
		}
		if ((GeometryObject)(object)DeckTop != (GeometryObject)null)
		{
			PlanarFace obj9 = DeckTop;
			BoundingBoxUV boundingBox5 = ((Face)obj9).GetBoundingBox();
			XYZ val6 = ((Face)obj9).Evaluate((boundingBox5.Max + boundingBox5.Min) / 2.0);
			TextNote obj10 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val6, "DeckTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj10.AddLeader((TextNoteLeaderTypes)0);
			obj10.GetLeaders().First().End = val6;
		}
		if ((GeometryObject)(object)DeckBot != (GeometryObject)null)
		{
			PlanarFace obj11 = DeckBot;
			BoundingBoxUV boundingBox6 = ((Face)obj11).GetBoundingBox();
			XYZ val7 = ((Face)obj11).Evaluate((boundingBox6.Max + boundingBox6.Min) / 2.0);
			TextNote obj12 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val7, "DeckBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj12.AddLeader((TextNoteLeaderTypes)0);
			obj12.GetLeaders().First().End = val7;
		}
		if ((GeometryObject)(object)DownTop != (GeometryObject)null)
		{
			PlanarFace obj13 = DownTop;
			BoundingBoxUV boundingBox7 = ((Face)obj13).GetBoundingBox();
			XYZ val8 = ((Face)obj13).Evaluate((boundingBox7.Max + boundingBox7.Min) / 2.0);
			TextNote obj14 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val8, "DownTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj14.AddLeader((TextNoteLeaderTypes)0);
			obj14.GetLeaders().First().End = val8;
		}
		if ((GeometryObject)(object)DownBot != (GeometryObject)null)
		{
			PlanarFace obj15 = DownBot;
			BoundingBoxUV boundingBox8 = ((Face)obj15).GetBoundingBox();
			XYZ val9 = ((Face)obj15).Evaluate((boundingBox8.Max + boundingBox8.Min) / 2.0);
			TextNote obj16 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val9, "DownBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj16.AddLeader((TextNoteLeaderTypes)0);
			obj16.GetLeaders().First().End = val9;
		}
		if ((GeometryObject)(object)DownVertical != (GeometryObject)null)
		{
			PlanarFace obj17 = DownVertical;
			BoundingBoxUV boundingBox9 = ((Face)obj17).GetBoundingBox();
			XYZ val10 = ((Face)obj17).Evaluate((boundingBox9.Max + boundingBox9.Min) / 2.0);
			TextNote obj18 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val10, "DownVertical", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj18.AddLeader((TextNoteLeaderTypes)0);
			obj18.GetLeaders().First().End = val10;
		}
		if ((GeometryObject)(object)WallTop != (GeometryObject)null)
		{
			PlanarFace obj19 = WallTop;
			BoundingBoxUV boundingBox10 = ((Face)obj19).GetBoundingBox();
			XYZ val11 = ((Face)obj19).Evaluate((boundingBox10.Max + boundingBox10.Min) / 2.0);
			TextNote obj20 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val11, "WallTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj20.AddLeader((TextNoteLeaderTypes)0);
			obj20.GetLeaders().First().End = val11;
		}
		if ((GeometryObject)(object)WallBot != (GeometryObject)null)
		{
			PlanarFace obj21 = WallBot;
			BoundingBoxUV boundingBox11 = ((Face)obj21).GetBoundingBox();
			XYZ val12 = ((Face)obj21).Evaluate((boundingBox11.Max + boundingBox11.Min) / 2.0);
			TextNote obj22 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val12, "WallBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj22.AddLeader((TextNoteLeaderTypes)0);
			obj22.GetLeaders().First().End = val12;
		}
		if ((GeometryObject)(object)DeckTop_EdgeTop != (GeometryObject)null)
		{
			val = DeckTop_EdgeTop.AsCurve();
			TextNote obj23 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val.GetEndPoint(0), "DeckTop_EdgeTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj23.AddLeader((TextNoteLeaderTypes)0);
			obj23.GetLeaders().First().End = GeomUtil.Middle2Point(val.GetEndPoint(0), val.GetEndPoint(1));
		}
		if ((GeometryObject)(object)DeckTop_EdgeBot != (GeometryObject)null)
		{
			val = DeckTop_EdgeBot.AsCurve();
			TextNote obj24 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val.GetEndPoint(0), "DeckTop_EdgeBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj24.AddLeader((TextNoteLeaderTypes)0);
			obj24.GetLeaders().First().End = GeomUtil.Middle2Point(val.GetEndPoint(0), val.GetEndPoint(1));
		}
		if ((GeometryObject)(object)DeckBot_EdgeTop != (GeometryObject)null)
		{
			val = DeckBot_EdgeTop.AsCurve();
			TextNote obj25 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val.GetEndPoint(0), "DeckBot_EdgeTop", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj25.AddLeader((TextNoteLeaderTypes)0);
			obj25.GetLeaders().First().End = GeomUtil.Middle2Point(val.GetEndPoint(0), val.GetEndPoint(1));
		}
		if ((GeometryObject)(object)DeckBot_EdgeBot != (GeometryObject)null)
		{
			val = DeckBot_EdgeBot.AsCurve();
			TextNote obj26 = TextNote.Create(doc, ((Element)doc.ActiveView).Id, val.GetEndPoint(0), "DeckBot_EdgeBot", doc.GetDefaultElementTypeId((ElementTypeGroup)12));
			obj26.AddLeader((TextNoteLeaderTypes)0);
			obj26.GetLeaders().First().End = GeomUtil.Middle2Point(val.GetEndPoint(0), val.GetEndPoint(1));
		}
	}

	public void Regenerate()
	{
		PropertyInfo[] properties = GetType().GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			object? value = propertyInfo.GetValue(this);
			PlanarFace val = (PlanarFace)((value is PlanarFace) ? value : null);
			if ((GeometryObject)(object)val != (GeometryObject)null)
			{
				GeometryObject geometryObjectFromReference = Host.GetGeometryObjectFromReference(((Face)val).Reference);
				PlanarFace val2 = (PlanarFace)(object)((geometryObjectFromReference is PlanarFace) ? geometryObjectFromReference : null);
				if ((GeometryObject)(object)val2 != (GeometryObject)null)
				{
					propertyInfo.SetValue(this, val2);
				}
			}
			object? value2 = propertyInfo.GetValue(this);
			Edge val3 = (Edge)((value2 is Edge) ? value2 : null);
			if (val3 != null)
			{
				propertyInfo.SetValue(this, Host.GetGeometryObjectFromReference(val3.Reference));
			}
		}
	}
}





