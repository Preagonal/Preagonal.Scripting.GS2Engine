using System;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed record ScriptScheduledEvent(DateTime DueAt, string EventName, ScriptVariable Receiver, IStackEntry[] Arguments)
{
	internal bool IsQueued    { get; set; }
	internal bool IsCancelled { get; set; }
}