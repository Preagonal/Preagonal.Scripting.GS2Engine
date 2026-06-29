using System;
using System.Linq;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptVariable : VariableCollection, IScriptVariable
{
	public ScriptVariable(string name = "")
	{
		Name = name;
	}

	public string Name { get; protected set; }

	public static readonly ScriptVariableProperties PropertiesInstance = [];
	public virtual         IScriptProperties        Properties => PropertiesInstance;

	protected void SetCallback(string variable, CallbackDelegate setCallback)
	{
		Properties.FirstOrDefault(x => x.PropertyName.Equals(variable, StringComparison.CurrentCultureIgnoreCase))?.SetCallback(setCallback);
	}

	/*
	protected void GetCallback(TString variable, VariableCollectionGetCallback getCallback)
	{
		if (!ContainsVariable(variable))
			_collection.Add(variable, 0.ToStackEntry());

		_collection[variable].GetCallback(getCallback);
	}
	*/
}