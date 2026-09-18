using Preagonal.Scripting.GS2Engine.Enums;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class LinkedStackEntry(IStackEntry target) : IStackEntry
{
	private readonly IStackEntry _target = target;

	public StackEntryType Type => _target.Type;

	public object? GetValue() => _target.GetValue();

	public object? GetParent() => _target.GetParent();

	public T? GetValue<T>() => _target.GetValue<T>();

	public bool TryGetValue<T>(out object? value) => _target.TryGetValue<T>(out value);

	public void SetValue(object? getValue, bool skipCallback = false) => _target.SetValue(getValue, skipCallback);
}