using System;
using System.Collections.Generic;
using System.Linq;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptProperties<T> : HashSet<IScriptProperty>, IScriptProperties where T : class
{
	protected ScriptProperties(Type? parentType)
	{
		Type       = typeof(T);
		ParentType = parentType;

		var name = $"{Type.Name}";
		if (Type.IsGenericType)
			name += $"<{Type.GetGenericArguments()[0].Name}>";

		ScriptManager.GlobalProperties.TryAdd(name, this);
	}

	private Type               Type             { get; }
	public  Type?              ParentType       { get; }
	public  bool               Compiled         { get; private set; }
	public  IScriptProperties? ParentProperties { get; private set; }

	public void Compile()
	{
		if (Compiled) return;

		Compiled = true;

		ParentProperties = GetProperties(ParentType);
		ParentProperties?.Compile();

		if (ParentProperties == null) return;

		foreach (var prop in ParentProperties)
		{
			if (this.Any(existing => existing.ScriptPropertyType == prop.ScriptPropertyType && existing.PropertyName.Equals(prop.PropertyName, StringComparison.CurrentCultureIgnoreCase)))
			{
				continue;
			}

			base.Add(prop);
		}
	}

	private static IScriptProperties? GetProperties(Type? type)
	{
		if (type == null) return null;

		var name = $"{type.Name}";

		if (type.IsGenericType)
			name += $"<{type.GetGenericArguments()[0].Name}>";

		return ScriptManager.GlobalProperties.GetValueOrDefault(name);
	}

	public static void AddProperties(IScriptProperties? properties, PropertyDefinitions<T> definitions)
	{
		properties ??= ScriptUniverse.PropertiesInstance;

		foreach (var definition in definitions)
		{
			properties.Add(new ScriptProperty<T>(definition, properties));
		}
	}

	public static void AddFunctions(IScriptProperties? properties, FunctionDefinitions<T> definitions)
	{
		properties ??= ScriptUniverse.PropertiesInstance;

		foreach (var definition in definitions)
		{
			properties.Add(new ScriptProperty<T>(definition, properties));
		}
	}

	public bool TryGetProperty(string propertyName, out IScriptProperty property)
	{
		foreach (var candidate in this)
		{
			if (!candidate.PropertyName.Equals(propertyName, StringComparison.OrdinalIgnoreCase)) continue;

			property = candidate;
			return true;
		}

		property = null!;
		return false;
	}

	public new void Add(IScriptProperty scriptProperty)
	{
		RemoveWhere(existing => existing.ScriptPropertyType == scriptProperty.ScriptPropertyType && existing.PropertyName.Equals(scriptProperty.PropertyName, StringComparison.CurrentCultureIgnoreCase));
		base.Add(scriptProperty);
	}
}