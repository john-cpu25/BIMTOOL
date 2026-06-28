
using System;
using System.Collections.Generic;

namespace RincoNhan.Tools.StairDetail;

public static class IListExtension
{
	public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
	{
		if (list == null)
		{
			throw new ArgumentNullException("list");
		}
		if (items == null)
		{
			throw new ArgumentNullException("items");
		}
		if (list is List<T> list2)
		{
			list2.AddRange(items);
			return;
		}
		foreach (T item in items)
		{
			list.Add(item);
		}
	}
}



