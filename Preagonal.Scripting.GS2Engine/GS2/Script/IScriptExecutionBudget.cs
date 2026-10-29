using System;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

/// <summary>Host-owned execution budget. Continuations must run on the host's script thread.</summary>
public interface IScriptExecutionBudget
{
	bool IsExhausted { get; }
	void Post(Action continuation, TimeSpan delay);
}
