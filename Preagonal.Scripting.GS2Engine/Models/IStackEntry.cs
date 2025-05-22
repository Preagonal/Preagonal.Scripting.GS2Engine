using Preagonal.Scripting.GS2Engine.Enums;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IStackEntry
{
	public StackEntryType Type { get; }
	public object?        GetValue();
	public T?             GetValue<T>();
	bool                  TryGetValue<T>(out object? value);
	void                  SetValue(object? getValue, bool skipCallback = false);
	void                  SetCallback(VariableCollection.VariableCollectionSetCallback setCallback);
	void                  GetCallback(VariableCollection.VariableCollectionGetCallback getCallback);
}