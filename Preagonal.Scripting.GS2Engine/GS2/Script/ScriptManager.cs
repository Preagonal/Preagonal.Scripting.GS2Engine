using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptManager(ILogger<ScriptManager> logger) : IScriptManager
{
	protected readonly ILogger<ScriptManager>                       _logger     = logger;
	public static      Dictionary<string, IScriptProperties>        GlobalProperties { get; } = [];
	public             ScriptVariable                               GlobalVariables  { get; } = new();
	public             ConcurrentDictionary<string, ScriptVariable> GlobalObjects    { get; } = new();
	public             ConcurrentDictionary<string, Script>         GlobalScripts    { get; } = new();


	public void RegisterGlobalObject(string name, ScriptVariable collection) =>
		GlobalObjects[name] = collection;

	public void RegisterGlobalVariable(string name, object? variable) =>
		GlobalVariables.AddOrUpdate(name, variable.ToStackEntry());
}