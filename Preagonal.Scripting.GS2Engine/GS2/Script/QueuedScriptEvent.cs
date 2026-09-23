using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

internal sealed record QueuedScriptEvent(
	ScriptVariable Source,
	Script Target,
	int Generation,
	string Name,
	IStackEntry[] Arguments,
	ScriptVariable? Receiver,
	ScriptExecutionContext? Context
);