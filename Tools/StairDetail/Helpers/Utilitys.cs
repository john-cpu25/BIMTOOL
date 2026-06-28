
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;


namespace RincoNhan.Tools.StairDetail;

public static class Utilitys
{
	public const double Precision = 1E-05;

	public static Element FindElementByName(Document doc, Type targetType, string targetName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(targetType)).FirstOrDefault((Func<Element, bool>)((Element e) => e.Name.Equals(targetName)));
	}

	public static IList<Element> FindListElementByName(Document doc, BuiltInCategory cateGory, string targetName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		FilteredElementCollector val = new FilteredElementCollector(doc);
		val.OfCategory(cateGory);
		val.WhereElementIsNotElementType();
		if (targetName != "")
		{
			return ((IEnumerable<Element>)val).Where((Element e) => e.Name.Equals(targetName)).ToList();
		}
		return val.ToElements();
	}

	public static IList<Element> FindListElementByName(Document doc, Type targetType, string targetName)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		FilteredElementCollector val = new FilteredElementCollector(doc);
		val.OfClass(targetType);
		val.WhereElementIsNotElementType();
		if (targetName != "")
		{
			return ((IEnumerable<Element>)val).Where((Element e) => e.Name.Equals(targetName)).ToList();
		}
		return val.ToElements();
	}

	public static BoundingBoxXYZ bbSecSion(XYZ center, XYZ direction, XYZ horizon, double Width, double Height, double Range)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		BoundingBoxXYZ val = new BoundingBoxXYZ();
		XYZ val2 = GeomUtil.AddXYZ(center, GeomUtil.MultiplyVector(direction.Normalize(), Range));
		val.Max = new XYZ(Height / 2.0, Width / 2.0, 0.0);
		val.Min = new XYZ((0.0 - Height) / 2.0, (0.0 - Width) / 2.0, 0.0 - Range);
		Transform val3 = Transform.CreateTranslation(val2);
		val3.BasisZ = direction;
		val3.BasisX = RbTinhToan.Normal(direction, horizon);
		val3.BasisY = RbTinhToan.Normal(val3.BasisZ, val3.BasisX);
		try
		{
			val.Transform = val3;
			return val;
		}
		catch (Exception)
		{
			throw;
		}
	}

	public static BoundingBoxXYZ bbSecSion(XYZ direc, double Width, double Height, double Range, XYZ center)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected O, but got Unknown
		BoundingBoxXYZ val = new BoundingBoxXYZ();
		double num = Width / 2.0;
		double x = direc.X;
		double y = direc.Y;
		val.Max = new XYZ(num, Height, 0.0);
		val.Min = new XYZ(0.0 - num, 0.0 - Height, 0.0 - Range);
		Transform val2 = new Transform(val.Transform);
		val2.Origin = center;
		val2.BasisX = new XYZ(x, y, 0.0).Normalize();
		val2.BasisY = new XYZ(0.0, 0.0, 1.0);
		val2.BasisZ = new XYZ(y, 0.0 - x, 0.0).Normalize();
		GeomUtil.AddXYZ(center, GeomUtil.MultiplyVector(new XYZ(direc.Y, 0.0 - direc.X, 0.0).Normalize(), Range));
		val.Transform = val2;
		return val;
	}
}



