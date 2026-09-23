using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

internal sealed record EventCatcher(string Handler, ScriptVariable? Sender = null);