using System;
using System.Collections;
using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class ScriptCollection(int addArgumentCount, Func<IReadOnlyList<object?>> snapshot, Func<IReadOnlyList<object?>, object?> add, Func<int, bool> removeAt) : IList, IList<object?>
{
	public static readonly ScriptCollectionProperties PropertiesInstance = [];

	public object? this[int index]
	{
		get
		{
			var values = snapshot();
			return index >= 0 && index < values.Count ? values[index] : null;
		}
		set
		{
			// Collection entries are mutated through their script properties.
		}
	}

	public int    AddArgumentCount { get; } = addArgumentCount;
	public int    Count            => snapshot().Count;
	public bool   IsReadOnly       => false;
	public bool   IsFixedSize      => false;
	public bool   IsSynchronized   => false;
	public object SyncRoot         { get; } = new();

	public object? Add(IReadOnlyList<object?> arguments) => arguments.Count == AddArgumentCount ? add(arguments) : null;

	public void Clear()
	{
		for (var index = Count - 1; index >= 0; index--)
			removeAt(index);
	}

	public bool Contains(object? value) => IndexOf(value) >= 0;

	public int IndexOf(object? value)
	{
		var values = snapshot();
		for (var index = 0; index < values.Count; index++)
			if (Equals(values[index], value))
				return index;

		return -1;
	}

	public void CopyTo(Array array, int index)
	{
		foreach (var value in snapshot())
			array.SetValue(value, index++);
	}

	public void CopyTo(object?[] array, int arrayIndex)
	{
		foreach (var value in snapshot())
			array[arrayIndex++] = value;
	}

	public void RemoveAt(int index)
	{
		if (index >= 0 && index < Count)
			removeAt(index);
	}

	public bool TryRemoveAt(int index) => index >= 0 && index < Count && removeAt(index);

	public bool Remove(object? value)
	{
		var index = IndexOf(value);
		if (index < 0)
			return false;

		removeAt(index);
		return true;
	}

	void IList.Remove(object? value) => Remove(value);

	public IEnumerator GetEnumerator() => snapshot().GetEnumerator();

	IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => snapshot().GetEnumerator();

	int IList.Add(object? value) => throw new NotSupportedException("Use the script collection's parameterized add operation.");

	void ICollection<object?>.Add(object? item) => throw new NotSupportedException("Use the script collection's parameterized add operation.");

	public void Insert(int index, object? value) => throw new NotSupportedException("The script collection does not support insertion.");
}