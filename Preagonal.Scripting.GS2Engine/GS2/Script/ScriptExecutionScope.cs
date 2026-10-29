using System;
using System.Threading;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

/// <summary>Shares a frame budget across a script invocation and its nested script calls.</summary>
public sealed class ScriptExecutionScope : IDisposable
{
	private static readonly AsyncLocal<ScriptExecutionScope?> Active = new();
	private readonly ScriptExecutionScope? _previous;
	private readonly Script _owner;
	private readonly int _generation;
	internal int SynchronousDepth;
	internal IScriptExecutionBudget Budget { get; }
	public static ScriptExecutionScope? Current => Active.Value;
	public bool CanSuspend => SynchronousDepth == 0;
	public bool IsCanceled => !_owner.ExecutionEnabled || _generation != _owner.EventGeneration;

	public ScriptExecutionScope(Script owner, IScriptExecutionBudget budget)
	{
		_owner = owner;
		_generation = owner.EventGeneration;
		Budget = budget;
		_previous = Active.Value;
		Active.Value = this;
	}

	public ScriptExecutionCheckpoint Checkpoint() => new(this, false, TimeSpan.Zero);
	public ScriptExecutionCheckpoint Delay(TimeSpan delay) => new(this, true, delay);
	public void Dispose() => Active.Value = _previous;

	internal void ThrowIfCanceled()
	{
		if (IsCanceled)
			throw new OperationCanceledException("The script was unloaded or replaced while suspended.");
	}
}
