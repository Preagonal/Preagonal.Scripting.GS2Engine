using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public sealed class ScriptTriggerTests
{
	private readonly ScriptManager _manager = new(new FakeLogger<ScriptManager>());

	[Fact]
	public async Task Trigger_does_not_interrupt_the_caller_and_preserves_argument_values_and_order()
	{
		var script = Compile(
			"""
			function onCreated() {
				this.trace = "before";
				temp.value = 7;
				this.result = this.trigger("Ping", temp.value, null, "last");
				temp.value = 99;
				this.trace @= ",after";
				this.trigger("onPing", 8, null, "end");
			}
			function onPing(temp.value, temp.empty, temp.last) {
				this.trace @= "," @ temp.value @ ":" @ (temp.empty == null) @ ":" @ temp.last;
				return 123;
			}
			"""
		);

		await script.Call("onCreated");
		Assert.Equal("before,after", script.GetVariable("trace").GetValue<string>());
		Assert.Equal(0d, script.GetVariable("result").GetValue<double>());
		await _manager.DispatchPendingEvents();
		Assert.Equal("before,after,7:1:last,8:1:end", script.GetVariable("trace").GetValue<string>());
	}

	[Fact]
	public async Task Trigger_on_a_script_uses_that_script_instead_of_its_owner()
	{
		var caller = Compile(
			"""
			function send() { target.trigger("Ping"); }
			function onPing() { this.called = true; }
			"""
		);
		var target = Compile("function onPing() { this.called = true; }", "target");
		_manager.RegisterObjectCreator("OwnedScript", (_, _) => target);
		_manager.TryCreateObject("OwnedScript", "", caller, out _);
		Assert.Same(caller, target.OwnerScript);

		await caller.Call("send");
		await _manager.DispatchPendingEvents();

		Assert.True(target.GetVariable("called").GetValue<bool>());
		Assert.False(caller.GetVariable("called").GetValue<bool>());
	}

	[Fact]
	public async Task Trigger_on_a_missing_named_object_does_not_trigger_the_caller()
	{
		var script = Compile(
			"""
			function onCreated() { this.calls++; "Staff/Panel".trigger("Created"); }
			"""
		);

		await script.Call("onCreated");
		await _manager.DispatchPendingEvents();
		Assert.Equal(1d, script.GetVariable("calls").GetValue<double>());
	}

	[Fact]
	public async Task Recursive_trigger_through_a_bound_function_runs_once_per_dispatch()
	{
		Compile(
			"""
			public function forward(temp.target) { temp.target.trigger("Tick"); }
			""",
			"bridge"
		);
		var script = Compile(
			"""
			function onCreated() { this.trigger("Tick"); }
			function onTick() { this.calls++; bridge.forward(this); }
			"""
		);

		await script.Call("onCreated");
		Assert.Equal(0d, script.GetVariable("calls").GetValue<double>());
		for (var i = 1; i <= 1000; i++)
		{
			await _manager.DispatchPendingEvents();
			Assert.Equal(i, script.GetVariable("calls").GetValue<double>());
		}
	}

	[Fact]
	public async Task Trigger_in_a_joined_class_keeps_the_original_receiver()
	{
		var joined = Compile(
			"""
			public function start() { this.trigger("Tick"); }
			function onTick() { this.calls++; }
			""",
			"counter"
		);
		var script = Compile("function onCreated() { this.start(); }");
		script.Join("counter");

		await script.Call("onCreated");
		await _manager.DispatchPendingEvents();

		Assert.Equal(1d, script.GetVariable("calls").GetValue<double>());
		Assert.Equal(0d, joined.GetVariable("calls").GetValue<double>());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Stale_triggers_are_discarded_after_halt_or_unregister(bool unregister)
	{
		var script = Compile(
			"""
			function onCreated() { this.trigger("Tick"); }
			function onTick() { this.calls++; }
			"""
		);
		await script.Call("onCreated");
		if (unregister)
		{
			_manager.UnregisterGlobalScript(script);
			_manager.RegisterGlobalScript(script);
		}
		else
		{
			script.HaltExecution();
			script.EnableExecution();
		}

		await _manager.DispatchPendingEvents();
		Assert.Equal(0d, script.GetVariable("calls").GetValue<double>());

		await script.Call("onCreated");
		await _manager.DispatchPendingEvents();
		Assert.Equal(1d, script.GetVariable("calls").GetValue<double>());
	}

	[Fact]
	public async Task Trigger_notifies_event_catchers_with_sender_and_arguments()
	{
		var sender = Compile("function send() { this.trigger(\"Ping\", 42); }", "sender");
		var listener = Compile(
			"""
			function onCreated() { this.catchevent(sender, "Ping", "received"); }
			function received(temp.sender, temp.value) { this.sender = temp.sender; this.value = temp.value; }
			""",
			"listener"
		);

		await listener.Call("onCreated");
		await sender.Call("send");
		Assert.Equal(0d, listener.GetVariable("value").GetValue<double>());
		await _manager.DispatchPendingEvents();
		Assert.Same(sender, listener.GetVariable("sender").GetValue());
		Assert.Equal(42d, listener.GetVariable("value").GetValue<double>());
	}

	[Fact]
	public async Task Trigger_retains_execution_context_and_restores_dispatcher_context()
	{
		var script = Compile(
			"""
			function send() { this.trigger("Ping"); }
			function onPing() { this.value = player; }
			function readPlayer() { return player; }
			"""
		);
		var player        = new ScriptVariable("player");
		var context       = new ScriptExecutionContext { Player = player };
		var defaultPlayer = new ScriptVariable("defaultplayer");
		_manager.RegisterGlobalVariable("player", defaultPlayer);

		await script.CallWithContext("send", context);
		Assert.Same(defaultPlayer, (await script.Call("readPlayer")).GetValue());
		await _manager.DispatchPendingEvents();
		Assert.Same(player, script.GetVariable("value").GetValue());
		Assert.Same(defaultPlayer, (await script.Call("readPlayer")).GetValue());
	}

	[Fact]
	public async Task Reentrant_dispatch_does_not_drain_events_raised_by_the_current_handler()
	{
		var script = Compile(
			"""
			function onCreated() { this.trigger("Tick"); }
			function onTick() { this.calls++; this.trigger("Tick"); dispatch(); }
			"""
		);
		_manager.RegisterGlobalVariable(
			"dispatch",
			(ScriptCommand)((_, _) =>
			{
				_manager.DispatchPendingEvents().GetAwaiter().GetResult();
				return 0.ToStackEntry();
			})
		);

		await script.Call("onCreated");
		await _manager.DispatchPendingEvents();
		Assert.Equal(1d, script.GetVariable("calls").GetValue<double>());
	}

	private Script Compile(string source, string name = "trigger-test")
	{
		var result = Interface.CompileCode(source, "weapon", name, withHeader: false);
		Assert.True(result.Success, result.ErrMsg);
		return new(_manager, name, result.ByteCode);
	}
}