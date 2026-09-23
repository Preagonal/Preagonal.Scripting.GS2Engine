using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public delegate IStackEntry ScriptCommand(ScriptMachine machine, IStackEntry[]? args);