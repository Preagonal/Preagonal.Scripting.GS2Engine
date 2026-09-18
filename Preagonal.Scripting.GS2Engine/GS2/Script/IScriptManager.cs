using System.Collections.Generic;
using System;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public delegate ScriptVariable ScriptObjectCreator(string objectName, Script script);

public interface IScriptManager
{
	void                        RegisterGlobalObject(string name, ScriptVariable collection);
	void                        RegisterGlobalScript(Script script);
	void                        RegisterGlobalVariable(string name, object? variable);
	void                        RegisterObjectCreator(string typeName, ScriptObjectCreator creator);
	void                        RequestClassScript(string className);
	void                        SetClassScriptRequestHandler(Action<string>? handler);
	bool                        TryCreateObject(string typeName, string objectName, Script script, out ScriptVariable? createdObject);
	void                        UnregisterGlobalObject(string name, ScriptVariable collection);
	void                        UnregisterGlobalScript(Script script);
	IReadOnlyCollection<Script> GetGlobalScripts();
	ScriptVariable              GlobalVariables { get; }
}