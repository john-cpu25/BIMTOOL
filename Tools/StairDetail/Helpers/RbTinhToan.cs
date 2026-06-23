using HuyAddin;
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;


namespace RincoNhan.Tools.StairDetail;

public static class RbTinhToan
{
	public const double Precision = 1E-05;

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

	public static bool PointInPlane(Plane mp, XYZ p)
	{
		XYZ val = GeomUtil.AddXYZ(p, mp.Normal);
		Curve input_cv = (Curve)(object)Line.CreateBound(p, val);
		return GeomUtil.KhoangCach(PlanIntersect(mp, input_cv), p) < 1E-05;
	}

	public static XYZ VectorVuongGoc(XYZ vt1, XYZ vt2)
	{
		return Plane.CreateByThreePoints(XYZ.Zero, vt1, vt2).Normal;
	}

	public static double GocCua2Vector(XYZ vector1, XYZ vector2)
	{
		XYZ vt = vector1.CrossProduct(vector2);
		double num = TichVoHuong(GeomUtil.AddXYZ(vector1, vector2), vt);
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

	public static void CopyParameter(Element rb, Element rbc, string ignore, List<string> ignore_group)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Expected I4, but got Unknown
		foreach (Parameter parameter in rb.Parameters)
		{
			Parameter val = parameter;
			if (((APIObject)val).IsReadOnly || !val.HasValue || ignore.Contains(val.Definition.Name) || (ignore_group.Count > 0 && ignore_group.Contains(ToGroupLabel(val.Definition.GetDataType()))))
			{
				continue;
			}
			Parameter val2 = null;
			val2 = ((!val.IsShared) ? rbc.get_Parameter(val.Definition) : rbc.get_Parameter(val.GUID));
			if (val2 == null || ((APIObject)val2).IsReadOnly)
			{
				continue;
			}
			StorageType storageType = val.StorageType;
			switch ((int)storageType)
			{
			case 1:
				val2.Set(val.AsInteger());
				break;
			case 2:
				val2.Set(val.AsDouble());
				break;
			case 3:
				val2.Set(val.AsString());
				break;
			case 4:
				try
				{
					val2.Set(val.AsElementId());
				}
				catch (Exception)
				{
				}
				break;
			}
		}
	}

