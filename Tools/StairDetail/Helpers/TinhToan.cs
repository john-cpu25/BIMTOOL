
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;


namespace RincoNhan.Tools.StairDetail;

public static partial class TinhToan
{
	public class ElementIdComparer : IEqualityComparer<ElementId>
	{
		public int GetHashCode(ElementId co)
		{
			if (co == (ElementId)null)
			{
				return 0;
			}
			return ((object)co).GetHashCode();
		}

		public bool Equals(ElementId x1, ElementId x2)
		{
			if (x1 == x2)
			{
				return true;
			}
			if (x1 == null || x2 == null)
			{
				return false;
			}
			return x1 == x2;
		}
	}

	public class ElementComparer : IEqualityComparer<Element>
	{
		public int GetHashCode(Element co)
		{
			return ((object)co)?.GetHashCode() ?? 0;
		}

		public bool Equals(Element x1, Element x2)
		{
			if (x1 == x2)
			{
				return true;
			}
			if (x1 == null || x2 == null)
			{
				return false;
			}
			return x1.Id == x2.Id;
		}
	}

	public const double Precision = 1E-05;

	public static bool BoundingBoxXyzContains(BoundingBoxXYZ bb, XYZ p)
	{
		XYZ val = GeomUtil.TransformPoint(p, bb.Transform.Inverse);
		bool num = bb.Min.X <= val.X || Math.Abs(bb.Min.X - val.X) < 1E-05;
		bool flag = bb.Max.X >= val.X || Math.Abs(bb.Max.X - val.X) < 1E-05;
		bool flag2 = bb.Min.Y <= val.Y || Math.Abs(bb.Min.Y - val.Y) < 1E-05;
		bool flag3 = bb.Max.Y >= val.Y || Math.Abs(bb.Max.Y - val.Y) < 1E-05;
		bool flag4 = bb.Min.Z <= val.Z || Math.Abs(bb.Min.Z - val.Z) < 1E-05;
		bool flag5 = bb.Max.Z >= val.Z || Math.Abs(bb.Max.Z - val.Z) < 1E-05;
		bool num2 = num && flag;
		bool flag6 = flag2 && flag3;
		bool flag7 = flag4 && flag5;
		return num2 && flag6 && flag7;
	}

	public static int Compare(XYZ p, XYZ q)
	{
		int num = Compare(p.X, q.X);
		if (num == 0)
		{
			num = Compare(p.Y, q.Y);
			if (num == 0)
			{
				num = Compare(p.Z, q.Z);
			}
		}
		return num;
	}

	public static int Compare(double a, double b)
	{
		if (!IsEqual(a, b))
		{
			if (!(a < b))
			{
				return 1;
			}
			return -1;
		}
		return 0;
	}

	public static bool IsZero(double a, double tolerance)
	{
		return tolerance > Math.Abs(a);
	}

	public static bool IsZero(double a)
	{
		return IsZero(a, 1E-05);
	}

	public static bool IsEqual(double a, double b)
	{
		return IsZero(b - a);
	}

