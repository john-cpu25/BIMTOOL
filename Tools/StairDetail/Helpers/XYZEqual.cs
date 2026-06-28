
using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RincoNhan.Tools.StairDetail;

public class XYZEqual : IEqualityComparer<XYZ>
{
	public bool Equals(XYZ x, XYZ y)
	{
		return x.DistanceTo(y) < 1E-05;
	}

	public int GetHashCode(XYZ obj)
	{
		throw new NotImplementedException();
	}
}


