using System;
using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Extensions;

public static class StringExtensions
{
	public static IEnumerable<string> TokenizeForScript(this string value, string separators)
	{
		if (string.IsNullOrEmpty(value))
			return [];

		return string.IsNullOrEmpty(separators) ? value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) : value.Split(separators.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
	}

	public static IEnumerable<double> PositionsOf(this string value, string needle)
	{
		if (value.Length == 0 || needle.Length == 0 || needle.Length > value.Length)
			yield break;

		for (var offset = 0; offset <= value.Length - needle.Length; offset++)
		{
			if (string.CompareOrdinal(value, offset, needle, 0, needle.Length) == 0)
				yield return offset;
		}
	}
}