	public static List<int> RealNumberPosition(Rebar rb)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<int> list = new List<int>();
		if ((int)rb.LayoutRule == 0)
		{
			list.Add(0);
			list.Add(0);
			return list;
		}
		if (rb.IncludeFirstBar)
		{
			list.Add(0);
		}
		else
		{
			list.Add(1);
		}
		if (rb.IncludeLastBar)
		{
			list.Add(rb.NumberOfBarPositions - 1);
		}
		else
		{
			list.Add(rb.NumberOfBarPositions - 2);
		}
		return list;
	}

	public static bool DiemThuocDuongThang(XYZ giaodiem, Line line)
	{
		XYZ endPoint = ((Curve)line).GetEndPoint(0);
		XYZ endPoint2 = ((Curve)line).GetEndPoint(1);
		XYZ val = GeomUtil.SubXYZ(giaodiem, endPoint);
		XYZ val2 = GeomUtil.SubXYZ(giaodiem, endPoint2);
		if (val.GetLength() < 1E-05 || val2.GetLength() < 1E-05 || GeomUtil.IsOppositeDirection(val, val2))
		{
			return true;
		}
		return false;
	}

	public static XYZ ProjectOnPlane(XYZ p, Plane mp)
	{
		XYZ val = GeomUtil.AddXYZ(p, mp.Normal);
		Curve input_cv = (Curve)(object)Line.CreateBound(p, val);
		return PlanIntersect(mp, input_cv);
	}

	public static double LamTron_mm(double input_inch, double lamtron_mm, bool lamtronlen)
	{
		int num = (int)(input_inch * 12.0 * 25.4 / lamtron_mm);
		if (lamtronlen)
		{
			num++;
		}
		return (double)num * lamtron_mm / 304.79999999999995;
	}

	public static double GocCua2Vector(XYZ vector1, XYZ vector2, XYZ huongZ)
	{
		XYZ val = vector1.CrossProduct(vector2);
		if (val.GetLength() == 0.0)
		{
			return Math.PI;
		}
		double num = GeomUtil.AddXYZ(vector1, vector2).DotProduct(val);
		double num2 = 0.0;
		double x = vector1.X;
		double y = vector1.Y;
		double z = vector1.Z;
		double x2 = vector2.X;
		double y2 = vector2.Y;
		double z2 = vector2.Z;
		double num3 = Math.Sqrt(x * x + y * y + z * z) * Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
		double num4 = (x * x2 + y * y2 + z * z2) / num3;
		double num5 = x * y2 - y * x2;
		double num6 = x * z2 - z * x2;
		double num7 = y * z2 - z * y2;
		double num8 = Math.Sqrt(num5 * num5 + num6 * num6 + num7 * num7) / num3;
		if (Math.Abs(num4) > 1E-05)
		{
			double d = num8 / num4;
			num2 = ((!(num4 >= 0.0)) ? (Math.Atan(d) + Math.PI) : Math.Atan(d));
		}
		else
		{
			num2 = Math.PI / 2.0;
		}
		if (!(num >= 0.0))
		{
			num2 = Math.PI * 2.0 - num2;
		}
		if (GeomUtil.IsOppositeDirection(val, huongZ))
		{
			num2 = 0.0 - num2;
		}
		return num2;
	}

	public static double KhoangCachDiemVsLine(XYZ point, Line line)
	{
		return GeomUtil.KhoangCach(PlanIntersect(Plane.CreateByNormalAndOrigin(line.Direction, point), (Curve)(object)line), point);
	}

	public static List<Solid> GetSolidByGeometry(GeometryElement geomElem, bool instance)
	{
		List<Solid> list = new List<Solid>();
		foreach (GeometryObject item in geomElem)
		{
			Solid val = (Solid)(object)((item is Solid) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val && val.Volume != 0.0)
			{
				list.Add(val);
				continue;
			}
			GeometryInstance val2 = (GeometryInstance)(object)((item is GeometryInstance) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val2)
			{
				GeometryElement geomElem2 = ((!instance) ? val2.GetSymbolGeometry() : val2.GetInstanceGeometry());
				list = GetSolidByGeometry(geomElem2, instance);
			}
		}
		return list;
	}

	public static IList<Curve> GetCurvesByGeometry(GeometryElement geomElem, bool instance = true)
	{
		IList<Curve> list = new List<Curve>();
		foreach (GeometryObject item in geomElem)
		{
			Curve val = (Curve)(object)((item is Curve) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val)
			{
				list.Add(val);
				continue;
			}
			GeometryInstance val2 = (GeometryInstance)(object)((item is GeometryInstance) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val2)
			{
				GeometryElement geomElem2 = ((!instance) ? val2.GetSymbolGeometry() : val2.GetInstanceGeometry());
				list = GetCurvesByGeometry(geomElem2, instance);
			}
		}
		return list;
	}

	public static XYZ Normal(IList<Curve> input)
	{
		Plane val = null;
		if (input.Count > 1)
		{
			val = Plane.CreateByThreePoints(input[0].GetEndPoint(0), input[0].GetEndPoint(1), input[1].GetEndPoint(1));
			return val.Normal;
		}
		val = ((input[0].GetEndPoint(0).X != input[0].GetEndPoint(1).X || input[0].GetEndPoint(0).Y != input[0].GetEndPoint(1).Y) ? Plane.CreateByThreePoints(input[0].GetEndPoint(0), input[0].GetEndPoint(1), input[0].GetEndPoint(1).Add(XYZ.BasisZ)) : Plane.CreateByThreePoints(input[0].GetEndPoint(0), input[0].GetEndPoint(1), input[0].GetEndPoint(1).Add(XYZ.BasisX)));
		return val.Normal;
	}

	public static Line ChieuFaceLenPlane(PlanarFace face, Plane mp_view, XYZEqual xYZEqual)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		List<XYZ> list = new List<XYZ>();
		foreach (EdgeArray edgeLoop in ((Face)face).EdgeLoops)
		{
			foreach (Edge item in edgeLoop)
			{
				XYZ val2 = HelperFixes.ProjectOnPlane(item.AsCurve().GetEndPoint(0), mp_view);
				if (!list.Contains(val2, xYZEqual))
				{
					list.Add(val2);
				}
				XYZ val3 = HelperFixes.ProjectOnPlane(item.AsCurve().GetEndPoint(1), mp_view);
				if (!list.Contains(val3, xYZEqual))
				{
					list.Add(val3);
				}
			}
		}
		List<XYZ> source = OrderPointOnLine(list, Line.CreateBound(list[0], list[1]).Direction, xYZEqual);
		return Line.CreateBound(source.First(), source.Last());
	}

	public static List<XYZ> OrderPointOnLine(List<XYZ> p_edge, XYZ direction, XYZEqual xYZEqual)
	{
		List<XYZ> list = new List<XYZ>();
		if (GeomUtil.IsSameDirection(Line.CreateBound(p_edge[0], p_edge[1]).Direction, direction))
		{
			list.Add(p_edge[0]);
			list.Add(p_edge[1]);
		}
		else
		{
			list.Add(p_edge[1]);
			list.Add(p_edge[0]);
		}
		if (p_edge.Count > 2)
		{
			for (int i = 2; i < p_edge.Count; i++)
			{
				XYZ val = p_edge[i];
				if (list.Contains(val, xYZEqual))
				{
					continue;
				}
				bool flag = false;
				XYZ val2 = XYZ.Zero;
				XYZ val3 = XYZ.Zero;
				for (int j = 0; j < list.Count - 1; j++)
				{
					val2 = val - list[j];
					val3 = list[j + 1] - list[j];
					if (GeomUtil.IsOppositeDirection(val2, val3))
					{
						list.Insert(j, val);
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					if (val2.GetLength() > val3.GetLength())
					{
						list.Add(val);
					}
					else
					{
						list.Insert(list.Count - 1, val);
					}
				}
			}
		}
		return list;
	}

	public static Element FindElementByName(Document doc, Type targetType, string targetName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(targetType)).FirstOrDefault((Func<Element, bool>)((Element e) => e.Name.Equals(targetName)));
	}

	public static XYZ PlanIntersect(Plane mp, Curve input_cv)
	{
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		XYZ val = null;
		Line obj = Line.CreateBound(input_cv.GetEndPoint(0), input_cv.GetEndPoint(1));
		XYZ direction = obj.Direction;
		XYZ normal = mp.Normal;
		double num = direction.X * normal.X + direction.Y * normal.Y + direction.Z * normal.Z;
		XYZ origin = mp.Origin;
		XYZ origin2 = obj.Origin;
		if (num != 0.0)
		{
			double num2 = (normal.X * (origin.X - origin2.X) + normal.Y * (origin.Y - origin2.Y) + normal.Z * (origin.Z - origin2.Z)) / num;
			return new XYZ(origin2.X + direction.X * num2, origin2.Y + direction.Y * num2, origin2.Z + direction.Z * num2);
		}
		return null;
	}

	public static Solid CreateSolidFromBoundingBox(BoundingBoxXYZ bbox)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		XYZ val = new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
		XYZ val2 = new XYZ(bbox.Max.X, bbox.Min.Y, bbox.Min.Z);
		XYZ val3 = new XYZ(bbox.Max.X, bbox.Max.Y, bbox.Min.Z);
		XYZ val4 = new XYZ(bbox.Min.X, bbox.Max.Y, bbox.Min.Z);
		Line item = Line.CreateBound(val, val2);
		Line item2 = Line.CreateBound(val2, val3);
		Line item3 = Line.CreateBound(val3, val4);
		Line item4 = Line.CreateBound(val4, val);
		List<Curve> obj = new List<Curve>
		{
			(Curve)(object)item,
			(Curve)(object)item2,
			(Curve)(object)item3,
			(Curve)(object)item4
		};
		double num = bbox.Max.Z - bbox.Min.Z;
		CurveLoop item5 = CurveLoop.Create((IList<Curve>)obj);
		return SolidUtils.CreateTransformed(GeometryCreationUtilities.CreateExtrusionGeometry((IList<CurveLoop>)new List<CurveLoop> { item5 }, XYZ.BasisZ, num), bbox.Transform);
	}

	public static bool CheckFaceInFace(Face f_union, Face f1)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		foreach (EdgeArray edgeLoop in f_union.EdgeLoops)
		{
			foreach (Edge item in edgeLoop)
			{
				foreach (XYZ item2 in item.AsCurve().Tessellate())
				{
					IntersectionResult val = f1.Project(item2);
					if (val == null)
					{
						return false;
					}
					if (GeomUtil.KhoangCach(item2, val.XYZPoint) > 1E-05)
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	public static Result DisableUpdaters(AddInId id, string name)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (name == "Detail Items" || name == "all")
			{
				// Rebar_DetailItem rebar_DetailItem = new Rebar_DetailItem(id);
				if (false)
				{
					// UpdaterRegistry.DisableUpdater...
				}
			}
			if (name == "Group Rebar" || name == "all")
			{
				// Update_GroupRebar update_GroupRebar = new Update_GroupRebar(id);
				if (false)
				{
					// UpdaterRegistry.DisableUpdater...
				}
			}
			UpdaterRegistry.GetRegisteredUpdaterInfos();
			return (Result)0;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return (Result)(-1);
		}
	}

	public static Result EnableUpdaters(AddInId id, string name)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (name == "Detail Items" || name == "all")
			{
				// Rebar_DetailItem rebar_DetailItem = new Rebar_DetailItem(id);
				if (false)
				{
					// UpdaterRegistry.EnableUpdater...
				}
			}
			if (name == "Group Rebar" || name == "all")
			{
				// Update_GroupRebar update_GroupRebar = new Update_GroupRebar(id);
				if (false)
				{
					// UpdaterRegistry.EnableUpdater...
				}
			}
			UpdaterRegistry.GetRegisteredUpdaterInfos();
			return (Result)0;
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return (Result)(-1);
		}
	}

	internal static IList<Curve> RevertListCurveRebar(IList<Curve> l_raw_offset)
	{
		List<Curve> list = new List<Curve>();
		for (int num = l_raw_offset.Count - 1; num >= 0; num--)
		{
			list.Add(l_raw_offset[num].CreateReversed());
		}
		return list;
	}

	internal static XYZ GetCenterOfFace(PlanarFace face)
	{
		BoundingBoxUV boundingBox = ((Face)face).GetBoundingBox();
		return ((Face)face).Evaluate((boundingBox.Max + boundingBox.Min) / 2.0);
	}

	public static Plane CreatePlane(PlanarFace upTop, double offset)
	{
		XYZ val = upTop.Origin + upTop.FaceNormal * offset;
		return Plane.CreateByNormalAndOrigin(upTop.FaceNormal, val);
	}

	internal static bool IsFaceHaveIntersect(SupportFace current, SupportFace next)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		foreach (EdgeArray edgeLoop in ((Face)next.Face).EdgeLoops)
		{
			foreach (Edge item in edgeLoop)
			{
				XYZ endPoint = item.AsCurve().GetEndPoint(0);
				if (((Face)current.Face).Project(endPoint) != null)
				{
					return true;
				}
			}
		}
		foreach (EdgeArray edgeLoop2 in ((Face)current.Face).EdgeLoops)
		{
			foreach (Edge item2 in edgeLoop2)
			{
				XYZ endPoint2 = item2.AsCurve().GetEndPoint(0);
				if (((Face)next.Face).Project(endPoint2) != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool PointInPlane(Plane mp, XYZ p)
	{
		XYZ val = GeomUtil.AddXYZ(p, mp.Normal);
		Curve input_cv = (Curve)(object)Line.CreateBound(p, val);
		return GeomUtil.KhoangCach(PlanIntersect(mp, input_cv), p) < 1E-05;
	}

	public static List<Solid> AddSolids(GeometryElement geoElement, bool instance = true)
	{
		List<Solid> list = new List<Solid>();
		foreach (GeometryObject item in geoElement)
		{
			Solid val = (Solid)(object)((item is Solid) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val && val.Volume > 0.0)
			{
				list.Add(val);
				continue;
			}
			GeometryInstance val2 = (GeometryInstance)(object)((item is GeometryInstance) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val2)
			{
				GeometryElement geoElement2 = ((!instance) ? val2.GetSymbolGeometry() : val2.GetInstanceGeometry());
				list.AddRange(AddSolids(geoElement2, instance));
			}
		}
		return list;
	}

	public static FamilyInstance DatFamily(Document doc, string familyname, XYZ diemdat)
	{
		Element obj = FindElementByName(doc, typeof(Family), familyname);
		Family val = (Family)(object)((obj is Family) ? obj : null);
		if (val == null)
		{
			string text = "C:\\IBIM_Tools\\Family\\" + familyname + ".rfa";
			try
			{
				doc.LoadFamily(text, out val);
			}
			catch (Exception)
			{
			}
			if (val == null)
			{
				text = "X:\\00. GENE_MANAGEMENT\\IBIM_Tools\\Family\\" + familyname + ".rfa";
				try
				{
					doc.LoadFamily(text, out val);
				}
				catch (Exception)
				{
				}
			}
		}
		if (val == null)
		{
			return null;
		}
		Element element = doc.GetElement(val.GetFamilySymbolIds().ElementAt(0));
		FamilySymbol val2 = (FamilySymbol)(object)((element is FamilySymbol) ? element : null);
		if (val2 == null)
		{
			return null;
		}
		val2.Activate();
		return ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewFamilyInstance(diemdat, val2, (StructuralType)1);
	}

	public static Level TimLevelPhuHop(List<Level> l_lv, XYZ origin, out double offset)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		List<Level> list = l_lv.OrderBy((Level o) => o.Elevation).ToList();
		Level val = list[0];
		double num = 0.0;
		ElementCategoryFilter val2 = new ElementCategoryFilter((BuiltInCategory)(-2001271));
		foreach (Element item in new FilteredElementCollector(((Element)val).Document).WherePasses((ElementFilter)(object)val2).ToElements())
		{
			num = item.get_Parameter((BuiltInParameter)(-1150193)).AsDouble();
		}
		offset = origin.Z - (list[0].Elevation + num);
		for (int i = 1; i < list.Count; i++)
		{
			double num2 = list[i].Elevation + num - origin.Z;
			if (num2 >= 0.0)
			{
				double num3 = Math.Abs(list[i - 1].Elevation + num - origin.Z);
				if (num2 > num3)
				{
					val = list[i - 1];
					offset = origin.Z - (list[i - 1].Elevation + num);
				}
				else
				{
					val = list[i];
					offset = origin.Z - (list[i].Elevation + num);
				}
				break;
			}
		}
		return val;
	}

	public static double GocCua2Vector(XYZ vector1, XYZ vector2)
	{
		XYZ val = vector1.CrossProduct(XYZ.BasisZ);
		double num = GeomUtil.AddXYZ(vector1, vector2).DotProduct(val);
		double num2 = 0.0;
		double x = vector1.X;
		double y = vector1.Y;
		double z = vector1.Z;
		double x2 = vector2.X;
		double y2 = vector2.Y;
		double z2 = vector2.Z;
		double num3 = Math.Sqrt(x * x + y * y + z * z) * Math.Sqrt(x2 * x2 + y2 * y2 + z2 * z2);
		double num4 = (x * x2 + y * y2 + z * z2) / num3;
		double num5 = x * y2 - y * x2;
		double num6 = x * z2 - z * x2;
		double num7 = y * z2 - z * y2;
		double num8 = Math.Sqrt(num5 * num5 + num6 * num6 + num7 * num7) / num3;
		if (Math.Abs(num4) > 1E-05)
		{
			double d = num8 / num4;
			num2 = ((!(num4 >= 0.0)) ? (Math.Atan(d) + Math.PI) : Math.Atan(d));
		}
		else
		{
			num2 = Math.PI / 2.0;
		}
		if (!(num >= 0.0))
		{
			num2 = Math.PI * 2.0 - num2;
		}
		return num2;
	}
}







