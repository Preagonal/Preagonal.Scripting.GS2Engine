using System;
using System.Collections;
using System.Linq;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptUniverseProperties : ScriptProperties<ScriptUniverse>
{
	public ScriptUniverseProperties() : base(typeof(ScriptVariable))
	{
		AddFunctions(
			this,
			new()
			{
				{ "arcsin", "Returns the inverse sine in radians, or zero for an argument outside [-1, 1].", (_, args) => InverseTrig(args, false), [new("value", typeof(double))] },
				{ "arccos", "Returns the inverse cosine in radians, or zero for an argument outside [-1, 1].", (_, args) => InverseTrig(args, true), [new("value", typeof(double))] },
				{ "getascii", "Returns the first unsigned byte of the string, or zero for an empty string.", (_, args) => GetString(args).Length > 0 ? GetString(args).buffer[0] : 0, [new("text", typeof(TString))] },
				{
					"strcmp", "Compares strings without ASCII case sensitivity and returns a negative, zero, or positive result.", (_, args) => ScriptStringComparison.Compare(GetString(args), GetString(args, 1)),
					[new("left", typeof(TString)), new("right", typeof(TString))]
				},
				{
					"strequals", "Compares strings without ASCII case sensitivity after trimming surrounding space bytes.", (_, args) => ScriptStringComparison.Compare(GetString(args), GetString(args, 1), true) == 0,
					[new("left", typeof(TString)), new("right", typeof(TString))]
				},
				{ "format2", "Formats a string using an array of arguments instead of separate arguments.", (_, args) => FormatArray(args), [new("format", typeof(string)), new("arguments", typeof(object[]))] },
				{ "lowercase", "Returns the string converted to lower case.", (_, args) => GetString(args).ToString().ToLowerInvariant(), [new("text", typeof(TString))] },
				{ "uppercase", "Returns the string converted to upper case.", (_, args) => GetString(args).ToString().ToUpperInvariant(), [new("text", typeof(TString))] },
				{ "base64encode", "Encodes the string's bytes as Base64.", (_, args) => ScriptStringEncoding.EncodeBase64(GetString(args)), [new("text", typeof(TString))] },
				{ "base64decode", "Decodes Base64 bytes, stopping at padding or the first invalid character.", (_, args) => ScriptStringEncoding.DecodeBase64(GetString(args)), [new("text", typeof(TString))] },
				{ "md5", "Returns the lowercase hexadecimal MD5 digest of the string's bytes.", (_, args) => ScriptStringEncoding.Md5(GetString(args)), [new("text", typeof(TString))] },
				{ "isstringutf8", "Reports whether the byte string contains a multibyte UTF-8 sequence and no malformed sequences.", (_, args) => ScriptStringEncoding.IsUtf8(GetString(args)), [new("text", typeof(TString))] },
				{
					"contains", "Tests for a nonempty word using delimiters without ASCII case sensitivity.", (_, args) => ScriptStringComparison.Contains(GetString(args), GetString(args, 1)),
					[new("source", typeof(TString)), new("needle", typeof(TString))]
				},
			}
		);
		Compile();
	}

	private static TString GetString(IStackEntry[] args, int index = 0) => args.Length > index ? args[index].GetValue<TString>() ?? new TString() : new();

	private static double InverseTrig(IStackEntry[] args, bool cosine)
	{
		var value = args.Length > 0 ? args[0].GetValue<double>() : 0d;
		return value is >= -1d and <= 1d ? cosine ? Math.Acos(value) : Math.Asin(value) : 0d;
	}

	private static string FormatArray(IStackEntry[] args)
	{
		var values = args.Length > 1 && args[1].GetValue() is IEnumerable array and not string and not TString ? array.Cast<object?>().Select(value => value is IStackEntry entry ? entry.GetValue() : value).ToArray() : [];
		return Tools.Format(GetString(args).ToString(), values);
	}
}