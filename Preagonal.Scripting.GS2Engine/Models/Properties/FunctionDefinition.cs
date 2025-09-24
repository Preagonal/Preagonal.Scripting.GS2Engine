using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public readonly struct FunctionDefinition<TInstance, TRet>(string propertyName, string description, PropertyFunctionDelegate<TInstance, TRet>? callTyped = null) : IFunctionDefinition<TInstance>
{
	public  string                                     PropertyName { get; init; } = propertyName;
	public  string                                     Description  { get; init; } = description;
	private PropertyFunctionDelegate<TInstance, TRet>? CallTyped    { get; init; } = callTyped;
	public  Type                                       ReturnType   => typeof(TRet);

	object? IFunctionDefinition<TInstance>.Call(TInstance instance, params IStackEntry[] value)
	{
		if (CallTyped is null) return null;
		// You can choose how strict you want this cast to be:
		// direct cast if you trust callers:
		// WriteTyped(instance, (TRet)value!);

		// or a safer conversion path:
		var ret = CallTyped(instance, value);
		var v   = ret != null ? ret : (TRet)Convert.ChangeType(ret!, typeof(TRet));
		return v;
	}
}