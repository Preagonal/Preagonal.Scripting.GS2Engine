using System.Threading.Tasks;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.Extensions;

public static class ScriptCommandExtensions
{
	public static ValueTask<IStackEntry> InvokeAsync(this ScriptCommand? command, ScriptMachine machine, IStackEntry[]? arguments)
	{
		if (command?.Target is BoundScriptFunction function)
			return new(function.InvokeAsync(arguments));

		using var nativeCall = new SynchronousScriptCall();
		return new(command?.Invoke(machine, arguments) ?? 0.ToStackEntry());
	}
}
