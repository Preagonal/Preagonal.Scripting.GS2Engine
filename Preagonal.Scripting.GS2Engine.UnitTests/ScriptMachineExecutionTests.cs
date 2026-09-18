using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptMachineExecutionTests
{
	[Fact]
	public async Task Given_nested_script_function_When_executed_Then_it_completes_without_waiting_on_itself()
	{
		var script = CompileScript(
			"""
			function addOne(value) {
				return value + 1;
			}

			function onCreated() {
				return addOne(41);
			}
			"""
		);

		var result = await script.Call("onCreated").WaitAsync(TimeSpan.FromSeconds(1));

		Assert.Equal(42, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_concurrent_top_level_calls_When_executed_Then_each_call_keeps_its_own_parameters()
	{
		var script = CompileScript(
			"""
			function echoAfterDelay(value) {
				sleep(0.02);
				return value;
			}
			"""
		);

		var first  = script.Call("echoAfterDelay", 1);
		var second = script.Call("echoAfterDelay", 2);

		var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(1));
		Assert.Equal([1d, 2d], results.Select(result => result.GetValue<double>()));
	}

	[Fact]
	public async Task Given_sleeping_event_When_timeout_executes_Then_timeout_does_not_wait_for_the_event()
	{
		var script = CompileScript(
			"""
			function slowEvent() {
				sleep(0.2);
			}

			function onTimeout() {
				return 42;
			}
			"""
		);

		var slowEvent = script.Call("slowEvent");
		await Task.Delay(10);

		var result = await script.Call("onTimeout").WaitAsync(TimeSpan.FromMilliseconds(100));
		Assert.Equal(42, result.GetValue<double>());
		await slowEvent;
	}

	private static Script CompileScript(string source)
	{
		var compilation = Interface.CompileCode(source, "weapon", "execution-test", withHeader: false);
		Assert.True(compilation.Success, compilation.ErrMsg);

		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		return new(manager, "execution-test", compilation.ByteCode);
	}
}