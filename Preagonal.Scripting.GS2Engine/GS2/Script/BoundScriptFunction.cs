using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

internal sealed class BoundScriptFunction(Script script, string name, ScriptVariable? receiver)
{
	public string Name { get; } = name;

	public IStackEntry Invoke(ScriptMachine machine, IStackEntry[]? args) => script.CallEntries(Name, args, receiver).ConfigureAwait(false).GetAwaiter().GetResult();
}