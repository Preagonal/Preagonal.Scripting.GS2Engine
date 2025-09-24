using System;
using System.Collections.Generic;
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

		Script.GlobalProperties.Add(name, this);
	}

	private Type               Type             { get; }
	private Type?              ParentType       { get; }
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
			base.Add(prop);
	}

	private static IScriptProperties? GetProperties(Type? type)
	{
		if (type == null) return null;

		var name = $"{type.Name}";

		if (type.IsGenericType)
			name += $"<{type.GetGenericArguments()[0].Name}>";

		return Script.GlobalProperties.GetValueOrDefault(name);
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

	public new void Add(IScriptProperty scriptProperty) => base.Add(scriptProperty);
}