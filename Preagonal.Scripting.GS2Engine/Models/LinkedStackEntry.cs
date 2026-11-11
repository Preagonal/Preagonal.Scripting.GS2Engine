using System.Collections.Generic;
using Preagonal.Scripting.GS2Engine.Enums;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class LinkedStackEntry(IStackEntry target) : IStackEntry
{
	public StackEntryType Type                                                            => target.Type;
	public object?        GetValue(bool forJson = false, HashSet<object>? visited = null) => target.GetValue(forJson, visited);
	public object?        GetParent()                                                     => target.GetParent();
	public T?             GetValue<T>()                                                   => target.GetValue<T>();
	public bool           TryGetValue<T>(out object? value)                               => target.TryGetValue<T>(out value);
	public void           SetValue(object? getValue, bool skipCallback = false)           => target.SetValue(getValue, skipCallback);
}