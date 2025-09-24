using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public readonly struct PropertyDefinition<TInstance, TRet>(string propertyName, string description, PropertyReadDelegate<TRet, TInstance>? readTyped = null, PropertyWriteDelegate<TInstance, TRet>? writeTyped = null, PropertyType propertyType = PropertyType.Default) : IPropertyDefinition<TInstance>
{
	public  string                                  PropertyName { get; init; } = propertyName;
	public  string                                  Description  { get; init; } = description;
	private PropertyReadDelegate<TRet, TInstance>?  ReadTyped    { get; init; } = readTyped;
	private PropertyWriteDelegate<TInstance, TRet>? WriteTyped   { get; init; } = writeTyped;
	public  PropertyType                            PropertyType { get; init; } = propertyType;
	public  Type                                    ReturnType   => typeof(TRet);

	object? IPropertyDefinition<TInstance>.Read(TInstance instance)
		=> ReadTyped is null ? null : ReadTyped(instance);

	void IPropertyDefinition<TInstance>.Write(TInstance instance, object? value)
	{
		if (WriteTyped is null) return;
		// You can choose how strict you want this cast to be:
		// direct cast if you trust callers:
		//WriteTyped(instance, (TRet)value!);


		if (value != null && value.GetType() == typeof(TString) && typeof(TRet) == typeof(string))
			value = value.ToString();
		//else
		//	throw new ArgumentException($"Value is not convertible, {value?.GetType().FullName}", nameof(value));


		// or a safer conversion path:
		var v = value is TRet t ? t : (TRet)Convert.ChangeType(value!, typeof(TRet));
		WriteTyped(instance, v);

	}
}