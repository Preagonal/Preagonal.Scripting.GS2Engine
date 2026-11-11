using System.Collections.Generic;
using Preagonal.Scripting.GS2Engine.Enums;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IStackEntry
{
	public StackEntryType Type { get; }
	public object?        GetValue(bool forJson = false, HashSet<object>? visited = null);
	public object?        GetParent();
	public T?             GetValue<T>();
	bool                  TryGetValue<T>(out object? value);
	void                  SetValue(object? getValue, bool skipCallback = false);
}