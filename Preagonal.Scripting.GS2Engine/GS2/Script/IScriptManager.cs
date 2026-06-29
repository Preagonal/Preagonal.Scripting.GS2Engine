using System.Collections.Concurrent;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public interface IScriptManager
{
	void                                         RegisterGlobalObject(string name, ScriptVariable collection);
	void                                         RegisterGlobalVariable(string name, object? variable);
	ScriptVariable                               GlobalVariables  { get; }
	ConcurrentDictionary<string, ScriptVariable> GlobalObjects    { get; }
	ConcurrentDictionary<string, Script>         GlobalScripts    { get; }
}