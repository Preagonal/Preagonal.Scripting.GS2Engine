using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptManagerTests
{
	[Fact]
	public async Task New_named_static_variable_template_copies_values_without_aliasing_array_storage()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		var script = new Script(
			manager,
			"template-owner",
			Compile(
				"""
				function onCreated() {
					new TStaticVar(TileTemplate) { blocked = true; offsets = {{1, 2}, {3, 4}}; }
					this.copy = new TileTemplate();
					this.copy.blocked = false;
					this.copy.offsets[0][0] = 99;
					this.originalBlocked = TileTemplate.blocked;
					this.originalOffset = TileTemplate.offsets[0][0];
					this.copiedOffset = this.copy.offsets[0][0];
				}
				"""
			)
		);

		await script.Call("onCreated");

		Assert.Equal(1d, script.GetVariable("originalblocked").GetValue<double>());
		Assert.Equal(1d, script.GetVariable("originaloffset").GetValue<double>());
		Assert.Equal(99d, script.GetVariable("copiedoffset").GetValue<double>());
		var copy = script.GetVariable("copy").GetValue<ScriptVariable>();
		Assert.NotNull(copy);
		Assert.Same(script, copy.OwnerScript);
	}

	[Fact]
	public void Script_when_created_defaults_source_server_to_offline()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());

		var script = new Script(manager, ScriptType.Weapon);

		Assert.Equal("Offline", script.SourceServer);
	}

	[Fact]
	public void Script_when_loaded_from_disk_marks_source_server_as_offline()
	{
		var path = Path.GetTempFileName();
		try
		{
			var manager = new ScriptManager(new FakeLogger<ScriptManager>());

			var script = new Script(manager, path);

			Assert.Equal("Offline", script.SourceServer);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public async Task GetGlobalScripts_WhenGlobalVariablesChangeConcurrently_DoesNotThrow()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		var script  = new Script(manager, ScriptType.Weapon);
		manager.RegisterGlobalScript(script);

		using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
		var writer = Task.Run(
			() =>
			{
				var index = 0;
				// ReSharper disable once AccessToDisposedClosure
				while (!cancellation.IsCancellationRequested)
					manager.RegisterGlobalVariable($"global{index++}", index);
			},
			cancellation.Token
		);

		var exception = Record.Exception(() =>
			{
				// ReSharper disable once AccessToDisposedClosure
				while (!cancellation.IsCancellationRequested)
					_ = manager.GetGlobalScripts();
			}
		);

		await cancellation.CancelAsync();
		await writer;

		Assert.Null(exception);
	}

	[Fact]
	public void GetGlobalScripts_WhenOnlyGlobalVariablesChange_ReusesSnapshot()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		manager.RegisterGlobalScript(new(manager, ScriptType.Weapon));
		var scripts = manager.GetGlobalScripts();

		manager.RegisterGlobalVariable("unrelated", 1);

		Assert.Same(scripts, manager.GetGlobalScripts());
	}

	[Fact]
	public async Task RegisterGlobalScript_WhenScriptBytecodeUpdatesConcurrently_DoesNotThrow()
	{
		var manager      = new ScriptManager(new FakeLogger<ScriptManager>());
		var ownerScript  = new Script(manager, ScriptType.Weapon);
		var sourceScript = new Script(manager, ScriptType.Weapon);
		_ = new GuiControl("control", ownerScript);
		var bytecode = Compile(
			"""
			//#CLIENTSIDE
			function control.onAction() {
				return 1;
			}
			"""
		);
		var alternateBytecode = Compile(
			"""
			//#CLIENTSIDE
			function control.onResize() {
				return 2;
			}
			"""
		);

		using var                  cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
		ConcurrentQueue<Exception> exceptions   = [];
		var writer = Task.Run(() =>
			{
				var useAlternate = false;
				while (!cancellation.IsCancellationRequested)
				{
					try
					{
						sourceScript.UpdateFromByteCode("source", useAlternate ? alternateBytecode : bytecode);
						useAlternate = !useAlternate;
					}
					catch (Exception exception)
					{
						exceptions.Enqueue(exception);
					}
				}
			}
		);

		while (!cancellation.IsCancellationRequested)
		{
			try
			{
				manager.RegisterGlobalScript(sourceScript);
			}
			catch (Exception exception)
			{
				exceptions.Enqueue(exception);
			}
		}

		cancellation.Cancel();
		await writer;

		Assert.Empty(exceptions);
	}

	[Fact]
	public async Task UnregisterGlobalScript_when_script_created_profile_removes_profile()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		var script  = new Script(manager, "object-owner", Compile("function onCreated() { new GuiControlProfile(\"owned-profile\"); }"));
		await script.Call("onCreated");

		manager.UnregisterGlobalScript(script);

		Assert.False(manager.GlobalVariables.ContainsVariable("owned-profile"));
	}

	[Fact]
	public async Task Contextual_script_property_function_receives_executing_script()
	{
		Script? executingScript = null;
		ScriptProperties<ContextualFunctionTarget>.AddFunctions(
			null,
			new()
			{
				{
					"captureexecutingscript", "", (_, machine, _) =>
					{
						executingScript = machine.CurrentScript;
						return 0;
					}
				},
			}
		);
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		var script  = new Script(manager, "context-owner", Compile("function onCreated() { captureexecutingscript(); }"));

		await script.Call("onCreated");

		Assert.Same(script, executingScript);
	}

	[Fact]
	public void UnregisterGlobalScript_removes_object_event_catchers_from_other_scripts()
	{
		var callCount = 0;
		var manager   = new ScriptManager(new FakeLogger<ScriptManager>());
		manager.RegisterGlobalVariable(
			"markevent",
			(Script.Command)((_, _) =>
			{
				callCount++;
				return 0.ToStackEntry();
			})
		);
		var owner   = new Script(manager, ScriptType.Weapon);
		var control = new TriggerableGuiControl("control", owner);
		var source  = new Script(manager, "event-source", Compile("function control.onAction() { markevent(); }"));
		manager.RegisterGlobalScript(source);
		control.TriggerAction();

		manager.UnregisterGlobalScript(source);
		control.TriggerAction();

		Assert.Equal(1, callCount);
	}

	[Fact]
	public async Task CatchEvent_when_control_emits_event_calls_named_handler()
	{
		var callCount = 0;
		var manager   = new ScriptManager(new FakeLogger<ScriptManager>());
		manager.RegisterGlobalVariable(
			"markevent",
			(Script.Command)((_, _) =>
			{
				callCount++;
				return 0.ToStackEntry();
			})
		);
		var owner   = new Script(manager, ScriptType.Weapon);
		var control = new TriggerableGuiControl("control", owner);
		var source = new Script(
			manager,
			"event-source",
			Compile(
				"""
				function onCreated() {
					this.catchevent(control, "onAction", "handleAction");
				}

				function handleAction() {
					markevent();
				}
				"""
			)
		);

		await source.Call("onCreated");
		control.TriggerAction();

		Assert.Equal(1, callCount);
	}

	private static byte[] Compile(string scriptText)
	{
		var response = GS2Compiler.Interface.CompileCode(scriptText, "weapon", "test", withHeader: false);
		if (response.Success)
			return response.ByteCode;

		throw new($"Script failure: {response.ErrMsg}");
	}

	private sealed class ContextualFunctionTarget;

	private sealed class TriggerableGuiControl(string id, Script script) : GuiControl(id, script)
	{
		public void TriggerAction() => InvokeEvent("onAction");
	}
}