using System;
using System.Globalization;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

public class StackEntry : IStackEntry
{
	internal StackEntry(StackEntryType type, object? value, object? parent = null)
	{
		Type   = type;
		Value  = value;
		Parent = parent;
	}

	private object?        Value       { get; set; }
	private object?        Parent      { get; set; }
	public  StackEntryType Type        { get; private set; }
	public  object?        GetValue()  => Value is LinkedStackEntry linkedValue ? linkedValue.GetValue() : Value;
	public  object?        GetParent() => Parent;

	public T1? GetValue<T1>()
	{
		if (TryGetValue<T1>(out var value))
		{
			return (T1?)value;
		}

		return default;
	}

	public bool TryGetValue<T>(out object? value)
	{
		try
		{
			var targetType   = typeof(T);
			var currentValue = GetValue();

			if (currentValue is null)
			{
				value = targetType == typeof(TString) ? (TString)string.Empty : targetType == typeof(string) ? string.Empty : default;
				return value is not null || default(T) is null;
			}

			if (targetType == typeof(object) || targetType.IsInstanceOfType(currentValue))
			{
				value = currentValue;
				return true;
			}

			if (targetType == typeof(TString))
			{
				value = (TString)Tools.ToScriptString(currentValue);
				return true;
			}

			if (targetType == typeof(string))
			{
				value = Tools.ToScriptString(currentValue);
				return true;
			}

			if (targetType == typeof(double))
			{
				value = currentValue switch
				{
					bool b                   => b ? 1.0d : 0.0d,
					TString t                => double.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0d,
					string s                 => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.0d,
					IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
					_                        => 0.0d,
				};
				return true;
			}

			if (targetType == typeof(bool))
			{
				value = currentValue switch
				{
					bool b                   => b,
					TString t                => double.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d != 0.0d : !string.IsNullOrEmpty(t.ToString()),
					string s                 => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d != 0.0d : !string.IsNullOrEmpty(s),
					IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture) != 0.0d,
					_                        => true,
				};
				return true;
			}

			if (currentValue is IConvertible)
			{
				value = Convert.ChangeType(currentValue, targetType, CultureInfo.InvariantCulture);
				return true;
			}

			value = default;
			return false;
		}
		catch (Exception e)
		{
			Tools.DebugLine(e.Message);
			value = default;
			return false;
		}
	}

	public void SetValue(object? value, bool skipCallback = false)
	{
		if (Value is LinkedStackEntry linkedValue && value is not LinkedStackEntry)
		{
			linkedValue.SetValue(value, skipCallback);
			Type = linkedValue.Type;
			return;
		}

		Value = value switch
		{
			string s => (TString)s,
			TString  => value,
			int      => Convert.ToDouble(value, CultureInfo.InvariantCulture),
			double   => (double)value,
			float    => Convert.ToDouble(value, CultureInfo.InvariantCulture),
			decimal  => Convert.ToDouble(value, CultureInfo.InvariantCulture),
			string[] => (string[])value,
			bool b   => b ? 1.0d : 0.0d,
			_        => value,
		};
		Type = value switch
		{
			ScriptCommand   => StackEntryType.Function,
			IScriptProperty => StackEntryType.ScriptProperty,
			Script          => StackEntryType.Script,
			string          => StackEntryType.String,
			TString         => StackEntryType.String,
			int             => StackEntryType.Number,
			double          => StackEntryType.Number,
			float           => StackEntryType.Number,
			decimal         => StackEntryType.Number,
			string[]        => StackEntryType.Array,
			bool            => StackEntryType.Number,
			_               => StackEntryType.Array,
		};

		/*
		if (SetterCallback != null && !skipCallback)
			SetterCallback(value);
			*/
	}
}