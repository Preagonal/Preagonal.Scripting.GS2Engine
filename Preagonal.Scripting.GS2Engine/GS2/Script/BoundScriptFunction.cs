using System.Threading.Tasks;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

internal sealed class BoundScriptFunction(Script script, string name, ScriptVariable? receiver)
{
	public string Name { get; } = name;

	public Task<IStackEntry> InvokeAsync(IStackEntry[]? args) => script.CallEntries(Name, args, receiver);

	public IStackEntry Invoke(ScriptMachine machine, IStackEntry[]? args)
	{
		using var nativeCall = new SynchronousScriptCall();
		return InvokeAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
	}
}
