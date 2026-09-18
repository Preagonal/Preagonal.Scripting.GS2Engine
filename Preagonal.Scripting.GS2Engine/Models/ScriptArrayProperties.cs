using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Preagonal.Scripting.GS2Engine.Extensions;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class ScriptArrayProperties : ScriptProperties<IList>
{
	public static readonly ScriptArrayProperties Instance = new();

	public ScriptArrayProperties() : base(null)
	{
		AddFunctions(
			this,
			new()
			{
				{ "addarray", "Appends every value from another array.", AddArray, [new("values", typeof(object[]))], typeof(void) },
				{ "indices", "Returns every index containing the specified value.", Indices, [new("value", typeof(object))] },
				{ "insertarray", "Inserts every value from another array at the specified index.", InsertArray, [new("index", typeof(int)), new("values", typeof(object[]))], typeof(void) },
			}
		);

		Compile();
	}

	private static object AddArray(IList target, IStackEntry[] args)
	{
		var values = GetValues(args.FirstOrDefault());
		if (values == null)
			return 0;

		var sourceCount = values.Count;
		for (var sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
			target.Add(Unwrap(values[sourceIndex]));

		return 0;
	}

	private static object Indices(IList target, IStackEntry[] args)
	{
		if (args.Length == 0)
			return Array.Empty<object?>();

		var value   = Unwrap(args[0].GetValue());
		var indices = new List<object?>();
		for (var index = 0; index < target.Count; index++)
		{
			if (ScriptValuesEqual(target[index], value))
				indices.Add((double)index);
		}

		return indices;
	}

	private static object InsertArray(IList target, IStackEntry[] args)
	{
		if (args.Length < 2)
			return 0;

		var index  = ToScriptInt(args[0].GetValue().ToScriptDouble());
		var values = GetValues(args[1]);
		if (values == null)
			return 0;

		var sourceCount = values.Count;
		for (var sourceIndex = sourceCount - 1; sourceIndex >= 0; sourceIndex--)
		{
			var value = Unwrap(values[sourceIndex]);
			if (index < 0 || target.Count < index)
				target.Add(value);
			else
				target.Insert(index, value);
		}

		return 0;
	}

	private static IList? GetValues(IStackEntry? entry)
	{
		var value = Unwrap(entry?.GetValue());
		if (value is IList list)
			return list;

		return value is IEnumerable enumerable and not string and not TString ? enumerable.Cast<object?>().Select(Unwrap).ToArray() : null;
	}

	private static bool ScriptValuesEqual(object? left, object? right)
	{
		left  = Unwrap(left);
		right = Unwrap(right);

		if (ReferenceEquals(left, right))
			return true;
		if (left == null || right == null)
			return false;
		if (IsNumeric(left) && IsNumeric(right))
			return Math.Abs(left.ToScriptDouble() - right.ToScriptDouble()) < 0.0001d;
		if (left is TString or string || right is TString or string)
			return string.Equals(Tools.ToScriptString(left), Tools.ToScriptString(right), StringComparison.Ordinal);

		return left.Equals(right);
	}

	private static object? Unwrap(object? value)
	{
		while (value is IStackEntry entry)
			value = entry.GetValue();

		return value;
	}

	private static bool IsNumeric(object value) => value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

	private static int ToScriptInt(double value)
	{
		var adjusted = value + 0.0001d;
		var result   = (int)adjusted;
		return adjusted < 0.0d && adjusted != result ? result - 1 : result;
	}
}