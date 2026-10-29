using System;
using System.Runtime.CompilerServices;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public readonly struct ScriptExecutionCheckpoint(ScriptExecutionScope scope, bool delayRequested, TimeSpan delay) : ICriticalNotifyCompletion
{
	public ScriptExecutionCheckpoint GetAwaiter() => this;
	public bool IsCompleted => !scope.CanSuspend || (!delayRequested && !scope.Budget.IsExhausted);
	public void GetResult() => scope.ThrowIfCanceled();
	public void OnCompleted(Action continuation) => scope.Budget.Post(continuation, delay);
	public void UnsafeOnCompleted(Action continuation) => OnCompleted(continuation);
}
