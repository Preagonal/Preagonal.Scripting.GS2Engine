using System;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

// Native getters and commands may synchronously call back into scripts for a return value.
// Such a callback cannot wait for a future host frame while its caller blocks that frame.
internal readonly struct SynchronousScriptCall : IDisposable
{
	private readonly ScriptExecutionScope? _scope;
	public SynchronousScriptCall()
	{
		_scope = ScriptExecutionScope.Current;
		if (_scope != null) _scope.SynchronousDepth++;
	}
	public void Dispose()
	{
		if (_scope != null) _scope.SynchronousDepth--;
	}
}
