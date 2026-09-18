using System.Collections;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Extensions;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class ListCellStackEntry(IList list, int index) : IStackEntry
{
	private readonly IList _list = list;

	public StackEntryType Type => CurrentEntry.Type;

	public object? GetValue() => CurrentValue;

	public object? GetParent() => _list;

	public T? GetValue<T>() => CurrentEntry.GetValue<T>();

	public bool TryGetValue<T>(out object? value) => CurrentEntry.TryGetValue<T>(out value);

	public void SetValue(object? getValue, bool skipCallback = false)
	{
		if (!IsValid)
			return;

		_list[index] = getValue is IStackEntry entry ? entry.GetValue() : getValue;
	}

	private bool IsValid => index >= 0 && index < _list.Count;

	private object? CurrentValue => IsValid ? _list[index] is IStackEntry entry ? entry.GetValue() : _list[index] : 0.0d;

	private IStackEntry CurrentEntry => (CurrentValue ?? 0.0d).ToStackEntry();
}