using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptVariable(string name = "") : VariableCollection, IScriptVariable
{
	private readonly List<string> _joinedClasses = [];

	public              string                Name             { get; protected set; } = name;
	public              IReadOnlyList<string> JoinedClassNames => _joinedClasses;
	[JsonIgnore] public Script?               OwnerScript      { get; internal set; }

	public string JoinedClasses
	{
		get => string.Join(",", _joinedClasses);
		set
		{
			_joinedClasses.Clear();
			foreach (var className in value.TokenizeForScript(" ,"))
				Join(className);
		}
	}

	public static readonly      ScriptVariableProperties PropertiesInstance = [];
	[JsonIgnore] public virtual IScriptProperties        Properties => PropertiesInstance;

	public void Join(string className)
	{
		var normalizedClassName = className.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(normalizedClassName) || _joinedClasses.Contains(normalizedClassName)) return;

		_joinedClasses.Add(normalizedClassName);
	}

	public void Leave(string className)
	{
		var normalizedClassName = className.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(normalizedClassName) || !_joinedClasses.Contains(normalizedClassName)) return;

		_joinedClasses.Remove(normalizedClassName);
	}

	protected void SetCallback(string variable, CallbackDelegate setCallback)
	{
		if (Properties.TryGetProperty(variable, out var property))
			property.SetCallback(setCallback);
	}

	protected override IEnumerable<KeyValuePair<string, object?>> GetVariableDictionary() =>
	[
		.. Properties
		   .Where(p => !p.IsFunction)
		   .Select(scriptProperty => new KeyValuePair<string, object?>(scriptProperty.PropertyName, scriptProperty.Read(this).GetValueForJson())),
	];
}