	public static void CopyParameter(Element rb, Element rbc, string tocopy)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Expected O, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected I4, but got Unknown
		foreach (Parameter parameter in rb.Parameters)
		{
			Parameter val = parameter;
			if (((APIObject)val).IsReadOnly || !val.HasValue || !tocopy.Contains(val.Definition.Name))
			{
				continue;
			}
			Parameter val2 = null;
			val2 = ((!val.IsShared) ? rbc.get_Parameter(val.Definition) : rbc.get_Parameter(val.GUID));
			if (val2 == null || ((APIObject)val2).IsReadOnly)
			{
				continue;
			}
			StorageType storageType = val.StorageType;
			switch ((int)storageType)
			{
			case 1:
				val2.Set(val.AsInteger());
				break;
			case 2:
				val2.Set(val.AsDouble());
				break;
			case 3:
				val2.Set(val.AsString());
				break;
			case 4:
				try
				{
					val2.Set(val.AsElementId());
				}
				catch (Exception)
				{
				}
				break;
			}
		}
	}

	public static IList<IList<Curve>> ChiaDeu(IList<Curve> rb1, IList<Curve> rb2, int sl)
	{
		IList<IList<Curve>> list = new List<IList<Curve>>();
		if (rb1.Count != rb2.Count || sl < 2)
		{
			return null;
		}
		List<XYZ> list2 = new List<XYZ>();
		double num = 1.0 / (double)(sl - 1);
		XYZ endPoint = rb1[0].GetEndPoint(0);
		XYZ item = GeomUtil.MultiplyVector(GeomUtil.SubXYZ(rb2[0].GetEndPoint(0), endPoint), num);
		list2.Add(item);
		for (int i = 0; i < rb1.Count; i++)
		{
			XYZ endPoint2 = rb1[i].GetEndPoint(1);
			XYZ item2 = GeomUtil.MultiplyVector(GeomUtil.SubXYZ(rb2[i].GetEndPoint(1), endPoint2), num);
			list2.Add(item2);
		}
		for (int j = 0; j < sl; j++)
		{
			IList<Curve> list3 = new List<Curve>();
			XYZ val = GeomUtil.AddXYZ(rb1[0].GetEndPoint(0), GeomUtil.MultiplyVector(list2[0], (double)j));
			for (int k = 0; k < rb1.Count; k++)
			{
				XYZ val2 = GeomUtil.AddXYZ(rb1[k].GetEndPoint(1), GeomUtil.MultiplyVector(list2[k + 1], (double)j));
				list3.Add((Curve)(object)Line.CreateBound(val, val2));
				val = val2;
			}
			list.Add(list3);
		}
		return list;
	}

	public static void CopyConstraint(RebarConstraint r_copy, RebarConstraintsManager manager1, RebarConstrainedHandle hand_paste)
	{
		try
		{
// 			manager1.SetPreferredConstraint(r_copy);
			return;
		}
		catch (Exception)
		{
		}
		Transform val = Transform.CreateTranslation(XYZ.Zero);
		Face targetHostFaceAndTransform = r_copy.GetTargetHostFaceAndTransform(0, val);
		PlanarFace val2 = (PlanarFace)(object)((targetHostFaceAndTransform is PlanarFace) ? targetHostFaceAndTransform : null);
		foreach (RebarConstraint item in manager1.GetConstraintCandidatesForHandle(hand_paste, r_copy.GetTargetElement(0).Id))
		{
			if ((r_copy.IsToCover() && !item.IsToCover()) || (r_copy.IsFixedDistanceToHostFace() && !item.IsFixedDistanceToHostFace()))
			{
				continue;
			}
			for (int i = 0; i < item.NumberOfTargets; i++)
			{
				Face targetHostFaceAndTransform2 = item.GetTargetHostFaceAndTransform(i, val);
				Face obj = ((targetHostFaceAndTransform2 is PlanarFace) ? targetHostFaceAndTransform2 : null);
				bool flag = GeomUtil.IsSameDirection(((PlanarFace)obj).FaceNormal, val2.FaceNormal);
				bool flag2 = GeomUtil.KhoangCach(((PlanarFace)obj).Origin, val2.Origin) < 1E-05;
				if (flag && flag2)
				{
					if (item.IsFixedDistanceToHostFace())
					{
						item.SetDistanceToTargetHostFace(r_copy.GetDistanceToTargetHostFace());
					}
					if (item.IsToCover())
					{
						item.SetDistanceToTargetCover(r_copy.GetDistanceToTargetCover());
					}
// 					manager1.SetPreferredConstraint(item);
				}
			}
		}
	}

	public static IList<Curve> JoinLine(Curve cv1, Curve cv2)
	{
		List<double> list = new List<double>();
		IList<Curve> list2 = new List<Curve>();
		XYZ endPoint = cv1.GetEndPoint(0);
		XYZ endPoint2 = cv1.GetEndPoint(1);
		XYZ endPoint3 = cv2.GetEndPoint(0);
		XYZ endPoint4 = cv2.GetEndPoint(1);
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint4));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint4));
		double num = 99999.0;
		foreach (double item in list)
		{
			if (num > item)
			{
				num = item;
			}
		}
		if (num == 0.0)
		{
			return null;
		}
		if (list[0] == num)
		{
			list2.Add(cv1.CreateReversed());
			list2.Add((Curve)(object)Line.CreateBound(endPoint, endPoint3));
			list2.Add(cv2);
			return list2;
		}
		if (list[1] == num)
		{
			list2.Add(cv2);
			list2.Add((Curve)(object)Line.CreateBound(endPoint4, endPoint));
			list2.Add(cv1);
			return list2;
		}
		if (list[2] == num)
		{
			list2.Add(cv1);
			list2.Add((Curve)(object)Line.CreateBound(endPoint2, endPoint3));
			list2.Add(cv2);
			return list2;
		}
		if (list[3] == num)
		{
			list2.Add(cv1);
			list2.Add((Curve)(object)Line.CreateBound(endPoint2, endPoint4));
			list2.Add(cv2.CreateReversed());
			return list2;
		}
		return list2;
	}

	public static IList<Curve> JoinLine(Curve cv1, IList<Curve> cv2)
	{
		List<double> list = new List<double>();
		IList<Curve> list2 = new List<Curve>();
		XYZ endPoint = cv1.GetEndPoint(0);
		XYZ endPoint2 = cv1.GetEndPoint(1);
		XYZ endPoint3 = cv2[0].GetEndPoint(0);
		XYZ endPoint4 = cv2.Last().GetEndPoint(1);
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint4));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint4));
		double num = 99999.0;
		foreach (double item in list)
		{
			if (num > item)
			{
				num = item;
			}
		}
		if (num == 0.0)
		{
			return null;
		}
		if (list[0] == num)
		{
			list2.Add(cv1.CreateReversed());
			list2.Add((Curve)(object)Line.CreateBound(endPoint, endPoint3));
			{
				foreach (Curve item2 in cv2)
				{
					list2.Add(item2);
				}
				return list2;
			}
		}
		if (list[1] == num)
		{
			foreach (Curve item3 in cv2)
			{
				list2.Add(item3);
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint4, endPoint));
			list2.Add(cv1);
			return list2;
		}
		if (list[2] == num)
		{
			list2.Add(cv1);
			list2.Add((Curve)(object)Line.CreateBound(endPoint2, endPoint3));
			{
				foreach (Curve item4 in cv2)
				{
					list2.Add(item4);
				}
				return list2;
			}
		}
		if (list[3] == num)
		{
			foreach (Curve item5 in cv2)
			{
				list2.Add(item5);
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint4, endPoint2));
			list2.Add(cv1.CreateReversed());
			return list2;
		}
		return list2;
	}

	public static IList<Curve> JoinLine(IList<Curve> cv1, IList<Curve> cv2)
	{
		List<double> list = new List<double>();
		IList<Curve> list2 = new List<Curve>();
		XYZ endPoint = cv1[0].GetEndPoint(0);
		XYZ endPoint2 = cv1.Last().GetEndPoint(1);
		XYZ endPoint3 = cv2[0].GetEndPoint(0);
		XYZ endPoint4 = cv2.Last().GetEndPoint(1);
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint, endPoint4));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint3));
		list.Add(GeomUtil.KhoangCach(endPoint2, endPoint4));
		double num = 99999.0;
		foreach (double item in list)
		{
			if (num > item)
			{
				num = item;
			}
		}
		if (num == 0.0)
		{
			return null;
		}
		if (list[0] == num)
		{
			foreach (Curve item2 in cv1.Reverse())
			{
				list2.Add(item2.CreateReversed());
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint, endPoint3));
			{
				foreach (Curve item3 in cv2)
				{
					list2.Add(item3);
				}
				return list2;
			}
		}
		if (list[1] == num)
		{
			foreach (Curve item4 in cv2)
			{
				list2.Add(item4);
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint4, endPoint));
			{
				foreach (Curve item5 in cv1)
				{
					list2.Add(item5);
				}
				return list2;
			}
		}
		if (list[2] == num)
		{
			foreach (Curve item6 in cv1)
			{
				list2.Add(item6);
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint2, endPoint3));
			{
				foreach (Curve item7 in cv2)
				{
					list2.Add(item7);
				}
				return list2;
			}
		}
		if (list[3] == num)
		{
			foreach (Curve item8 in cv2)
			{
				list2.Add(item8);
			}
			list2.Add((Curve)(object)Line.CreateBound(endPoint4, endPoint2));
			{
				foreach (Curve item9 in cv1.Reverse())
				{
					list2.Add(item9.CreateReversed());
				}
				return list2;
			}
		}
		return list2;
	}

	public static XYZ Normal(IList<Curve> input)
	{
		return Plane.CreateByThreePoints(input[0].GetEndPoint(0), input[0].GetEndPoint(1), input[1].GetEndPoint(1)).Normal;
	}

	public static XYZ Normal(Curve cv1, Curve cv2)
	{
		return Plane.CreateByThreePoints(cv1.GetEndPoint(0), cv1.GetEndPoint(1), cv2.GetEndPoint(1)).Normal;
	}

	public static XYZ Normal(XYZ p1, XYZ p2)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		return Plane.CreateByThreePoints(new XYZ(), p1, p2).Normal;
	}

	public static List<Solid> AddCurvesAndSolids(GeometryElement geomElem)
	{
		List<Solid> list = new List<Solid>();
		foreach (GeometryObject item in geomElem)
		{
			GeometryInstance val = (GeometryInstance)(object)((item is GeometryInstance) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val)
			{
				list = AddCurvesAndSolids(val.GetInstanceGeometry());
			}
			Solid val2 = (Solid)(object)((item is Solid) ? item : null);
			if ((GeometryObject)null != (GeometryObject)(object)val2 && val2.Volume != 0.0)
			{
				list.Add(val2);
			}
		}
		return list;
	}

	public static List<Curve> DeviceToLine(Curve input, int number)
	{
		List<Curve> list = new List<Curve>();
		IList<XYZ> list2 = input.Tessellate();
		XYZ val = list2[0];
		XYZ val2 = list2[1];
		Line val3 = Line.CreateBound(list2[0], list2[1]);
		for (int i = 1; i < list2.Count - 1; i++)
		{
			XYZ direction = Line.CreateBound(list2[i], list2[i + 1]).Direction;
			if (Math.Abs(val3.Direction.X * direction.X + val3.Direction.Y * direction.Y + val3.Direction.Z * direction.Z) > Math.Cos(Math.PI / 180.0))
			{
				val2 = list2[i + 1];
			}
			else
			{
				list.Add((Curve)(object)val3);
				val = list2[i];
				val2 = list2[i + 1];
			}
			val3 = Line.CreateBound(val, val2);
		}
		list.Add((Curve)(object)val3);
		return SapXepThuTuTheoNguon(list, TopLength(list, number));
	}

	public static List<Curve> TopLength(List<Curve> input, int n)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003e: Expected O, but got Unknown
		List<Curve> list = new List<Curve>(n);
		if (input.Count <= n)
		{
			return input;
		}
		double num = 0.0032808398950131237;
		Line item = Line.CreateBound(new XYZ(), new XYZ(0.0, 0.0, num));
		for (int i = 0; i < n; i++)
		{
			list.Add((Curve)(object)item);
		}
		for (int j = 0; j < input.Count; j++)
		{
			for (int k = 0; k < n; k++)
			{
				if (input[j].Length > list[k].Length)
				{
					for (int num2 = n - 1; num2 > k; num2--)
					{
						list[num2] = list[num2 - 1];
					}
					list[k] = input[j];
					break;
				}
			}
		}
		return list;
	}

	private static List<Curve> SapXepThuTuTheoNguon(List<Curve> Nguon, List<Curve> CanSapXep)
	{
		List<Curve> list = new List<Curve>();
		foreach (Curve item in Nguon)
		{
			foreach (Curve item2 in CanSapXep)
			{
				if (item.Length == item2.Length)
				{
					list.Add(item);
					break;
				}
			}
		}
		return list;
	}

	public static Curve ExtendLine(Curve cv_input, double start, double end)
	{
		Line val = Line.CreateBound(cv_input.GetEndPoint(0), cv_input.GetEndPoint(1));
		XYZ obj = GeomUtil.AddXYZ(((Curve)val).GetEndPoint(0), GeomUtil.MultiplyVector(val.Direction.Negate(), start));
		XYZ val2 = GeomUtil.AddXYZ(((Curve)val).GetEndPoint(1), GeomUtil.MultiplyVector(val.Direction, end));
		return (Curve)(object)Line.CreateBound(obj, val2);
	}

	public static Curve HermiteSubArc(Curve hermite, XYZ start, XYZ end, bool tangent)
	{
		HermiteSpline val = (HermiteSpline)(object)((hermite is HermiteSpline) ? hermite : null);
		new List<XYZ>();
		List<double> list = new List<double>();
		List<double> list2 = new List<double>();
		for (int i = 0; i < val.ControlPoints.Count; i++)
		{
			if (tangent)
			{
				list.Add(TichVoHuong(GeomUtil.SubXYZ(val.ControlPoints[i], start).Normalize(), val.Tangents[i].Normalize()));
				list2.Add(TichVoHuong(GeomUtil.SubXYZ(val.ControlPoints[i], end).Normalize(), val.Tangents[i].Normalize()));
			}
			else
			{
				list.Add(GeomUtil.KhoangCach(val.ControlPoints[i], start));
				list2.Add(GeomUtil.KhoangCach(val.ControlPoints[i], end));
			}
		}
		int num = 0;
		int num2 = 0;
		if (tangent)
		{
			num = vitriMax(list);
			num2 = vitriMax(list2);
		}
		else
		{
			num = vitriMin(list);
			num2 = vitriMin(list2);
		}
		return (Curve)(object)HermiteSpline.Create(GetRange(val.ControlPoints, num, num2), false);
	}

	public static HermiteSpline HermiteSubArc_GanTrungVoiTiepTuyenNhat(Curve hermite, Curve start_cv, Curve end_cv)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		HermiteSpline val;
		try
		{
			val = (HermiteSpline)hermite;
		}
		catch (Exception)
		{
			val = ConvertToHermite(hermite);
		}
		Line val2 = Line.CreateBound(start_cv.GetEndPoint(0), start_cv.GetEndPoint(1));
		Line val3 = Line.CreateBound(end_cv.GetEndPoint(0), end_cv.GetEndPoint(1));
		new List<XYZ>();
		List<double> list = new List<double>();
		List<double> list2 = new List<double>();
		foreach (XYZ tangent in val.Tangents)
		{
			list.Add(TichVoHuong(tangent.Normalize(), val2.Direction));
			list2.Add(TichVoHuong(tangent.Normalize(), val3.Direction));
		}
		int start = vitriMax(list);
		int end = vitriMax(list2);
		return HermiteSpline.Create(GetRange(val.ControlPoints, start, end), false);
	}

	public static HermiteSpline SubArc_GanTrungVoiTiepTuyenNhat(Curve arc, Curve start_cv, Curve end_cv)
	{
		IList<XYZ> list = arc.Tessellate();
		IList<XYZ> list2 = new List<XYZ>();
		for (int i = 0; i < list.Count - 1; i++)
		{
			list2.Add(GeomUtil.SubXYZ(list[i + 1], list[i]));
		}
		list2.Add(list2[list2.Count - 1]);
		Line val = Line.CreateBound(start_cv.GetEndPoint(0), start_cv.GetEndPoint(1));
		Line val2 = Line.CreateBound(end_cv.GetEndPoint(0), end_cv.GetEndPoint(1));
		new List<XYZ>();
		List<double> list3 = new List<double>();
		List<double> list4 = new List<double>();
		foreach (XYZ item in list2)
		{
			list3.Add(TichVoHuong(item.Normalize(), val.Direction));
			list4.Add(TichVoHuong(item.Normalize(), val2.Direction));
		}
		int start = vitriMax(list3);
		int end = vitriMax(list4);
		return HermiteSpline.Create(GetRange(list, start, end), false);
	}

	public static Arc SubArc_GiaoDiem(Curve input, Curve start_cv, Curve end_cv)
	{
		Arc val = (Arc)(object)((input is Arc) ? input : null);
		if ((GeometryObject)(object)val == (GeometryObject)null)
		{
			return null;
		}
		XYZ center = val.Center;
		List<XYZ> list = GiaoDiem((Curve)(object)val, ExtendLine(start_cv, 1000.0, 1000.0));
		List<XYZ> list2 = GiaoDiem((Curve)(object)val, ExtendLine(end_cv, 1000.0, 1000.0));
		XYZ start = null;
		XYZ end = null;
		if (list != null && list2 != null)
		{
			if (list.Count == 1)
			{
				start = list[0];
			}
			if (list.Count == 2)
			{
				start = ((!(GeomUtil.KhoangCach(start_cv.GetEndPoint(0), list[0]) < GeomUtil.KhoangCach(start_cv.GetEndPoint(0), list[1]))) ? list[1] : list[0]);
			}
			if (list2.Count == 1)
			{
				end = list2[0];
			}
			if (list2.Count == 2)
			{
				end = ((!(GeomUtil.KhoangCach(end_cv.GetEndPoint(0), list2[0]) < GeomUtil.KhoangCach(end_cv.GetEndPoint(0), list2[1]))) ? list2[1] : list2[0]);
			}
		}
		else
		{
			XYZ val2 = ChieuVuongGoc(center, start_cv);
			Line cv = Line.CreateBound(center, val2);
			start = GiaoDiemLineExtend(start_cv, (Curve)(object)cv, 1000.0);
			XYZ val3 = ChieuVuongGoc(center, end_cv);
			Line cv2 = Line.CreateBound(center, val3);
			end = GiaoDiemLineExtend(end_cv, (Curve)(object)cv2, 1000.0);
		}
		return ArcFromStarEndCenter(start, end, center);
	}

	public static HermiteSpline HermiteSubArc_KhoangCachTiepTuyenMin(Curve hermite, Curve start_cv, Curve end_cv, out XYZ start_point, out XYZ end_point)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		HermiteSpline val;
		try
		{
			val = (HermiteSpline)hermite;
		}
		catch (Exception)
		{
			val = ConvertToHermite(hermite);
		}
		Curve val2 = ExtendLine((Curve)(object)Line.CreateBound(start_cv.GetEndPoint(0), start_cv.GetEndPoint(1)), 1000.0, 1000.0);
		Curve val3 = ExtendLine((Curve)(object)Line.CreateBound(end_cv.GetEndPoint(0), end_cv.GetEndPoint(1)), 1000.0, 1000.0);
		IList<XYZ> list = new List<XYZ>();
		List<XYZ> list2 = new List<XYZ>();
		List<XYZ> list3 = new List<XYZ>();
		List<double> list4 = new List<double>();
		List<double> list5 = new List<double>();
		for (int i = 0; i < val.ControlPoints.Count; i++)
		{
			XYZ val4 = val.ControlPoints[i];
			Curve obj = ExtendLine((Curve)(object)Line.CreateBound(val4, GeomUtil.AddXYZ(val4, val.Tangents[i])), 1000.0, 1000.0);
			XYZ val5 = TinhToan.GiaoDiem(obj, val2);
			list2.Add(val5);
			XYZ val6 = TinhToan.GiaoDiem(obj, val3);
			list3.Add(val6);
			if (val5 == null)
			{
				list4.Add(99999.0);
			}
			else
			{
				list4.Add(GeomUtil.KhoangCach(val5, val4));
			}
			if (val6 == null)
			{
				list5.Add(99999.0);
			}
			else
			{
				list5.Add(GeomUtil.KhoangCach(val6, val4));
			}
		}
		int num = vitriMin(list4);
		int num2 = vitriMin(list5);
		list = GetRange(val.ControlPoints, num, num2);
		start_point = list2[num];
		end_point = list3[num2];
		if (list == null)
		{
			list = GetRange(val.ControlPoints, num2, num);
		}
		return HermiteSpline.Create(list, false);
	}

	public static HermiteSpline SubArc_KhoangCachTiepTuyenMin(Curve arc, Curve start_cv, Curve end_cv, out XYZ start_point, out XYZ end_point)
	{
		IList<XYZ> list = arc.Tessellate();
		IList<XYZ> list2 = new List<XYZ>();
		for (int i = 0; i < list.Count - 1; i++)
		{
			list2.Add(GeomUtil.SubXYZ(list[i + 1], list[i]));
		}
		list2.Add(list2[list2.Count - 1]);
		Curve val = ExtendLine((Curve)(object)Line.CreateBound(start_cv.GetEndPoint(0), start_cv.GetEndPoint(1)), 1000.0, 1000.0);
		Curve val2 = ExtendLine((Curve)(object)Line.CreateBound(end_cv.GetEndPoint(0), end_cv.GetEndPoint(1)), 1000.0, 1000.0);
		new List<XYZ>();
		List<XYZ> list3 = new List<XYZ>();
		List<XYZ> list4 = new List<XYZ>();
		List<double> list5 = new List<double>();
		List<double> list6 = new List<double>();
		for (int j = 0; j < list.Count; j++)
		{
			XYZ val3 = list[j];
			Curve obj = ExtendLine((Curve)(object)Line.CreateBound(val3, GeomUtil.AddXYZ(val3, list2[j])), 1000.0, 1000.0);
			XYZ val4 = TinhToan.GiaoDiem(obj, val);
			list3.Add(val4);
			XYZ val5 = TinhToan.GiaoDiem(obj, val2);
			list4.Add(val5);
			if (val4 == null)
			{
				list5.Add(99999.0);
			}
			else
			{
				list5.Add(GeomUtil.KhoangCach(val4, val3));
			}
			if (val5 == null)
			{
				list6.Add(99999.0);
			}
			else
			{
				list6.Add(GeomUtil.KhoangCach(val5, val3));
			}
		}
		int num = vitriMin(list5);
		int num2 = vitriMin(list6);
		IList<XYZ> range = GetRange(list, num, num2);
		start_point = list3[num];
		end_point = list4[num2];
		return HermiteSpline.Create(range, false);
	}

	private static int vitriMin(List<double> khoangcach)
	{
		int result = 0;
		double num = 99999999.0;
		for (int i = 0; i < khoangcach.Count; i++)
		{
			if (khoangcach[i] < num)
			{
				result = i;
				num = khoangcach[i];
			}
		}
		return result;
	}

	private static int vitriMax(List<double> khoangcach)
	{
		int result = 0;
		double num = 0.0;
		for (int i = 0; i < khoangcach.Count; i++)
		{
			if (khoangcach[i] > num)
			{
				result = i;
				num = khoangcach[i];
			}
		}
		return result;
	}

	private static IList<XYZ> GetRange(IList<XYZ> input, int start, int end)
	{
		if (start < 0 || start > input.Count || end > input.Count || end < 0 || end < start)
		{
			return null;
		}
		IList<XYZ> list = new List<XYZ>();
		for (int i = start; i < input.Count; i++)
		{
			list.Add(input[i]);
			if (i > end)
			{
				break;
			}
		}
		return list;
	}

	public static double TichVoHuong(XYZ vt1, XYZ vt2)
	{
		return vt1.X * vt2.X + vt1.Y * vt2.Y + vt1.Z * vt2.Z;
	}

	public static double MaxDouble(List<double> input)
	{
		double num = 0.0;
		foreach (double item in input)
		{
			if (num < item)
			{
				num = item;
			}
		}
		return num;
	}

	public static double MaxDouble(double[] input)
	{
		double num = 0.0;
		foreach (double num2 in input)
		{
			if (num < num2)
			{
				num = num2;
			}
		}
		return num;
	}

	public static double MinDouble(double[] input)
	{
		double num = 99999999.0;
		foreach (double num2 in input)
		{
			if (num > num2)
			{
				num = num2;
			}
		}
		return num;
	}

	public static void ChiaDeu(Line input, int soluong_diem, double off_start, double off_end, ref List<XYZ> kq)
	{
		XYZ val = GeomUtil.AddXYZ(((Curve)input).GetEndPoint(0), GeomUtil.MultiplyVector(input.Direction.Negate(), off_start));
		XYZ val2 = GeomUtil.AddXYZ(((Curve)input).GetEndPoint(1), GeomUtil.MultiplyVector(input.Direction, off_end));
		input = Line.CreateBound(val, val2);
		double num = ((Curve)input).Length / (double)(soluong_diem - 1);
		kq.Add(val);
		if (soluong_diem > 1)
		{
			for (int i = 1; i < soluong_diem; i++)
			{
				kq.Add(GeomUtil.AddXYZ(val, GeomUtil.MultiplyVector(input.Direction, num * (double)i)));
			}
		}
	}

	public static int ChiaDeu(Line input, double length, double off_start, double off_end, ref List<XYZ> kq)
	{
		XYZ val = GeomUtil.AddXYZ(((Curve)input).GetEndPoint(0), GeomUtil.MultiplyVector(input.Direction.Negate(), off_start));
		XYZ val2 = GeomUtil.AddXYZ(((Curve)input).GetEndPoint(1), GeomUtil.MultiplyVector(input.Direction, off_end));
		input = Line.CreateBound(val, val2);
		double num = ((Curve)input).Length / length;
		int num2 = 0;
		num2 = ((((Curve)input).Length % length == 0.0) ? ((int)num + 1) : ((int)num + 2));
		length = ((Curve)input).Length / (double)(num2 - 1);
		kq.Add(val);
		for (int i = 1; i < num2; i++)
		{
			kq.Add(GeomUtil.AddXYZ(val, GeomUtil.MultiplyVector(input.Direction, length * (double)i)));
		}
		return num2;
	}

	public static XYZ GiaoDiemLineExtend(Curve cv1, Curve cv2, double extend_value)
	{
		Curve obj = ExtendLine(cv1, extend_value, extend_value);
		Curve val = ExtendLine(cv2, extend_value, extend_value);
		return TinhToan.GiaoDiem(obj, val);
	}

	public static Line ToLine(Curve input)
	{
		return Line.CreateBound(input.GetEndPoint(0), input.GetEndPoint(1));
	}

	public static Curve CungTronTiepXuc(XYZ Hermite_point, XYZ Hermite_tangent, Line Line_input)
	{
		if (TichVoHuong(Hermite_tangent.Normalize(), Line_input.Direction.Normalize()) < 0.0)
		{
			Curve obj = ((Curve)Line_input).CreateReversed();
			Line_input = (Line)(object)((obj is Line) ? obj : null);
		}
		Line val = Line.CreateBound(Hermite_point, GeomUtil.AddXYZ(Hermite_point, Hermite_tangent));
		XYZ val2 = GiaoDiemLineExtend((Curve)(object)val, (Curve)(object)Line_input, 1000.0);
		XYZ val3 = GeomUtil.AddXYZ(val2, GeomUtil.MultiplyVector(Line_input.Direction, GeomUtil.KhoangCach(val2, Hermite_point)));
		Plane mp = Plane.CreateByThreePoints(Hermite_point, ((Curve)Line_input).GetEndPoint(0), ((Curve)Line_input).GetEndPoint(1));
		Line cv = Line.CreateBound(Hermite_point, GeomUtil.AddXYZ(Hermite_point, NormalLineOnPlane(val, mp)));
		Line cv2 = Line.CreateBound(val3, GeomUtil.AddXYZ(val3, NormalLineOnPlane(Line_input, mp)));
		XYZ val4 = GiaoDiemLineExtend((Curve)(object)cv, (Curve)(object)cv2, 1000.0);
		XYZ val5 = GeomUtil.SubXYZ(Hermite_point, val4);
		XYZ val6 = GeomUtil.SubXYZ(val3, val4);
		XYZ val7 = GeomUtil.AddXYZ(val4, GeomUtil.MultiplyVector(GeomUtil.AddXYZ(val5, val6).Normalize(), val5.GetLength()));
		return (Curve)(object)Arc.Create(Hermite_point, val3, val7);
	}

	public static XYZ NormalLineOnPlane(Line input, Plane mp)
	{
		XYZ val = GeomUtil.AddXYZ(((Curve)input).GetEndPoint(0), mp.Normal);
		return Plane.CreateByThreePoints(((Curve)input).GetEndPoint(0), ((Curve)input).GetEndPoint(1), val).Normal;
	}

	private static IList<XYZ> Tessellate_Length(Curve input, double length, double off_start, double off_end)
	{
		List<XYZ> kq = new List<XYZ>();
		IList<XYZ> list = new List<XYZ>();
		IList<XYZ> list2 = input.Tessellate();
		kq.Add(list2[0]);
		for (int i = 1; i < list2.Count; i++)
		{
			Line val = Line.CreateBound(list2[i - 1], list2[i]);
			if (((Curve)val).Length > length)
			{
				ChiaDeu(val, length, 0.0, 0.0, ref kq);
			}
			else
			{
				kq.Add(list2[i]);
			}
		}
		List<XYZ> list3 = new List<XYZ>();
		foreach (XYZ item in kq)
		{
			list3.Add(input.Project(item).XYZPoint);
		}
		double num = 0.0;
		if (off_start == 0.0)
		{
			list.Add(list3[0]);
		}
		for (int j = 1; j < list3.Count; j++)
		{
			num += GeomUtil.KhoangCach(list3[j], list3[j - 1]);
			if (num >= Math.Abs(off_start) && num <= input.Length - Math.Abs(off_end))
			{
				list.Add(list3[j]);
			}
		}
		return list;
	}

	public static int ChiaDeuCurve(Curve cv_input, double length, double off_start, double off_end, ref List<XYZ> kq)
	{
		List<XYZ> kq2 = new List<XYZ>();
		int num = 1;
		IList<XYZ> list = Tessellate_Length(cv_input, length, off_start, off_end);
		if (off_start > 0.0)
		{
			double length2 = off_start / length;
			XYZ val = GeomUtil.SubXYZ(list[0], list[1]).Normalize();
			Curve obj = ((Curve)Line.CreateBound(list[0], GeomUtil.AddXYZ(list[0], GeomUtil.MultiplyVector(val, off_start)))).CreateReversed();
			ChiaDeu((Line)(object)((obj is Line) ? obj : null), length2, 0.0, 0.0, ref kq2);
		}
		foreach (XYZ item in list)
		{
			kq2.Add(item);
		}
		if (off_end > 0.0)
		{
			double length3 = off_end / length;
			XYZ val2 = GeomUtil.SubXYZ(list[list.Count - 1], list[list.Count - 2]).Normalize();
			ChiaDeu(Line.CreateBound(list[list.Count - 1], GeomUtil.AddXYZ(list[list.Count - 1], GeomUtil.MultiplyVector(val2, off_end))), length3, 0.0, 0.0, ref kq2);
		}
		if (kq2.Count == 0)
		{
			kq.Add(cv_input.GetEndPoint(0));
			return 1;
		}
		XYZ val3 = kq2[0];
		kq.Add(val3);
		for (int i = 2; i < kq2.Count; i++)
		{
			if (GeomUtil.KhoangCach(kq2[i], val3) > length)
			{
				val3 = kq2[i - 1];
				kq.Add(val3);
				num++;
			}
		}
		return num;
	}

	public static void ChiaDeuCurve(Curve cv_input, int soluong, double off_start, double off_end, ref List<XYZ> kq)
	{
		double length = 0.0032808398950131237;
		List<XYZ> kq2 = new List<XYZ>();
		ChiaDeuCurve(cv_input, length, off_start, off_end, ref kq2);
		if (soluong == 1)
		{
			kq.Add(kq2[0]);
			return;
		}
		int num = (kq2.Count - 1) / (soluong - 1);
		double num2 = (kq2.Count - 1 - num * (soluong - 1)) / soluong;
		for (int i = 0; i < soluong - 1; i++)
		{
			int index = (int)((double)i * num2) + i * num;
			kq.Add(kq2[index]);
		}
		kq.Add(kq2[kq2.Count - 1]);
	}

	public static ReferencePlane CreateRefPlane_by3Point(Document doc, XYZ start, XYZ end, XYZ cut_direction_point)
	{
		Plane mp = Plane.CreateByThreePoints(start, end, cut_direction_point);
		Line input = Line.CreateBound(start, end);
		return ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewReferencePlane(start, end, NormalLineOnPlane(input, mp), doc.ActiveView);
	}

	public static ReferencePlane CreateRefPlane(Document doc, Plane mp)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		XYZ origin = mp.Origin;
		XYZ val = new XYZ(0.0 - mp.Normal.Z, 0.0, mp.Normal.X);
		XYZ val2 = GeomUtil.AddXYZ(origin, val);
		Line input = Line.CreateBound(origin, val2);
		return ((Autodesk.Revit.Creation.ItemFactoryBase)doc.Create).NewReferencePlane(origin, val2, NormalLineOnPlane(input, mp), doc.ActiveView);
	}

	public static Element FindElementByName(Document doc, Type targetType, string targetName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(targetType)).FirstOrDefault((Func<Element, bool>)((Element e) => e.Name.Equals(targetName)));
	}

	public static HermiteSpline ConvertToHermite(Curve input)
	{
		double length = 0.0032808398950131237;
		List<XYZ> kq = new List<XYZ>();
		ChiaDeuCurve(input, length, 0.0, 0.0, ref kq);
		return HermiteSpline.Create((IList<XYZ>)kq, false);
	}

	public static List<XYZ> GiaoDiem(Curve cv1, Curve cv2)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Expected O, but got Unknown
		List<XYZ> list = new List<XYZ>();
		IntersectionResultArray val = default(IntersectionResultArray);
		if ((int)cv1.Intersect(cv2, out val) != 8)
		{
			if (cv1.IsBound && cv2.IsBound)
			{
				XYZ endPoint = cv1.GetEndPoint(0);
				XYZ endPoint2 = cv1.GetEndPoint(1);
				XYZ endPoint3 = cv2.GetEndPoint(0);
				XYZ endPoint4 = cv2.GetEndPoint(1);
				if (GeomUtil.IsEqual(endPoint, endPoint3) || GeomUtil.IsEqual(endPoint, endPoint4))
				{
					list.Add(endPoint);
				}
				if (GeomUtil.IsEqual(endPoint2, endPoint3) || GeomUtil.IsEqual(endPoint2, endPoint4))
				{
					list.Add(endPoint2);
				}
			}
			return null;
		}
		if (val.Size < 1)
		{
			return null;
		}
		foreach (IntersectionResult item in val)
		{
			IntersectionResult val2 = item;
			list.Add(val2.XYZPoint);
		}
		return list;
	}

	public static Arc ArcFromStarEndCenter(XYZ start, XYZ end, XYZ center)
	{
		XYZ obj = GeomUtil.SubXYZ(start, center);
		XYZ val = GeomUtil.SubXYZ(end, center);
		double length = obj.GetLength();
		XYZ val2 = GeomUtil.AddXYZ(obj, val).Normalize();
		XYZ val3 = GeomUtil.AddXYZ(center, GeomUtil.MultiplyVector(val2, length));
		return Arc.Create(start, end, val3);
	}

	public static XYZ ChieuVuongGoc(XYZ point, Curve curve)
	{
		Line val = Line.CreateBound(curve.GetEndPoint(0), curve.GetEndPoint(1));
		return PlanIntersect(Plane.CreateByNormalAndOrigin(val.Direction, point), (Curve)(object)val);
	}

	public static Curve TimEdgeCurveChung(Face face1, Face face2)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Expected O, but got Unknown
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		List<Curve> list = new List<Curve>();
		foreach (EdgeArray edgeLoop in face1.EdgeLoops)
		{
			foreach (Edge item in edgeLoop)
			{
				Edge val = item;
				list.Add(val.AsCurve());
			}
		}
		foreach (EdgeArray edgeLoop2 in face2.EdgeLoops)
		{
			foreach (Edge item2 in edgeLoop2)
			{
				Curve val2 = item2.AsCurve();
				foreach (Curve item3 in list)
				{
					if (CurveTrungNhau(val2, item3))
					{
						return val2;
					}
				}
			}
		}
		return null;
	}

	public static bool CurveTrungNhau(Curve cv1, Curve cv2)
	{
		bool num = GeomUtil.KhoangCach(cv1.GetEndPoint(0), cv2.GetEndPoint(0)) < 1E-05;
		bool flag = GeomUtil.KhoangCach(cv1.GetEndPoint(1), cv2.GetEndPoint(1)) < 1E-05;
		bool flag2 = GeomUtil.KhoangCach(cv1.GetEndPoint(0), cv2.GetEndPoint(1)) < 1E-05;
		bool flag3 = GeomUtil.KhoangCach(cv1.GetEndPoint(1), cv2.GetEndPoint(0)) < 1E-05;
		if ((num && flag) || (flag2 && flag3))
		{
			return true;
		}
		return false;
	}

	public static IList<Curve> RevertListCurveRebar(IList<Curve> l_raw_offset)
	{
		List<Curve> list = new List<Curve>();
		for (int num = l_raw_offset.Count - 1; num >= 0; num--)
		{
			list.Add(l_raw_offset[num].CreateReversed());
		}
		return list;
	}

	public static string ToGroupLabel(ForgeTypeId id)
	{
		if (id == (ForgeTypeId)null)
		{
			return string.Empty;
		}
		return LabelUtils.GetLabelForGroup(id);
	}
}







