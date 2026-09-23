using System;

namespace Preagonal.Scripting.GS2Engine;

internal static class ScriptStringComparison
{
	public static bool Contains(TString source, TString needle)
	{
		if (needle.Length == 0) return false;
		const string delimiters     = " .,;:-_><|!\"\u00a7$%&/\\()=?`\u00b4{[]}+*~#''^";
		var          remainingStart = 0;
		for (var start = 0; start <= source.Length - needle.Length; start++)
		{
			var offset = 0;
			while (offset < needle.Length && Lower(source.buffer[start + offset]) == Lower(needle.buffer[offset])) offset++;
			if (offset != needle.Length) continue;
			var end = start + needle.Length;
			if ((start == remainingStart || delimiters.Contains((char)source.buffer[start - 1])) && (end == source.Length || delimiters.Contains((char)source.buffer[end]))) return true;
			// The desktop implementation consumes each rejected match before searching again.
			remainingStart = end;
			start          = end - 1;
		}

		return false;
	}

	public static int Compare(TString left, TString right, bool trim = false)
	{
		ReadOnlySpan<byte> first  = left.buffer.AsSpan(0, left.Length);
		ReadOnlySpan<byte> second = right.buffer.AsSpan(0, right.Length);
		if (trim)
		{
			first  = Trim(first);
			second = Trim(second);
		}

		for (var index = 0;; index++)
		{
			var a = index < first.Length ? Lower(first[index]) : 0;
			var b = index < second.Length ? Lower(second[index]) : 0;
			if (a != b || a == 0) return a - b;
		}
	}

	private static int Lower(byte value) => value is >= (byte)'A' and <= (byte)'Z' ? value + 32 : value;

	private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
	{
		while (!value.IsEmpty && (value[0] & 0x7f) == 0x20) value  = value[1..];
		while (!value.IsEmpty && (value[^1] & 0x7f) == 0x20) value = value[..^1];
		return value;
	}
}