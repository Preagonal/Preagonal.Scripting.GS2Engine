using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public sealed class ScriptExecutionContext
{
	public ScriptVariable? Player       { get; init; }
	public ScriptVariable? PlayerObject { get; init; }
	public ScriptVariable? Level        { get; init; }
}