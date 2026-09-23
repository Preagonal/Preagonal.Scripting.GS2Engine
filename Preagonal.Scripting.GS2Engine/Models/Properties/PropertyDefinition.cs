using System;
using System.Globalization;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public readonly struct PropertyDefinition<TInstance, TRet>(
	string propertyName,
	string description,
	PropertyReadDelegate<TRet, TInstance>? readTyped = null,
	PropertyWriteDelegate<TInstance, TRet>? writeTyped = null,
	PropertyType propertyType = PropertyType.Default
) : IPropertyDefinition<TInstance>
{
	public  string                                  PropertyName { get; init; } = propertyName;
	public  string                                  Description  { get; init; } = description;
	private PropertyReadDelegate<TRet, TInstance>?  ReadTyped    { get; init; } = readTyped;
	private PropertyWriteDelegate<TInstance, TRet>? WriteTyped   { get; init; } = writeTyped;
	public  PropertyType                            PropertyType { get; init; } = propertyType;
	public  Type                                    ReturnType   => typeof(TRet);

	Func<TInstance, object?>? IPropertyDefinition<TInstance>.Read
	{
		get
		{
			var readTyped = ReadTyped;
			return readTyped is null ? null : instance => readTyped(instance);
		}
	}

	Action<TInstance, object?>? IPropertyDefinition<TInstance>.Write
	{
		get
		{
			var writeTyped = WriteTyped;
			return writeTyped is null ? null : (instance, value) => writeTyped(instance, ConvertValue(value));
		}
	}

	private static TRet ConvertValue(object? value)
	{
		var targetType = typeof(TRet);
		if (value is IStackEntry stackEntry)
			value = stackEntry.GetValue();

		if (value is TRet typed)
			return typed;

		if (targetType == typeof(object))
			return (TRet)value!;

		if (targetType == typeof(string))
			return (TRet)(object)Tools.ToScriptString(value);

		if (targetType == typeof(TString))
			return (TRet)(object)(TString)Tools.ToScriptString(value);

		if (targetType == typeof(bool))
			return (TRet)(object)ToScriptBool(value);

		if (targetType == typeof(double))
			return (TRet)(object)ToScriptDouble(value);

		if (targetType == typeof(int))
			return (TRet)(object)(int)ToScriptDouble(value);

		if (targetType == typeof(string[]))
			return (TRet)(object)Tools.ToScriptString(value).Split(',');

		if (targetType.IsArray && value is Array array && targetType.GetElementType() is { } elementType)
		{
			var converted = Array.CreateInstance(elementType, array.Length);
			for (var index = 0; index < array.Length; index++)
				converted.SetValue(Convert.ChangeType(array.GetValue(index), elementType, CultureInfo.InvariantCulture), index);
			return (TRet)(object)converted;
		}

		return (TRet)Convert.ChangeType(value!, targetType, CultureInfo.InvariantCulture);
	}

	private static bool ToScriptBool(object? value) =>
		value switch
		{
			null                     => false,
			bool b                   => b,
			TString t                => ToScriptBool(t.ToString()),
			string s                 => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? Math.Abs(d) > double.Epsilon : !string.IsNullOrEmpty(s),
			IConvertible convertible => Math.Abs(Convert.ToDouble(convertible, CultureInfo.InvariantCulture)) > double.Epsilon,
			_                        => true
		};

	private static double ToScriptDouble(object? value) =>
		value switch
		{
			null                     => 0.0d,
			bool b                   => b ? 1.0d : 0.0d,
			TString t                => ToScriptDouble(t.ToString()),
			string s                 => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0d,
			IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
			_                        => 0.0d
		};
}