using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.ByteCode;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;
using Preagonal.Scripting.GS2Engine.UnitTests.Objects;
using Xunit.Abstractions;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptMachineTests
{
	private          int                     _calledTimes;
	private readonly Dictionary<int, string> _receivedStrings = new();
	private readonly ScriptManager           _scriptManager;

	public ScriptMachineTests(ITestOutputHelper testOutputHelper)
	{
		_scriptManager = new(new FakeLogger<ScriptManager>(_ => { }));

		Tools.SetDebugFuncWrite(testOutputHelper.WriteLine);
		Tools.SetDebugFuncWriteLine(testOutputHelper.WriteLine);
		Tools.DEBUG_ON = true;

		ConcurrentDictionary<int, Drawing> drawings = new();
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { "screenwidth", "The width of the game screen", _ => 1024 }, { "screenheight", "The height of the game screen", _ => 1024 }, });

		ScriptProperties<ScriptMachineTests>.AddFunctions(
			null,
			new()
			{
				{ "echo", "", EchoCallback },
				{
					"showimg", "", (_, args) =>
					{
						if (!(args?.Length > 3)) return 0;
						try
						{
							var     index = (int)args[0]!.GetValue<double>();
							string? image = args[1]?.GetValue<TString>() ?? string.Empty;

							var x = (int)args[2]!.GetValue<double>();
							var y = (int)args[3]!.GetValue<double>();
							if (drawings.TryGetValue(index, out var value))
							{
								value.ShowImg(image, x, y);
							}
							else
							{
								value = new(image, x, y);
								drawings.AddOrUpdate(index, value, (_, _) => value);
							}
						}
						catch (Exception)
						{
							//_logger.LogDebug(e.Message);
						}

						return 0;
					}
				},
				{
					"findimg", "", (_, args) =>
					{
						if (!(args?.Length > 0)) return null;
						try
						{
							var index = (int)args[0]!.GetValue<double>();

							if (drawings.TryGetValue(index, out var value))
							{
								return value;
							}
						}
						catch (Exception)
						{
							//_logger.LogDebug(e.Message);
						}

						return null;
					}
				},
				{
					"getimgwidth", "", (_, args) =>
					{
						if (!(args?.Length > 0)) return 0;
						try
						{
							var image = args[0]!.GetValue<TString>();

							if (image != null) Console.WriteLine(image);

							return 1;
						}
						catch (Exception)
						{
							//_logger.LogDebug(e.Message);
						}

						return 0;
					}
				},
			}
		);

		foreach (var property in ScriptManager.GlobalProperties.Where(x => !x.Value.Compiled))
		{
			property.Value.Compile();
		}
	}

	private int EchoCallback(ScriptMachineTests _, IStackEntry[] args)
	{
		_receivedStrings[_calledTimes] = args[0]?.GetValue()?.ToString() ?? "";

		Console.WriteLine(_receivedStrings[_calledTimes]);

		_calledTimes++;

		return 0;
	}

	private Script CompileScript(string scriptText, string scriptName = "testScript", ScriptVariable? refObject = null, ScriptGrammar grammar = ScriptGrammar.GS2)
	{
		var response = Interface.CompileCode(scriptText, "weapon", scriptName, withHeader: false, grammar: grammar);

		if (response.Success)
		{
			// Arrange
			return new(_scriptManager, scriptName, response.ByteCode, refObject);
		}

		throw new($"Script failure: {response.ErrMsg}");
	}

	private Script CompileLegacyBytecodeScript(string objectPath, string scriptName = "legacyScript") => new(_scriptManager, scriptName, CreateObjFromStrBytecode(objectPath));

	[Fact]
	public async Task Given_top_level_statements_When_executing_script_Then_bytecode_runs_from_start_to_end()
	{
		var captured = string.Empty;
		_scriptManager.RegisterGlobalVariable(
			"captureconsole",
			(ScriptCommand)((_, args) =>
			{
				captured = args?.FirstOrDefault()?.GetValue()?.ToString() ?? string.Empty;
				return 0.ToStackEntry();
			})
		);
		var script = CompileScript("captureconsole(\"executed\");");

		await script.ExecuteScript();

		Assert.Equal("executed", captured);
	}

	[Fact]
	public async Task Given_system_version_When_echoed_Then_full_version_is_used()
	{
		_scriptManager.RegisterGlobalVariable("testversion", new Version(1, 2, 3, 4));
		var script = CompileScript("function onCreated() { echo(testversion); }");

		await script.Call("onCreated");

		Assert.Equal("1.2.3.4", _receivedStrings[0]);
	}

	[Theory]
	[InlineData("major", 1d)]
	[InlineData("minor", 2d)]
	[InlineData("build", 3d)]
	[InlineData("revision", 4d)]
	public async Task Given_system_version_When_member_is_read_Then_component_is_returned(string member, double expected)
	{
		_scriptManager.RegisterGlobalVariable("testversion", new Version(1, 2, 3, 4));
		var script = CompileScript($"function onCreated() {{ return testversion.{member}; }}");

		var result = await script.Call("onCreated");

		Assert.Equal(expected, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_bare_receiver_property_When_reading_Then_property_getter_is_used()
	{
		var response = Interface.CompileCode("function onCreated() { return position; }", "levelnpc", "receiver", withHeader: false);
		Assert.True(response.Success, response.ErrMsg);
		var script = new ReceiverPropertyScript(_scriptManager, response.ByteCode) { Position = 42 };

		var result = await script.Call("onCreated");

		Assert.Equal(42, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_bare_receiver_property_When_assigning_Then_property_setter_is_used()
	{
		var response = Interface.CompileCode("function onCreated() { position = 42; }", "levelnpc", "receiver", withHeader: false);
		Assert.True(response.Success, response.ErrMsg);
		var script = new ReceiverPropertyScript(_scriptManager, response.ByteCode);

		await script.Call("onCreated");

		Assert.Equal(42, script.Position);
	}

	[Fact]
	public async Task Given_undeclared_bare_variable_When_assigned_Then_value_is_global()
	{
		var writer = CompileScript("function onCreated() { SharedState = 42; }");
		var reader = CompileScript("function onCreated() { return SharedState; }");

		await writer.Call("onCreated");
		var result = await reader.Call("onCreated");

		Assert.Equal(42, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_undeclared_bare_member_When_assigned_Then_parent_is_global()
	{
		var writer = CompileScript("function onCreated() { SharedState.value = 42; }");
		var reader = CompileScript("function onCreated() { return SharedState.value; }");

		await writer.Call("onCreated");
		var result = await reader.Call("onCreated");

		Assert.Equal(42, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_this_variable_When_bare_name_is_read_Then_script_value_is_not_used()
	{
		var script = CompileScript("function onCreated() { this.ScriptState = 42; return ScriptState; }");

		var result = await script.Call("onCreated");

		Assert.Equal(0, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_script_scoped_fade_state_When_timeout_runs_Then_state_advances_and_timer_is_rearmed()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				this.alphachange = 0.00625;
				this.alpha = 0;
			}

			function onTimeout() {
				if (this.alpha < 1) {
					this.alpha += this.alphachange;
					if (this.alphachange < 0.0125) {
						this.alphachange += 0.0025;
					}
					this.setTimer(0.05);
				}
			}
			"""
		);

		await script.Call("onCreated");
		await script.Call("onTimeout");

		Assert.Equal(0.00625d, script.GetVariable("alpha").GetValue<double>());
		Assert.Equal(0.00875d, script.GetVariable("alphachange").GetValue<double>());
		Assert.NotNull(script.Timer);
	}

	[Fact]
	public async Task Given_nested_script_event_When_outer_function_resumes_Then_outer_instruction_position_is_restored()
	{
		// Arrange
		_scriptManager.RegisterGlobalVariable("invokenested", (ScriptCommand)((machine, _) => machine.CurrentScript.Call("onNested").ConfigureAwait(false).GetAwaiter().GetResult()));
		var script = CompileScript(
			"""
			//#CLIENTSIDE
			function onCreated() {
				this.marker = "before";
				invokenested();
				this.marker @= "-after";
				return this.marker;
			}

			function onNested() {
				this.nested = true;
			}
			"""
		);

		// Act
		var result = await script.Call("onCreated");

		// Assert
		Assert.Equal("before-after", result.GetValue()?.ToString());
		Assert.True(script.GetVariable("nested").GetValue<bool>());
	}

	private Script CompileLegacyParamsBytecodeScript(string scriptName = "legacyParamsScript") => new(_scriptManager, scriptName, CreateParamsOpcodeBytecode());

	private Script CompileRawBytecodeScript(IReadOnlyCollection<byte> code, IReadOnlyList<string>? strings = null, string scriptName = "rawScript") => new(_scriptManager, scriptName, CreateRawReturnBytecode(code, strings));

	private static byte[] CreateRawReturnBytecode(IReadOnlyCollection<byte> code, IReadOnlyList<string>? strings)
	{
		TString result = new();
		WriteSegment(result, BytecodeSegment.Gs1EventFlags, [0, 0, 0, 0]);

		TString functions = new();
		functions.writeInt(0);
		functions.writeCString("onCreated");
		WriteSegment(result, BytecodeSegment.FunctionNames, functions.toByteArray());

		TString stringSegment = new();
		if (strings != null)
		{
			foreach (var value in strings)
				stringSegment.writeCString(value);
		}

		WriteSegment(result, BytecodeSegment.Strings, stringSegment.toByteArray());

		List<byte> bytecode = new(code) { (byte)Opcode.OP_RET };
		WriteSegment(result, BytecodeSegment.Bytecode, bytecode);
		result.writeByte((byte)'\n');

		return result.toByteArray();
	}

	private static byte[] CreateObjFromStrBytecode(string objectPath)
	{
		TString result = new();
		WriteSegment(result, BytecodeSegment.Gs1EventFlags, [0, 0, 0, 0]);

		TString functions = new();
		functions.writeInt(0);
		functions.writeCString("onCreated");
		WriteSegment(result, BytecodeSegment.FunctionNames, functions.toByteArray());

		TString strings = new();
		strings.writeCString(objectPath);
		WriteSegment(result, BytecodeSegment.Strings, strings.toByteArray());

		byte[] code =
		[
			(byte)Opcode.OP_TYPE_STRING,
			0xF0,
			0,
			(byte)Opcode.OP_OBJ_FROM_STR,
			(byte)Opcode.OP_RET,
		];
		WriteSegment(result, BytecodeSegment.Bytecode, code);
		result.writeByte((byte)'\n');

		return result.toByteArray();
	}

	private static byte[] CreateParamsOpcodeBytecode()
	{
		TString result = new();
		WriteSegment(result, BytecodeSegment.Gs1EventFlags, [0, 0, 0, 0]);

		TString functions = new();
		functions.writeInt(0);
		functions.writeCString("onCreated");
		WriteSegment(result, BytecodeSegment.FunctionNames, functions.toByteArray());

		WriteSegment(result, BytecodeSegment.Strings, []);

		byte[] code =
		[
			(byte)Opcode.OP_PARAMS,
			(byte)Opcode.OP_TYPE_NUMBER,
			0xF3,
			1,
			(byte)Opcode.OP_ARRAY,
			(byte)Opcode.OP_RET,
		];
		WriteSegment(result, BytecodeSegment.Bytecode, code);
		result.writeByte((byte)'\n');

		return result.toByteArray();
	}

	private static byte[] CreateFunctionBoundaryBytecode()
	{
		TString result = new();
		WriteSegment(result, BytecodeSegment.Gs1EventFlags, [0, 0, 0, 0]);

		TString functions = new();
		functions.writeInt(0);
		functions.writeCString("noReturn");
		functions.writeInt(3);
		functions.writeCString("nextFunction");
		WriteSegment(result, BytecodeSegment.FunctionNames, functions.toByteArray());

		TString strings = new();
		strings.writeCString("hit");
		WriteSegment(result, BytecodeSegment.Strings, strings.toByteArray());

		byte[] code =
		[
			(byte)Opcode.OP_TYPE_VAR,
			0xF0,
			0,
			(byte)Opcode.OP_TYPE_NUMBER,
			0xF3,
			1,
			(byte)Opcode.OP_ASSIGN,
			(byte)Opcode.OP_TYPE_NUMBER,
			0xF3,
			99,
			(byte)Opcode.OP_RET,
		];
		WriteSegment(result, BytecodeSegment.Bytecode, code);
		result.writeByte((byte)'\n');

		return result.toByteArray();
	}

	private static void WriteSegment(TString target, BytecodeSegment segment, IReadOnlyCollection<byte> bytes)
	{
		target.writeInt((int)segment);
		target.writeInt(bytes.Count);
		target.writeBytes(bytes);
	}

	private Script InitializePrebakedScript(string fileName) => new(_scriptManager, fileName);

	[Fact]
	public void When_script_is_faulty_Then_exception_is_thrown()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() 
		                          			}
		                          """;


		//Act
		var result = Assert.Throws<Exception>(() => CompileScript(scriptText));
		;

		//Assert
		Assert.Equal("Script failure: malformed input at line 3: \t\t\t}\n", result.Message);
	}

	private void RegisterGlobalObject(string name, ScriptVariable collection) => _scriptManager.RegisterGlobalObject(name, collection);

	[Fact]
	public async Task When_calling_built_in_sin_Then_correct_sin_value_is_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var expectedSin = new List<double> { 1, 0, -1, 0 };
		var sin         = new List<double>();
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function test(dir) {
		                          				temp.angle = (pi/2 * (dir+1));
		                          				
		                          				return sin(temp.angle);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		for (var i = 0; i < 4; i++) sin.Add((await script.Call("test", i)).GetValue<double>());

		//Assert
		for (var i = 0; i < 4; i++) Assert.Equal(expectedSin[i], sin[i]);
	}

	[Fact]
	public async Task When_calling_built_in_cos_Then_correct_cos_value_is_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var expectedCos = new List<double> { 0, -1, 0, 1 };
		var cos         = new List<double>();
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function test(dir) {
		                          				temp.angle = (pi/2) * (dir+1);
		                          				
		                          				return cos(temp.angle);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		for (var i = 0; i < 4; i++) cos.Add((await script.Call("test", i)).GetValue<double>());

		//Assert
		for (var i = 0; i < 4; i++) Assert.Equal(expectedCos[i], cos[i]);
	}

	[Fact]
	public async Task Given_string_When_calling_lowercase_member_function_Then_lowercase_string_should_be_returned()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.value = "Login_Icon.PNG";
		                          	return temp.value.lowercase();
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.Equal("login_icon.png", result.GetValue<TString>()!);
	}

	[Fact]
	public async Task Given_string_When_calling_replaceall_member_function_Then_replaced_string_should_be_returned()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.value = "Zelda: A Link";
		                          	return temp.value.replaceAll(" ", "_").replaceAll(":", "");
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.Equal("Zelda_A_Link", result.GetValue<TString>()!);
	}

	[Fact]
	public async Task Given_temp_var_When_returning_without_temp_prefix_Then_temp_var_should_be_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.var = "test";
		                          				
		                          				temp.var2 = var;
		                          				
		                          				return var2;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("test", result.GetValue<TString>()!);
	}

	[Fact]
	public async Task Given_function_in_script2_When_calling_public_function_in_script1_Then_value_should_be_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText1 = """
		                           			//#CLIENTSIDE
		                           			public function PubFun() {
		                           				temp.var = "PubFun";
		                           				
		                           				temp.var2 = var;
		                           				
		                           				return var2;
		                           			}
		                           """;
		CompileScript(scriptText1, "script1");
		const string scriptText2 = """
		                           			//#CLIENTSIDE
		                           			function onCreated() {
		                           				return script1.PubFun();
		                           			}
		                           """;
		var script2 = CompileScript(scriptText2);

		//Act
		var result = await script2.Call("onCreated");

		//Assert
		Assert.Equal("PubFun", result.GetValue()!.ToString());
	}

	[Fact]
	public async Task Given_function_in_script2_When_calling_public_function_with_argument_Then_argument_is_passed()
	{
		//Arrange
		const string scriptText1 = """
		                           			//#CLIENTSIDE
		                           			public function PubFun(value) {
		                           				return value;
		                           			}
		                           """;
		CompileScript(scriptText1, "script1");
		const string scriptText2 = """
		                           			//#CLIENTSIDE
		                           			function onCreated() {
		                           				return script1.PubFun("from-argument");
		                           			}
		                           """;
		var script2 = CompileScript(scriptText2);

		//Act
		var result = await script2.Call("onCreated");

		//Assert
		Assert.Equal("from-argument", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_function_in_script2_When_calling_public_function_from_string_object_Then_value_should_be_returned()
	{
		//Arrange
		const string scriptText1 = """
		                           			//#CLIENTSIDE
		                           			public function showOptions() {
		                           				return "options-opened";
		                           			}
		                           """;
		CompileScript(scriptText1, "-Serverlist_Options");
		const string scriptText2 = """
		                           			//#CLIENTSIDE
		                           			function onCreated() {
		                           				return ("-Serverlist_Options").showOptions();
		                           			}
		                           """;
		var script2 = CompileScript(scriptText2);

		//Act
		var result = await script2.Call("onCreated");

		//Assert
		Assert.Equal("options-opened", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_named_script_exists_When_isobject_guard_calls_public_function_from_string_object_Then_value_should_be_returned()
	{
		//Arrange
		const string scriptText1 = """
		                           			//#CLIENTSIDE
		                           			public function initServerlist() {
		                           				return "serverlist-opened";
		                           			}
		                           """;
		CompileScript(scriptText1, "-Rescripted/Serverlist");
		const string scriptText2 = """
		                           			//#CLIENTSIDE
		                           			function onCreated() {
		                           				if (isObject("-Rescripted/Serverlist")) {
		                           					return ("-Rescripted/Serverlist").initServerlist();
		                           				}

		                           				return "missing";
		                           			}
		                           """;
		var script2 = CompileScript(scriptText2);

		//Act
		var result = await script2.Call("onCreated");

		//Assert
		Assert.Equal("serverlist-opened", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_function_without_return_When_next_function_has_bytecode_Then_execution_stops_at_function_boundary()
	{
		//Arrange
		var script = new Script(_scriptManager, "boundaryScript", CreateFunctionBoundaryBytecode());

		//Act
		var result = await script.Call("noReturn");

		//Assert
		Assert.Equal(0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Inline_callback_does_not_truncate_the_enclosing_function()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				temp.callback = function(temp.x) { return temp.x + 1; };
				this.completed = true;
				return temp.callback(41);
			}
			function unrelated() { this.unrelatedCalled = true; return 99; }
			"""
		);

		Assert.Equal(42d, (await script.Call("onCreated")).GetValue<double>());
		Assert.True(script.GetVariable("completed").GetValue<bool>());
		Assert.False(script.ContainsVariable("unrelatedcalled"));
	}

	[Fact]
	public async Task Gui_factory_returns_control_after_inline_drawing_callback()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				temp.gui = new GuiControl("SceneText");
				temp.draw = function(temp.control) { temp.control.width = 240; };
				temp.draw(temp.gui);
				return temp.gui;
			}
			"""
		);

		var control = Assert.IsType<GuiControl>((await script.Call("onCreated")).GetValue());
		Assert.Equal(240, control.Width);
	}

	[Fact]
	public async Task Inline_callback_string_conversion_preserves_its_function_name_for_indirect_calls()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				temp.callback = function(temp.value) { return temp.value + 1; };
				temp.functionName = "" @ temp.callback;
				return temp.functionName(41);
			}
			"""
		);

		Assert.Equal(42d, (await script.Call("onCreated")).GetValue<double>());
	}

	[Fact]
	public async Task Joined_callback_can_be_called_by_its_string_name_on_the_original_receiver()
	{
		CompileScript(
			"""
			function onCreated() {
				this.callback = function(temp.value) { return this.offset + temp.value; };
			}
			""",
			"callbackclass"
		);
		var owner = CompileScript(
			"""
			function onCreated() { this.offset = 40; }
			function callCallback() {
				temp.functionName = "" @ this.callback;
				return temp.functionName(2);
			}
			"""
		);
		owner.Join("callbackclass");
		await owner.Call("onCreated");

		Assert.Equal(42d, (await owner.Call("callCallback")).GetValue<double>());
	}

	[Fact]
	public async Task Gui_control_passed_from_new_block_through_joined_class_keeps_its_receiver()
	{
		CompileScript(
			"""
			function setAnimation(temp.gui, temp.duration, temp.transition) {
				temp.anim = temp.gui.createAnimation();
				temp.anim.duration = temp.duration ? temp.duration : 0.5;
				temp.anim.transition = temp.transition;
				return temp.anim;
			}
			function fadeIn(temp.gui, temp.duration) { return thiso.setAnimation(temp.gui, temp.duration, "fadein"); }
			""",
			"animationhelpers"
		);
		var script = CompileScript(
			"""
			function onCreated() {
				this.join("animationhelpers");
				new GuiControl(TestPanel) {
					thiso.animation = thiso.fadeIn(this);
					thiso.configure(this);
				}
			}
			function configure(temp.gui) {
				temp.gui.useownprofile = true;
				temp.profile = temp.gui.profile;
				temp.profile.cankeyfocus = false;
				temp.profile.tab = false;
				this.configured = temp.gui;
			}
			"""
		);

		await script.Call("onCreated");

		var panel = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables.GetVariable("testpanel").GetValue());
		Assert.Same(panel, script.GetVariable("configured").GetValue());
		Assert.IsType<GuiAnimation>(script.GetVariable("animation").GetValue());
		Assert.True(panel.UseOwnProfile);
		Assert.False(panel.GetResolvedProfile()!.CanKeyFocus);
	}

	[Theory]
	[InlineData("created", "onCreated")]
	[InlineData("onCREATED", "onCreated")]
	[InlineData("initialized", "onInitialized")]
	[InlineData("onInitFrame", "onInitFrame")]
	public async Task Initialization_events_run_owner_and_joined_handlers_on_the_owner(string eventName, string handler)
	{
		var first  = CompileScript($"function {handler}(temp.value) {{ this.trace @= temp.value; }}", "firstinitializer");
		var second = CompileScript($"function {handler}() {{ this.trace @= \"second\"; }}", "secondinitializer");
		var owner  = CompileScript($"function {handler}() {{ this.trace = \"owner\"; return 42; }}");
		owner.Join("firstinitializer");
		owner.Join("secondinitializer");

		Assert.Equal(42d, (await owner.Call(eventName, "first")).GetValue<double>());
		Assert.Equal("ownerfirstsecond", owner.GetVariable("trace").GetValue()?.ToString());
		Assert.False(first.ContainsVariable("trace"));
		Assert.False(second.ContainsVariable("trace"));
	}

	[Fact]
	public async Task Initialization_events_include_new_classes_and_deduplicate_nested_handlers()
	{
		var nested = CompileScript("function onCreated() { this.trace @= \"nested\"; }", "nestedinitializer");
		var first  = CompileScript("function onCreated() { this.trace @= \"first\"; this.join(\"lateinitializer\"); }", "firstinitializer");
		var late   = CompileScript("function onCreated() { this.trace @= \"late\"; }", "lateinitializer");
		first.Join("nestedinitializer");
		late.Join("nestedinitializer");
		nested.Join("firstinitializer");
		var owner = CompileScript("function onCreated() { this.trace = \"owner\"; }");
		owner.Join("firstinitializer");

		await owner.Call("onCreated");

		Assert.Equal("ownerfirstnestedlate", owner.GetVariable("trace").GetValue()?.ToString());
	}

	[Fact]
	public async Task Joined_initializer_installs_inline_callback_after_owner_initialization()
	{
		CompileScript(
			"""
			function onCreated() {
				this.words = function(temp.value) { return temp.value.tokenize(); };
				this.helpersReady = true;
			}
			""",
			"prelude"
		);
		var owner = CompileScript(
			"""
			function onCreated() { this.ownerReady = true; }
			function formatWords() { return this.words("one two"); }
			"""
		);
		owner.Join("prelude");

		await owner.Call("onCreated");

		Assert.True(owner.GetVariable("ownerready").GetValue<bool>());
		Assert.True(owner.GetVariable("helpersready").GetValue<bool>());
		var words = Assert.IsType<List<object>>((await owner.Call("formatWords")).GetValue());
		Assert.Equal(new[] { "one", "two" }, words.Select(word => word.ToString()));
	}

	[Fact]
	public async Task Ordinary_events_and_direct_initialization_method_calls_do_not_fan_out()
	{
		CompileScript("function onTimeout() { this.trace @= \"class\"; } function onCreated() { this.trace @= \"class\"; }", "initializer");
		var owner = CompileScript(
			"""
			function onCreated() { this.trace = "owner"; }
			function onTimeout() { this.trace = "timeout"; }
			function initializeDirectly() { this.onCreated(); }
			"""
		);
		owner.Join("initializer");

		await owner.Call("onTimeout");
		Assert.Equal("timeout", owner.GetVariable("trace").GetValue()?.ToString());
		await owner.Call("initializeDirectly");
		Assert.Equal("owner", owner.GetVariable("trace").GetValue()?.ToString());
	}

	[Fact]
	public async Task Script_field_and_function_with_same_name_remain_independent()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				this.hidesplash = false;
				this.before = this.hidesplash;
				this.hideSplash();
				return this.hidesplash;
			}
			function hideSplash() {
				this.hidesplash = true;
				this.trigger("Timeout", null);
			}
			function onTimeout() {
				if (this.hidesplash) this.faded = true;
			}
			"""
		);

		var result = await script.Call("onCreated");

		Assert.False(script.GetVariable("before").GetValue<bool>());
		Assert.True(result.GetValue<bool>());
		Assert.False(script.GetVariable("faded").GetValue<bool>());
		await _scriptManager.DispatchPendingEvents();
		Assert.True(script.GetVariable("faded").GetValue<bool>());
	}

	[Fact]
	public async Task Null_argument_preserves_its_position_in_script_calls()
	{
		var script = CompileScript(
			"""
			function onCreated() { return capture(null, "second", "third"); }
			function capture(temp.first, temp.second, temp.third) {
				return (temp.first == null) @ ":" @ temp.second @ ":" @ temp.third;
			}
			"""
		);

		var result = await script.Call("onCreated");

		Assert.Equal("1:second:third", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_script_join_When_class_is_joined_Then_class_script_is_requested()
	{
		//Arrange
		var requestedClasses = new List<string>();
		_scriptManager.SetClassScriptRequestHandler(requestedClasses.Add);
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		Assert.Equal(["joinedclass"], requestedClasses);
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_calling_class_function_Then_value_should_be_returned()
	{
		//Arrange
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function ClassValue() {
		                         				return 42;
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          				return this.ClassValue();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(42.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_calling_class_function_without_this_Then_value_should_be_returned()
	{
		//Arrange
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function ClassValue() {
		                         				return 42;
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          				return ClassValue();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(42.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_event_exists_only_in_class_Then_event_is_executed()
	{
		//Arrange
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function onCreated() {
		                         				this.marker = "from-class-event";
		                         				return this.marker;
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function SomeOtherFunction() {
		                          				return 0;
		                          			}
		                          """;
		var script = CompileScript(scriptText);
		script.Join("joinedclass");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("from-class-event", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_joined_class_function_calls_global_command_When_reading_current_receiver_Then_receiver_is_joined_object()
	{
		string? receiverName = null;
		_scriptManager.RegisterGlobalVariable(
			"capturereceiver",
			(ScriptCommand)((machine, _) =>
			{
				receiverName = machine.CurrentReceiver.Name;
				return 0.ToStackEntry();
			})
		);
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function Capture() {
		                         				capturereceiver();
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          				Capture();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		await script.Call("onCreated");

		Assert.Equal("testScript", receiverName);
	}

	[Fact]
	public async Task Given_joined_class_function_calls_private_receiver_function_When_executed_Then_receiver_function_is_called()
	{
		const string classText = """
		                          			//#CLIENTSIDE
		                          			function Initialize() {
		                          				return this.CanInitialize();
		                          			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                           			//#CLIENTSIDE
		                           			function onCreated() {
		                           				this.join("joinedclass");
		                           				return this.Initialize();
		                           			}

		                           			function CanInitialize() {
		                           				return 42;
		                           			}
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.Equal(42.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_object_joined_to_class_with_public_function_When_hasfunction_is_called_Then_returns_true()
	{
		const string classText = """
		                         //#CLIENTSIDE
		                         public function IsStaff() {
		                         	return true;
		                         }
		                         """;
		CompileScript(classText, "playerfunctions");
		var receiver = new ScriptVariable("player");
		receiver.Join("playerfunctions");
		_scriptManager.RegisterGlobalVariable("player", receiver);
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	return player.hasfunction("isStaff");
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_object_without_function_When_hasfunction_is_called_Then_returns_false()
	{
		var receiver = new ScriptVariable("player");
		_scriptManager.RegisterGlobalVariable("player", receiver);
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	return player.hasfunction("missingFunction");
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.False(result.GetValue<bool>());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CancelEvents_removes_matching_events_including_queued_callbacks(bool queued)
	{
		var script = CompileScript(
			"""
			function onCreated() {
				this.scheduleevent(0, "Ready");
				this.scheduleevent(0, "Other");
				this.scheduleevent(0, "READY");
			}
			function cancel() { this.cancelevents("rEaDy"); }
			function onReady() { this.cancelledCalls++; }
			function onOther() { this.otherCalls++; }
			"""
		);
		await script.Call("onCreated");
		var now    = DateTime.UtcNow.AddSeconds(1);
		var events = queued ? script.TakeDueScriptScheduledEvents(now) : null;
		await script.Call("cancel");
		events ??= script.TakeDueScriptScheduledEvents(now);
		foreach (var scheduledEvent in events)
			await script.CallScheduledEvent(scheduledEvent);
		Assert.False(script.ContainsVariable("cancelledcalls"));
		Assert.Equal(1d, script.GetVariable("othercalls").GetValue<double>());
		Assert.Empty(script.TakeDueScriptScheduledEvents(now));
		script.ScheduleEvent(script, 0, "Ready");
		await script.CallScheduledEvent(Assert.Single(script.TakeDueScriptScheduledEvents(now)));
		Assert.Equal(1d, script.GetVariable("cancelledcalls").GetValue<double>());
	}

	[Fact]
	public async Task Missing_global_object_does_not_pass_null_guard()
	{
		var script = CompileScript(
			"""
			function onCreated() {
				this.entered = false;
				if (MissingObject != null) this.entered = true;
				return this.entered;
			}
			"""
		);
		Assert.False((await script.Call("onCreated")).GetValue<bool>());
	}

	[Fact]
	public async Task Given_script_schedules_named_event_When_event_becomes_due_Then_event_runs_with_arguments()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	this.scheduleevent(0.05, "Ready", "done");
		                          }

		                          function onReady(temp.value) {
		                          	return temp.value;
		                          }
		                          """;
		var script = CompileScript(scriptText);
		await script.Call("onCreated");

		var scheduledEvent = Assert.Single(script.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1)));
		var result         = await script.CallScheduledEvent(scheduledEvent);

		Assert.Equal("done", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_named_event_listener_When_control_animation_finishes_Then_both_handlers_run_with_correct_arguments()
	{
		var owner = CompileScript("function panel.onAnimationFinished(temp.transition) { this.transition = temp.transition; this.calls++; }", "owner");
		var panel = new GuiControl("panel", owner);
		var listener = CompileScript(
			"""
			function listen() { this.catchevent(panel.name, "onAnimationFinished", "finished"); }
			function finished(temp.sender, temp.transition) { this.sender = temp.sender; this.transition = temp.transition; this.calls++; this.ignoreevent("panel", "onAnimationFinished"); }
			""",
			"listener"
		);
		await listener.Call("listen");
		var animation = panel.CreateAnimation()!;
		animation.Transition = "fadein";
		animation.Duration   = 0.25;
		panel.AdvanceAnimations(0.3);
		await _scriptManager.DispatchPendingEvents();
		Assert.Equal(1d, owner.GetVariable("calls").GetValue<double>());
		Assert.Equal("fadein", owner.GetVariable("transition").GetValue<string>());
		Assert.Same(panel, listener.GetVariable("sender").GetValue());
		Assert.Equal("fadein", listener.GetVariable("transition").GetValue<string>());
		await owner.Call("panel.onAnimationFinished", "fadeout");
		Assert.Equal(1d, listener.GetVariable("calls").GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_callbacks_When_called_repeatedly_Then_temps_are_local_to_each_function()
	{
		var script = CompileScript(
			"""
			function panel.onSelect(temp.selected) {
				this.before = temp.i;
				temp.i = temp.selected;
				helper();
				this.after = temp.i;
			}
			function helper() { this.helperStart = temp.i; temp.i = 99; }
			"""
		);
		await script.Call("panel.onSelect", 3);
		await script.Call("panel.onSelect", 5);
		Assert.Equal(0d, script.GetVariable("before").GetValue<double>());
		Assert.Equal(0d, script.GetVariable("helperStart").GetValue<double>());
		Assert.Equal(5d, script.GetVariable("after").GetValue<double>());
	}

	[Fact]
	public async Task Given_reentrant_gui_event_When_control_resizes_Then_caller_temps_are_preserved()
	{
		var script = CompileScript(
			"""
			function resize() { temp.i = 3; panel.width = 120; return temp.i; }
			function panel.onResize() { temp.i = 99; }
			"""
		);
		_ = new GuiControl("panel", script);
		var result = await script.Call("resize");
		Assert.Equal(3d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_missing_temp_parameter_named_like_native_method_When_read_Then_it_is_zero()
	{
		var script = CompileScript("function check(temp.destroy) { return temp.destroy ? 1 : 0; }");
		var result = await script.Call("check");
		Assert.Equal(0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_array_string_cell_When_compared_to_null_Then_nonempty_name_is_not_null()
	{
		var script = CompileScript(
			"""
			function check() {
				temp.values = {"Someone", "", 0};
				return {temp.values[0] == null, temp.values[1] == null, temp.values[2] == null};
			}
			"""
		);
		var result = await script.Call("check");
		Assert.Equal("0,1,1", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_foreach_constructs_strings_When_appending_to_temp_array_Then_all_results_are_kept()
	{
		var script = CompileScript(
			"""
			function check() {
				return render({{"first", "hello"}, {"second", "world"}});
			}
			function render(temp.lines) {
				for (temp.l : temp.lines) temp.newLines.add(construct(temp.l));
				return temp.newLines;
			}
			function construct(temp.lines) { return temp.lines[0] @ ":" @ temp.lines[1]; }
			"""
		);
		var result = await script.Call("check");
		Assert.Equal("first:hello,second:world", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_missing_global_When_compared_to_null_Then_it_is_zero()
	{
		var script = CompileScript("function check() { return MissingWindow == null; }");
		var result = await script.Call("check");
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_joined_class_timeout_rearms_itself_When_next_timeout_is_due_Then_it_keeps_the_joined_receiver()
	{
		const string classText = """
		                         function onTimeout() {
		                             this.calls++;
		                             this.setTimer(0.05);
		                         }
		                         """;
		var timerClass = CompileScript(classText, "timerclass");
		var receiver   = CompileScript("function unused() {}", "receiver");
		receiver.Join("timerclass");

		await receiver.Call("onTimeout");
		var firstEvent = Assert.Single(timerClass.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1)));
		await timerClass.CallScheduledEvent(firstEvent);
		var secondEvent = Assert.Single(timerClass.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1)));

		Assert.Same(receiver, firstEvent.Receiver);
		Assert.Same(receiver, secondEvent.Receiver);
		Assert.Equal(2d, receiver.GetVariable("calls").GetValue<double>());
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_event_exists_in_nested_class_Then_event_is_executed()
	{
		//Arrange
		const string nestedClassText = """
		                               			//#CLIENTSIDE
		                               			function onCreated() {
		                               				this.marker = "from-nested-class-event";
		                               				return this.marker;
		                               			}
		                               """;
		CompileScript(nestedClassText, "nestedclass");
		const string joinedClassText = """
		                               			//#CLIENTSIDE
		                               			function SomeClassFunction() {
		                               				return 0;
		                               			}
		                               """;
		var joinedClass = CompileScript(joinedClassText, "joinedclass");
		joinedClass.Join("nestedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function SomeOtherFunction() {
		                          				return 0;
		                          			}
		                          """;
		var script = CompileScript(scriptText);
		script.Join("joinedclass");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("from-nested-class-event", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_class_function_writes_this_Then_joining_script_is_updated()
	{
		//Arrange
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function SetClassMarker() {
		                         				this.marker = "from-class";
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          				this.SetClassMarker();
		                          				return this.marker;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("from-class", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_script_joined_to_class_When_class_function_has_argument_Then_argument_is_passed()
	{
		//Arrange
		const string classText = """
		                         			//#CLIENTSIDE
		                         			function EchoClassValue(value) {
		                         				return value;
		                         			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.join("joinedclass");
		                          				return this.EchoClassValue("from-argument");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("from-argument", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_joined_class_dynamic_dispatch_When_temp_command_is_passed_Then_receiver_function_is_called()
	{
		const string classText = """
		                          			//#CLIENTSIDE
		                          			function onActionClientSide(temp.command, temp.value) {
		                          				this.(temp.command)(temp.value);
		                          			}
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                           			//#CLIENTSIDE
		                           			function ReceiveValue(temp.value) {
		                           				this.received = temp.value;
		                           			}
		                          """;
		var script = CompileScript(scriptText);
		script.Join("joinedclass");

		await script.Call("onActionClientSide", "ReceiveValue", "from-argument");

		Assert.Equal("from-argument", script.GetVariable("received").GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_joined_class_dynamic_dispatch_When_receiver_assigns_itself_to_global_Then_global_contains_receiver()
	{
		const string classText = """
		                         //#CLIENTSIDE
		                         function onActionClientSide(temp.command) {
		                         	this.(temp.command)();
		                         }
		                         """;
		CompileScript(classText, "joinedclass");
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onLoadOptions() {
		                          	this.setInit();
		                          }

		                          function setInit() {
		                          	this.init = true;
		                          	SystemOptions = this;
		                          }
		                          """;
		var script = CompileScript(scriptText);
		script.Join("joinedclass");

		await script.Call("onActionClientSide", "onLoadOptions");

		Assert.Same(script, _scriptManager.GlobalVariables.GetVariable("systemoptions").GetValue());
		Assert.True(script.GetVariable("init").GetValue<bool>());
	}

	[Fact]
	public async Task Given_playero_When_member_read_Then_registered_playero_object_is_used()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var playero = new ScriptVariable();
		playero.AddOrUpdate("account", "testaccount".ToStackEntry());
		RegisterGlobalObject("playero", playero);
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return playero.account;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("testaccount", result.GetValue()!.ToString());
	}

	[Theory]
	[InlineData("client")]
	[InlineData("clientr")]
	[InlineData("serverr")]
	public async Task Given_flag_namespace_When_assigned_Then_only_registered_namespace_is_updated(string name)
	{
		var player = new ScriptVariable();
		var flags  = new ScriptVariable(name);
		RegisterGlobalObject("player", player);
		RegisterGlobalObject(name, flags);
		var script = CompileScript($"function onCreated() {{ {name}.hasmp = true; }}");

		await script.Call("onCreated");

		Assert.True(flags.GetVariable("hasmp").GetValue<bool>());
		Assert.False(player.ContainsVariable("hasmp"));
	}

	[Theory]
	[InlineData("client")]
	[InlineData("clientr")]
	[InlineData("serverr")]
	public async Task Given_flag_namespace_When_read_Then_registered_namespace_value_is_returned(string name)
	{
		var player = new ScriptVariable();
		var flags  = new ScriptVariable(name);
		player.AddOrUpdate("hasmp", false.ToStackEntry());
		flags.AddOrUpdate("hasmp", true.ToStackEntry());
		RegisterGlobalObject("player", player);
		RegisterGlobalObject(name, flags);
		var script = CompileScript($"function onCreated() {{ return {name}.hasmp; }}");

		var result = await script.Call("onCreated");

		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_level_When_member_read_Then_registered_level_object_is_used()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var level = new ScriptVariable();
		level.AddOrUpdate("name", "testlevel.nw".ToStackEntry());
		RegisterGlobalObject("level", level);
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return level.name;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("testlevel.nw", result.GetValue()!.ToString());
	}

	[Fact]
	public async Task CallWithContext_Given_player_When_called_Then_reads_player_opcode()
	{
		//Arrange
		var player = new ScriptVariable();
		player.AddOrUpdate("account", "context-player".ToStackEntry());
		var script = CompileScript("function readPlayer() { return player.account; }");

		//Act
		var result = await script.CallWithContext("readPlayer", new() { Player = player });

		//Assert
		Assert.Equal("context-player", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task CallWithContext_Given_player_object_When_called_Then_reads_playero_opcode()
	{
		//Arrange
		var playerObject = new ScriptVariable();
		playerObject.AddOrUpdate("account", "context-playero".ToStackEntry());
		var script = CompileScript("function readPlayerObject() { return playero.account; }");

		//Act
		var result = await script.CallWithContext("readPlayerObject", new() { PlayerObject = playerObject });

		//Assert
		Assert.Equal("context-playero", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task CallWithContext_Given_level_When_called_Then_reads_level_opcode()
	{
		//Arrange
		var level = new ScriptVariable();
		level.AddOrUpdate("name", "context-level.nw".ToStackEntry());
		var script = CompileScript("function readLevel() { return level.name; }");

		//Act
		var result = await script.CallWithContext("readLevel", new() { Level = level });

		//Assert
		Assert.Equal("context-level.nw", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task CallWithContext_Given_null_context_When_called_Then_throws_argument_null_exception()
	{
		//Arrange
		var script = CompileScript("function probe() { return 1; }");

		//Act
		var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => script.CallWithContext("probe", null!));

		//Assert
		Assert.Equal("Value cannot be null. (Parameter 'executionContext')", exception.Message);
	}

	[Fact]
	public async Task Given_this_var3_When_returning_without_this_prefix_Then_0_should_be_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.var3 = "test2";
		                          				
		                          				return var3;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0d, result.GetValue()!);
	}

	[Fact]
	public async Task Given_temp_var_When_creating_new_array_object_Then_result_should_be_empty_array_object()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.GlobalVariables.Clear();
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.var = new[2];
		                          				
		                          				return temp.var == {0,0};
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue()!);
	}

	[Fact(Skip = "Waiting for fix in GS2Compiler")]
	public async Task Given_temp_update_When_comparing_multiple_or_and_one_variable_is_updated_Then_result_should_be_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.GlobalVariables.Clear();
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.var1 = 30.5;
		                          				temp.var2 = 30;
		                          				temp.var3 = 2;
		                          				temp.var4 = 0;
		                          				temp.var5 = "myvar";
		                          				this.oldData = {var1,var2,var3,var4,var5};
		                          				var3 = -1;
		                          				temp.update = var1 != this.oldData[0] ||
		                          				    var2 != this.oldData[1] ||
		                          				    (var3 == -1 && this.oldData[2] >=0);
		                          				return temp.update;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(Convert.ToBoolean(result.GetValue()!));
	}

	[Fact(Skip = "Waiting for fix in GS2Compiler")]
	public async Task Given_temp_var_When_at_comparing_Then_result_should_be_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.GlobalVariables.Clear();
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.var = {true,true};
		                          				echo(@var);
		                          				echo(""@{1,1});

		                          				return @var == @{1,1};
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(true, result.GetValue()!);
	}

	[Fact]
	public async Task Given_temp_var3_is_array_object_When_comparing_values_with_another_array_object_with_identical_values_Then_result_should_be_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.var3 = {1,"asd",3};
		                          				
		                          				return temp.var3 == {1,"asd",3};
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue()!);
	}

	[Fact]
	public async Task Given_swedish_culture_When_number_is_joined_to_string_Then_decimal_separator_is_us_english()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var previousCulture   = CultureInfo.CurrentCulture;
		var previousUiCulture = CultureInfo.CurrentUICulture;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "" @ 0.5;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		try
		{
			CultureInfo.CurrentCulture   = CultureInfo.GetCultureInfo("sv-SE");
			CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("sv-SE");

			//Act
			var result = await script.Call("onCreated");

			//Assert
			Assert.Equal("0.5", result.GetValue()?.ToString());
		}
		finally
		{
			CultureInfo.CurrentCulture   = previousCulture;
			CultureInfo.CurrentUICulture = previousUiCulture;
		}
	}

	[Fact]
	public async Task Given_array_When_size_called_Then_returns_count()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2, 3};
		                          				return temp.values.size();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_index_called_Then_returns_matching_index()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 8, 3};
		                          				return temp.values.index(8);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_type_called_Then_returns_array_type()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1};
		                          				return temp.values.type();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_in_operator_called_Then_returns_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return 2 in {1, 2, 3};
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_number_When_in_range_called_Then_returns_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return 2 in |1, 3|;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_if_condition_When_value_is_non_one_nonzero_Then_branch_is_taken()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				if (2) return "true";
		                          				return "false";
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>()!);
	}

	[Fact]
	public async Task Given_if_condition_When_value_is_zero_Then_branch_is_not_taken()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				if (0) return "true";
		                          				return "false";
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("false", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_or_opcode_When_left_value_is_non_one_nonzero_Then_left_value_is_preserved()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				2,
				(byte)Opcode.OP_OR,
				0xF3,
				3,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				0,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("2", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_and_opcode_When_left_value_is_zero_Then_left_value_is_preserved()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				0,
				(byte)Opcode.OP_AND,
				0xF3,
				3,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				9,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_assignment_after_false_and_or_expression_When_executing_Then_original_target_is_assigned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function isFalse() {
		                          				return false;
		                          			}

		                          			function onCreated() {
		                          				result = 1;
		                          				result = isFalse() && "" == "x" || "".pos("toon") >= 0;
		                          				return result;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.False(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_or_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_OR,
				0xF3,
				2,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_and_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_AND,
				0xF3,
				2,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_add_called_Then_appends_value()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2};
		                          				temp.values.add(3);
		                          				return temp.values[2];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_end_without_array_start_When_executing_Then_returns_array_from_available_stack_values()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				1,
				(byte)Opcode.OP_ARRAY_END,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(StackEntryType.Array, result.Type);
		var values = Assert.IsType<List<object>>(result.GetValue());
		Assert.Equal(1.0d, Assert.Single(values));
	}

	[Fact]
	public async Task Given_comparison_opcode_without_operands_When_executing_Then_missing_operands_default_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_EQ,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_call_opcode_without_operand_When_executing_Then_missing_call_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_CALL,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_assign_opcode_without_operands_When_executing_Then_missing_assignment_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_ASSIGN,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_optimized_compare_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_UNKNOWN_226,
				0xF3,
				0,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_join_opcode_without_operands_When_executing_Then_missing_operands_default_to_empty_strings()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_JOIN,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(string.Empty, result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_if_opcode_without_operand_When_executing_Then_missing_operand_is_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_IF,
				0xF3,
				2,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_format_opcode_without_operands_When_executing_Then_missing_format_defaults_to_empty_string()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_FORMAT,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(string.Empty, result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_format_opcode_with_assignment_target_When_executing_Then_target_is_preserved()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				1,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_FORMAT,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
			],
			["colorstr", "#%.2x"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("#01", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_int_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_INT,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_abs_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_ABS,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_object_length_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_OBJ_LENGTH,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_substring_opcode_without_operands_When_executing_Then_missing_operands_default_to_empty_string()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_OBJ_SUBSTR,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(string.Empty, result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_string_When_starts_matches_prefix_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "P Login".starts("P ");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_string_When_starts_does_not_match_prefix_Then_false_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "P Login".starts("H ");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_member_access_opcode_without_member_name_When_executing_Then_missing_member_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_MEMBER_ACCESS,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_convert_to_object_opcode_without_operand_When_executing_Then_missing_operand_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_CONV_TO_OBJECT,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_new_object_opcode_without_operands_When_executing_Then_missing_operands_default_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_NEW_OBJECT,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_makevar_opcode_without_operand_When_executing_Then_missing_name_defaults_to_empty_variable()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_MAKEVAR,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_object_from_string_opcode_without_operand_When_executing_Then_missing_name_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_OBJ_FROM_STR,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_member_access_opcode_without_parent_When_executing_Then_missing_parent_defaults_to_zero()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["value"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_delete_called_Then_removes_index()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2, 3};
		                          				temp.values.delete(1);
		                          				return temp.values[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_insert_called_Then_inserts_value_at_index()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 3};
		                          				temp.values.insert(1, 2);
		                          				return temp.values[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(2.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_remove_called_Then_removes_first_matching_value()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2, 3};
		                          				temp.values.remove(2);
		                          				return temp.values[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_replace_called_Then_replaces_value_at_index()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2, 3};
		                          				temp.values.replace(1, 8);
		                          				return temp.values[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(8.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_subarray_called_Then_returns_requested_slice()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 8, 4};
		                          				temp.tail = temp.values.subarray(1, 2);
		                          				return temp.tail[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(4.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_cell_assigned_Then_returns_assigned_value()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1, 2, 3};
		                          				temp.values[1] = 8;
		                          				return temp.values[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(8.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_array_When_setarray_called_Then_resizes_array()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.values = {1};
		                          				setarray(temp.values, 3);
		                          				return temp.values.size();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_multidimensional_array_When_created_Then_nested_cell_defaults_to_zero()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.grid = new[2][3];
		                          				return temp.grid[1, 2];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_multidimensional_array_When_cell_assigned_Then_returns_assigned_value()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.grid = new[2][3];
		                          				temp.grid[1, 2] = 8;
		                          				return temp.grid[1, 2];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(8.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_makevar_When_assigned_Then_target_variable_is_updated()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				makevar("this.dynamic") = 9;
		                          				return this.dynamic;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(9.0d, result.GetValue());
	}

	[Fact]
	public async Task Given_registered_object_creator_When_new_object_called_Then_factory_creates_object()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.RegisterObjectCreator(
			"TestCreatedObject",
			(id, _) =>
			{
				var created = new ScriptVariable(id);
				created.AddOrUpdate("kind", "registered".ToStackEntry());
				return created;
			}
		);
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.created = new TestCreatedObject("test");
		                          				return temp.created.kind;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("registered", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_tstaticvar_When_created_Then_dynamic_properties_can_be_stored()
	{
		var script = CompileScript("function onCreated() { temp.object = new TStaticVar(); temp.object.value = 7; return temp.object.value; }");

		var result = await script.Call("onCreated");

		Assert.Equal(7d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_script_object_When_trigger_is_called_Then_object_event_is_invoked()
	{
		var script = CompileScript("function onCreated() { object = new TStaticVar(\"object\"); object.trigger(\"Ping\", 7); return object.value; } function object.onPing(value) { this.value = value; } function read() { return object.value; }");

		var result = await script.Call("onCreated");
		Assert.Equal(0d, result.GetValue<double>());
		await _scriptManager.DispatchPendingEvents();
		result = await script.Call("read");

		Assert.Equal(7d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_registered_global_function_When_called_Then_missing_function_is_not_logged()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		var script = CompileScript("function onCreated() { return uppercase(\"registered\"); }");

		var result = await script.Call("onCreated");

		Assert.Equal("REGISTERED", result.GetValue()?.ToString());
		Assert.DoesNotContain(logLines, line => line.Contains("Function uppercase not found", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Given_unregistered_function_When_called_Then_missing_function_is_logged()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		var script = CompileScript("function onCreated() { functionThatDoesNotExist(); }");

		await script.Call("onCreated");

		Assert.Contains(logLines, line => line.Equals("Script: Function functionThatDoesNotExist not found in function onCreated in script of Weapon testScript", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Given_repeated_unregistered_function_When_called_Then_missing_function_is_logged_once()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		var script = CompileScript("function onTimeout() { functionThatDoesNotExist(); }");

		await script.Call("onTimeout");
		await script.Call("onTimeout");

		Assert.Single(logLines, line => line.Contains("Function functionThatDoesNotExist not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Given_unregistered_object_creator_When_object_is_created_Then_missing_creator_is_logged()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		var script = CompileScript("function onCreated() { temp.object = new ObjectCreatorThatDoesNotExist(\"object\"); temp.object.value = 1; }");

		await script.Call("onCreated");

		Assert.Contains(logLines, line => line.Equals("Script: Object creator ObjectCreatorThatDoesNotExist not found in function onCreated in script of Weapon testScript", StringComparison.OrdinalIgnoreCase));
		Assert.DoesNotContain(logLines, line => line.Contains("Script: Property", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Given_native_script_object_When_unknown_property_is_read_Then_missing_property_is_logged()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		_scriptManager.RegisterGlobalVariable("preagonalversion", new Version(1, 2, 3, 4));
		var script = CompileScript("function onCreated() { return preagonalversion.propertyThatDoesNotExist; }");

		await script.Call("onCreated");

		Assert.Contains(logLines, line => line.Contains("Property Version.propertyThatDoesNotExist not found in function onCreated", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Given_typed_script_object_When_dynamic_property_is_assigned_Then_missing_property_is_not_logged()
	{
		var logLines = new List<string>();
		Tools.SetLogFuncWriteLine(line => logLines.Add(line ?? string.Empty));
		_scriptManager.RegisterObjectCreator("MemberFunctionChild", (id, _) => new MemberFunctionChildObject(id));
		var script = CompileScript("function onCreated() { temp.object = new MemberFunctionChild(\"typedObject\"); temp.object.dynamicProperty = 7; return temp.object.dynamicProperty; }");

		var result = await script.Call("onCreated");

		Assert.Equal(7d, result.GetValue<double>());
		Assert.DoesNotContain(logLines, line => line.Contains("Property typedObject.dynamicProperty not found", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Given_with_block_member_object_When_member_function_is_called_Then_member_object_is_used()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.RegisterObjectCreator("MemberFunctionParent", (id, _) => new(id));
		_scriptManager.RegisterObjectCreator("MemberFunctionChild", (id, _) => new MemberFunctionChildObject(id));
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new MemberFunctionParent("parent");
		                          				parent.child = new MemberFunctionChild("child");
		                          				with (parent) {
		                          					child.mark();
		                          				}
		                          				return parent.child.called;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_new_object_name_operand_is_variable_When_variable_has_value_Then_creator_receives_operand_name()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		_scriptManager.RegisterObjectCreator(
			"TestCreatedObject",
			(id, _) =>
			{
				var created = new ScriptVariable(id);
				created.AddOrUpdate("id", id.ToStackEntry());
				return created;
			}
		);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF6,
				(byte)'1',
				(byte)'.',
				(byte)'5',
				0,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_NEW_OBJECT,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				2,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["temp.scale", "TestCreatedObject", "id"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("temp.scale", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_function_registered_twice_When_called_Then_latest_registration_is_used()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		ScriptProperties<ScriptMachineTests>.AddFunctions(null, new() { { "duplicatefunctionregistration", "", (_, _) => "old" } });
		ScriptProperties<ScriptMachineTests>.AddFunctions(null, new() { { "duplicatefunctionregistration", "", (_, _) => "new" } });
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return duplicatefunctionregistration();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("new", result.GetValue()?.ToString());
	}

	[Fact]
	public void Given_child_script_properties_When_parent_has_same_property_Then_child_property_is_used()
	{
		//Arrange
		_ = new ParentPropertyMergeTestProperties();
		var properties = new ChildPropertyMergeTestProperties();
		properties.Compile();
		var instance = new ChildPropertyMergeTestObject();
		var property = properties.First(property => property.PropertyName == "value");

		//Act
		property.Write(instance, 7);

		//Assert
		Assert.Equal(0, instance.ParentValue);
		Assert.Equal(7, instance.ChildValue);
		Assert.Single(properties, prop => prop.PropertyName == "value");
	}

	[Fact]
	public void Given_script_property_When_object_property_is_written_with_script_variable_Then_value_is_assigned()
	{
		//Arrange
		var properties = new ObjectPropertyWriteTestProperties();
		properties.Compile();
		var instance = new ObjectPropertyWriteTestObject();
		var value    = new ScriptVariable("value");
		var property = properties.First(property => property.PropertyName == "objectvalue");

		//Act
		property.Write(instance, value);

		//Assert
		Assert.Same(value, instance.ObjectValue);
	}

	[Fact]
	public void Given_script_property_When_tstring_property_is_written_with_string_Then_value_is_assigned_as_tstring()
	{
		//Arrange
		var properties = new TStringPropertyWriteTestProperties();
		properties.Compile();
		var instance = new TStringPropertyWriteTestObject();
		var property = properties.First(property => property.PropertyName == "scriptstring");

		//Act
		property.Write(instance, "value");

		//Assert
		Assert.Equal(typeof(TString), instance.ScriptString.GetType());
		Assert.Equal("value", instance.ScriptString.ToString());
	}

	[Fact]
	public async Task Given_gui_control_profile_When_font_size_is_set_Then_text_height_uses_font_size()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				profile = new GuiControlProfile("profile");
		                          				profile.fontsize = 20;
		                          				return profile.gettextheight();
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(20.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_profile_When_normal_bitmap_is_assigned_Then_registered_property_is_available()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					normalbitmap = "gui2001_button.png";
		                          				}

		                          				return profile.normalbitmap;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("gui2001_button.png", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_profile_When_bevel_highlight_color_is_assigned_Then_registered_property_is_available()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					bevelcolorhl = "255 255 255";
		                          				}

		                          				return profile.bevelcolorhl;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("255 255 255", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_profile_When_bevel_lowlight_color_is_assigned_Then_registered_property_is_available()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					bevelcolorll = "0 0 0";
		                          				}

		                          				return profile.bevelcolorll;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0 0 0", result.GetValue()?.ToString());
	}

	[Fact]
	public void Given_gui_control_profile_When_copied_Then_justify_is_preserved()
	{
		//Arrange
		var source = new GuiControlProfile("source") { Align = "left", Justify = "center", };
		var target = new GuiControlProfile("target");

		//Act
		target.CopyFrom(source);

		//Assert
		Assert.Equal("center", target.Justify);
	}

	[Fact]
	public void Given_gui_control_profile_When_copied_Then_bevel_highlight_color_is_preserved()
	{
		//Arrange
		var source = new GuiControlProfile("source") { BevelColorHl = "255 255 255", };
		var target = new GuiControlProfile("target");

		//Act
		target.CopyFrom(source);

		//Assert
		Assert.Equal("255 255 255", target.BevelColorHl);
	}

	[Fact]
	public void Given_gui_control_profile_When_copied_Then_bevel_lowlight_color_is_preserved()
	{
		//Arrange
		var source = new GuiControlProfile("source") { BevelColorLl = "0 0 0", };
		var target = new GuiControlProfile("target");

		//Act
		target.CopyFrom(source);

		//Assert
		Assert.Equal("0 0 0", target.BevelColorLl);
	}

	[Fact]
	public async Task Given_gui_control_profile_When_use_own_profile_copies_profile_Then_normal_bitmap_is_copied()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("buttonprofile") {
		                          					normalbitmap = "gui2001_button.png";
		                          					pressedbitmap = "gui2001_button_pressed.png";
		                          				}

		                          				new GuiControl("button") {
		                          					profile = buttonprofile;
		                          					useownprofile = true;
		                          				}

		                          				return button.profile.normalbitmap;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("gui2001_button.png", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_profile_is_assigned_by_string_Then_named_profile_is_resolved()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					transparency = 0.3;
		                          				}

		                          				new GuiControl("control") {
		                          					profile = "profile";
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var control = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["control"].GetValue());
		Assert.Equal(0.3d, control.GetResolvedProfile()?.Transparency);
	}

	[Fact]
	public async Task Given_gui_control_When_use_own_profile_copies_string_profile_Then_profile_values_are_copied()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					fillcolor = { 0, 0, 0, 120 };
		                          					transparency = 0.9;
		                          				}

		                          				new GuiControl("control") {
		                          					profile = "profile";
		                          					useownprofile = true;
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var control = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["control"].GetValue());
		Assert.Equal("0,0,0,120", control.GetResolvedProfile()?.FillColor);
		Assert.Equal(0.9d, control.GetResolvedProfile()?.Transparency);
	}

	[Fact]
	public async Task Given_gui_control_When_profile_is_assigned_after_use_own_profile_Then_own_profile_is_preserved()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("control") {
		                          					useownprofile = true;
		                          					profile = MissingProfile;
		                          					profile.fonttype = "friz";
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var control = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["control"].GetValue());
		Assert.Equal("friz", control.GetResolvedProfile()?.FontType);
	}

	[Fact]
	public async Task Given_gui_control_profile_When_justify_is_set_Then_align_matches()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					justify = "center";
		                          				}

		                          				return profile.align @ "|" @ profile.justify;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("center|center", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_profile_When_align_is_set_Then_justify_matches()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					align = "right";
		                          				}

		                          				return profile.align @ "|" @ profile.justify;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("right|right", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_chained_assignment_When_value_is_assigned_Then_each_target_gets_value()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				left = right = "value";

		                          				return left @ "|" @ right;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("value|value", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_chained_member_assignment_When_value_is_assigned_Then_each_member_gets_value()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("profile") {
		                          					normalbitmap = mouseoverbitmap = "gui2001_button.png";
		                          				}

		                          				return profile.normalbitmap @ "|" @ profile.mouseoverbitmap;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("gui2001_button.png|gui2001_button.png", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_single_equals_in_nested_condition_When_compared_Then_variable_is_not_assigned()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.found = -1;

		                          	if ((temp.found = 3) != -1) {
		                          		return temp.found;
		                          	}

		                          	return 0;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(-1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_single_equals_in_while_When_variable_changes_Then_comparison_ends_loop()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.i = 0;
		                          	temp.count = 0;

		                          	while (temp.i = 0) {
		                          		temp.count++;
		                          		temp.i = 1;
		                          		if (temp.count > 5) {
		                          			return -99;
		                          		}
		                          	}

		                          	return temp.count;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_old_object_from_string_bytecode_When_targeting_this_child_Then_script_variable_is_returned()
	{
		//Arrange
		var script = CompileLegacyBytecodeScript("this.legacychild");

		//Act
		var result = await script.Call("onCreated");

		//Assert
		var legacyChild = Assert.IsType<ScriptVariable>(result.GetValue());
		Assert.Equal("legacychild", legacyChild.Name);
		Assert.True(script.ContainsVariable("legacychild"));
		Assert.Same(legacyChild, script["legacychild"].GetValue<ScriptVariable>());
	}

	[Fact]
	public async Task Given_call_arguments_When_accessing_params_identifier_Then_indexed_value_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return params[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated", "left", "right");

		//Assert
		Assert.Equal("right", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_enumerable_call_argument_When_accessing_nested_index_Then_indexed_value_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return params[0][0][1];
		                          			}
		                          """;
		var    script  = CompileScript(scriptText);
		object entries = new object?[] { new object?[] { "alpha", "beta" } };

		//Act
		var result = await script.Call("onCreated", entries);

		//Assert
		Assert.Equal("beta", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_enumerable_named_parameter_When_accessing_nested_index_Then_indexed_value_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onReceiveText(texttype, textoption, textlines) {
		                          				return texttype @ ":" @ textoption @ ":" @ textlines[0][1];
		                          			}
		                          """;
		var    script    = CompileScript(scriptText);
		object textLines = new object?[] { new object?[] { "alpha", "beta" } };

		//Act
		var result = await script.Call("onReceiveText", "lister", "simpleserverlist", textLines);

		//Assert
		Assert.Equal("lister:simpleserverlist:beta", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_switch_on_named_parameter_When_case_matches_Then_case_body_is_executed()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function handleServerListerData(textoption, textlines) {
		                          				switch (textoption) {
		                          					case "simpleserverlist":
		                          						this.received = textlines[0][1];
		                          						break;
		                          				}

		                          				return this.received;
		                          			}
		                          """;
		var    script    = CompileScript(scriptText);
		object textLines = new object?[] { new object?[] { "alpha", "beta" } };

		//Act
		var result = await script.Call("handleServerListerData", "simpleserverlist", textLines);

		//Assert
		Assert.Equal("beta", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_parameter_array_assigned_to_member_When_indexing_local_entry_Then_first_value_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function handleServerListerData(textlines) {
		                          				this.serverlistentries = textlines;
		                          				temp.entry = this.serverlistentries[0];
		                          				return temp.entry[0] @ ":" @ temp.entry[1];
		                          			}
		                          """;
		var    script    = CompileScript(scriptText);
		object textLines = new object?[] { new object?[] { "Zelda: A Link to the Past", "P Zelda: A Link to the Past", "0" } };

		//Act
		var result = await script.Call("handleServerListerData", textLines);

		//Assert
		Assert.Equal("Zelda: A Link to the Past:P Zelda: A Link to the Past", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_replaceall_helper_with_unset_found_variable_When_called_Then_loop_runs_with_zero_default()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return replaceAll("Zelda: A Link", " ", "_");
		                          			}

		                          			public function replaceAll(temp.text, temp.match, temp.replace) {
		                          				temp.prev = 0;
		                          				temp.output = "";
		                          				while (temp.found != -1) {
		                          					temp.found = indexOf(temp.text, temp.match, temp.prev);
		                          					if (temp.found == -1) {
		                          						temp.output @= temp.text.substring(int(temp.prev), -1);
		                          						return temp.output;
		                          					} else {
		                          						temp.output @= temp.text.substring(int(temp.prev), temp.found - temp.prev) @ temp.replace;
		                          						temp.prev = temp.found + temp.match.length();
		                          					}
		                          				}
		                          				return temp.output;
		                          			}

		                          			public function indexOf(temp.text, temp.match, temp.offset) {
		                          				for (temp.i = temp.offset; temp.i < temp.text.length(); temp.i ++) {
		                          					if (temp.text.substring(int(temp.i), temp.match.length()) == temp.match) {
		                          						return temp.i;
		                          					}
		                          				}
		                          				return -1;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Zelda:_A_Link", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_replaceall_helper_called_twice_When_first_call_leaves_found_minus_one_Then_second_call_uses_fresh_temp()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				replaceAll("Alpha", " ", "_");
		                          				return replaceAll("Zelda: A Link", " ", "_");
		                          			}

		                          			public function replaceAll(temp.text, temp.match, temp.replace) {
		                          				temp.prev = 0;
		                          				temp.output = "";
		                          				while (temp.found != -1) {
		                          					temp.found = indexOf(temp.text, temp.match, temp.prev);
		                          					if (temp.found == -1) {
		                          						temp.output @= temp.text.substring(int(temp.prev), -1);
		                          						return temp.output;
		                          					} else {
		                          						temp.output @= temp.text.substring(int(temp.prev), temp.found - temp.prev) @ temp.replace;
		                          						temp.prev = temp.found + temp.match.length();
		                          					}
		                          				}
		                          				return temp.output;
		                          			}

		                          			public function indexOf(temp.text, temp.match, temp.offset) {
		                          				for (temp.i = temp.offset; temp.i < temp.text.length(); temp.i ++) {
		                          					if (temp.text.substring(int(temp.i), temp.match.length()) == temp.match) {
		                          						return temp.i;
		                          					}
		                          				}
		                          				return -1;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Zelda:_A_Link", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_script_function_call_with_multiple_arguments_When_called_from_script_Then_argument_order_is_preserved()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return helper("left", "right");
		                          			}

		                          			function helper(first, second) {
		                          				return first @ ":" @ second;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("left:right", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_native_global_function_call_with_multiple_arguments_When_called_from_script_Then_argument_order_is_preserved()
	{
		//Arrange
		ScriptProperties<ScriptUniverse>.AddFunctions(null, new() { { "adventure_setserver", "", (_, args) => string.Join("|", args.Select(arg => arg.GetValue()?.ToString() ?? string.Empty)) } });
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return adventure_setserver("loginserver.graal.in", 14911);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("loginserver.graal.in|14911", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_function_switch_after_outer_condition_When_case_matches_Then_case_body_is_executed()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				if ("lister" == "lister") {
		                          					return handleServerListerData("simpleserverlist");
		                          				}

		                          				return "not-called";
		                          			}

		                          			function handleServerListerData(textoption) {
		                          				switch (textoption) {
		                          					case "simpleserverlist":
		                          						return "ok";
		                          				}

		                          				return "missing";
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("ok", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_temp_member_parameter_When_called_Then_argument_is_bound_to_member()
	{
		// Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TEMP,
				(byte)Opcode.OP_UNKNOWN_234,
				0xF0,
				0,
				(byte)Opcode.OP_FUNC_PARAMS_END,
				(byte)Opcode.OP_TEMP,
				(byte)Opcode.OP_UNKNOWN_237,
				0xF0,
				0,
			],
			["gui"]
		);

		// Act
		var result = await script.Call("onCreated", "LoginCtrl_Container");

		// Assert
		Assert.Equal("LoginCtrl_Container", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_old_params_bytecode_When_accessing_array_index_Then_indexed_value_is_returned()
	{
		//Arrange
		var script = CompileLegacyParamsBytecodeScript();

		//Act
		var result = await script.Call("onCreated", "left", "right");

		//Assert
		Assert.Equal("right", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_ref_object_When_accessing_thiso_member_Then_ref_object_member_is_returned()
	{
		//Arrange
		var refObject = new ScriptVariable("refobject");
		refObject.AddOrUpdate("marker", "from-ref".ToStackEntry());
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return thiso.marker;
		                          			}
		                          """;
		var script = CompileScript(scriptText, refObject: refObject);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("from-ref", result.GetValue()?.ToString());
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_items_Then_properly_for_loop_through_items()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.i = 0;
		                          				temp.sounds = {
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          					"text_" @ temp.i++,
		                          				};

		                          				for (temp.sound : temp.sounds) {
		                          					echo(temp.sound);
		                          				}

		                          				return "done!";
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = (await script.Call("onCreated")).GetValue<TString>();

		//Assert
		Assert.Equal("done!", result?.ToString());
		Assert.Equal(15, _calledTimes);
		Assert.Equal("text_11", _receivedStrings[3]);
		Assert.Equal("text_0", _receivedStrings[14]);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				for(this.i=0;this.i<8;this.i++) {
		                          					echo(((this.i==6)?"test2":"test") @ "_text_" @ this.i);
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		_ = await script.Call("onCreated");

		//Assert
		Assert.Equal("test_text_3", _receivedStrings[3]);
		Assert.Equal("test2_text_6", _receivedStrings[6]);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times_()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				with(test) {
		                          					width = 12;
		                          				};

		                          				echo(test.width);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		_ = await script.Call("onCreated");

		//Assert
		Assert.Equal("12", _receivedStrings[0]);
	}

	[Fact]
	public async Task Given_gui_control_When_bounds_property_is_set_Then_bounds_reads_back_from_position_and_extent()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.bounds = "2 3 40 50";
		                          				return test.bounds;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("2 3 40 50", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_width_property_is_set_below_one_Then_width_clamps_to_default_min_extent()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.width = 0;
		                          				return test.width;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(8.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_When_resize_changes_extent_Then_onresize_is_called()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                                          this.resize = "";
		                          				test = new GuiControl("test");
		                          				test.resize(2, 3, 40, 50);
		                                          return this.resize;
		                          			}

		                          			function test.onResize(width, height) {
		                                          this.resize = width @ " " @ height;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");
		await _scriptManager.DispatchPendingEvents();
		var result = script.GetVariable("resize");

		//Assert
		Assert.Equal("40 50", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_resize_changes_position_Then_onmove_is_called()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                                          this.move = "";
		                          				test = new GuiControl("test");
		                          				test.resize(2, 3, 40, 50);
		                                          return this.move;
		                          			}

		                          			function test.onMove(x, y) {
		                                          this.move = x @ " " @ y;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");
		await _scriptManager.DispatchPendingEvents();
		var result = script.GetVariable("move");

		//Assert
		Assert.Equal("2 3", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_registered_global_gui_control_When_set_size_changes_extent_Then_global_onresize_is_called()
	{
		//Arrange
		var control = new GuiControl("graalcontrol", new(_scriptManager, ScriptType.Weapon));
		_scriptManager.RegisterGlobalObject("graalcontrol", control);
		CompileScript(
			"""
						//#CLIENTSIDE
						function GraalControl.onResize(width, height) {
							resized = width @ " " @ height;
						}
			"""
		);

		//Act
		control.SetSize(300, 200);

		await _scriptManager.DispatchPendingEvents();

		//Assert
		Assert.Equal("300 200", _scriptManager.GlobalVariables["resized"].GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_registered_global_gui_control_When_notify_resize_is_called_Then_global_onresize_is_called()
	{
		//Arrange
		var control = new GuiControl("graalcontrol", new(_scriptManager, ScriptType.Weapon));
		_scriptManager.RegisterGlobalObject("graalcontrol", control);
		control.SetSize(300, 200);
		CompileScript(
			"""
						//#CLIENTSIDE
						function GraalControl.onResize(width, height) {
							resized = width @ " " @ height;
						}
			"""
		);

		//Act
		control.NotifyResize();

		await _scriptManager.DispatchPendingEvents();

		//Assert
		Assert.Equal("300 200", _scriptManager.GlobalVariables["resized"].GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_temp_member_argument_inside_gui_with_block_When_member_name_exists_on_control_Then_temp_member_is_used()
	{
		//Arrange
		ScriptProperties<ScriptMachineTests>.AddFunctions(null, new() { { "capturearg", "", (_, args) => args.Length > 0 ? args[0].GetValue()?.ToString() ?? string.Empty : string.Empty } });
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.text = "Nickname:";
		                          				new GuiControl("label") {
		                          					hint = capturearg(temp.text);
		                          					text = _(temp.text);
		                          				}

		                          				return label.hint @ "," @ label.text;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Nickname:,Nickname:", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_with_block_expression_When_current_property_is_referenced_Then_current_value_is_used()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("parent") {
		                          					height = 250;
		                          					new GuiControl("child") {
		                          						height = 26;
		                          						y = (parent.height - height) - 50;
		                          					}
		                          				}

		                          				return child.y;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(174.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_child_When_parent_extent_is_set_Then_child_width_sizing_uses_extent_delta()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				child = new GuiControl("child");
		                          				parent.extent = "100 50";
		                          				child.bounds = "10 5 20 10";
		                          				child.horizsizing = "width";
		                          				parent.addcontrol(child);
		                          				parent.extent = "150 50";
		                          				return child.width;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(70.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_When_position_uses_commas_Then_position_reads_back()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.position = "10,20";
		                          				return test.position;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("10 20", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_area_click_priority_is_above_range_Then_value_is_clamped()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.areaclickpriority = 9;
		                          				return test.areaclickpriority;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(2.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_When_local_to_global_coord_uses_parent_Then_offsets_are_applied()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				child = new GuiControl("child");
		                          				parent.position = "10 20";
		                          				child.position = "3 4";
		                          				parent.addcontrol(child);
		                          				return child.localtoglobalcoord("5 6");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("18,30", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_gui_control_When_global_to_local_coord_uses_parent_Then_offsets_are_removed()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				child = new GuiControl("child");
		                          				parent.position = "10 20";
		                          				child.position = "3 4";
		                          				parent.addcontrol(child);
		                          				return child.globaltolocalcoord("18 30");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("5,6", result.GetValue<string>());
	}

	[Theory]
	[InlineData("{5, 6}")]
	[InlineData("\"5,6\"")]
	[InlineData("\"5 6\"")]
	public async Task Gui_coordinate_functions_accept_points_and_return_indexable_points(string point)
	{
		var script = CompileScript(
			$$"""
			  //#CLIENTSIDE
			  function onCreated() {
			    parent = new GuiControl("parent");
			    child = new GuiControl("child");
			    parent.position = "10 20";
			    child.position = "3 4";
			    parent.addcontrol(child);
			    temp.globalpoint = child.localtoglobalcoord({{point}});
			    temp.localpoint = child.globaltolocalcoord(temp.globalpoint);
			    return temp.globalpoint[0] @ ":" @ temp.globalpoint[1] @ ":" @
			      temp.localpoint[0] @ ":" @ temp.localpoint[1] @ ":" @ temp.globalpoint;
			  }
			  """
		);
		var result = await script.Call("onCreated");
		Assert.Equal("18:30:5:6:18,30", result.GetValue<string>());
	}

	[Fact]
	public async Task Given_global_function_with_incompatible_receiver_When_called_inside_with_control_Then_function_is_called()
	{
		//Arrange
		ScriptProperties<IAsyncDisposable>.AddFunctions(null, new() { { "incompatibleglobalreceiver", "", (_, _) => "ok" } });

		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				with (test) {
		                          					return incompatibleglobalreceiver();
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("ok", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_gui_initializer_inside_with_When_oncreated_runs_Then_controls_are_added_to_initializer_parents()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				root = new GuiControl("root");
		                          				with (root) {
		                          					new GuiControl("parent") {
		                          						new GuiControl("child") {
		                          						}
		                          					}
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var root   = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["root"].GetValue());
		var parent = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["parent"].GetValue());
		var child  = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["child"].GetValue());
		Assert.Same(parent, Assert.Single(root.Controls));
		Assert.Same(child, Assert.Single(parent.Controls));
	}

	[Fact]
	public void Given_gui_control_showtop_When_called_Then_control_becomes_last_child()
	{
		//Arrange
		var root    = new GuiControl("root", null!);
		var window  = new GuiControl("window", null!);
		var sibling = new GuiControl("sibling", null!);
		root.AddControl(window);
		root.AddControl(sibling);

		//Act
		window.ShowTop();

		//Assert
		Assert.Same(window, root.Controls.Last());
	}

	[Fact]
	public void Given_gui_control_showtop_with_tab_child_When_called_Then_first_responder_is_propagated_to_root()
	{
		//Arrange
		var root   = new GuiControl("root", null!);
		var window = new GuiControl("window", null!);
		var child  = new GuiControl("child", null!) { Profile = new GuiControlProfile("childProfile") { Tab = true } };
		root.AddControl(window);
		window.AddControl(child);
		root.Awaken();

		//Act
		window.ShowTop();

		//Assert
		Assert.Same(child, root.FirstResponder);
	}

	[Fact]
	public async Task Given_gui_initializer_inside_with_When_named_control_property_is_read_Then_property_value_is_assigned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				container = new GuiControl("container");
		                          				container.clientwidth = 320;
		                          				container.clientheight = 240;
		                          				with (container) {
		                          					new GuiControl("screen") {
		                          						width = container.clientwidth;
		                          						height = container.clientheight;
		                          					}
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var container = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["container"].GetValue());
		var screen    = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["screen"].GetValue());
		Assert.Same(screen, Assert.Single(container.Controls));
		Assert.Equal(320, screen.Width);
		Assert.Equal(240, screen.Height);
	}

	[Fact]
	public async Task Given_named_gui_control_exists_When_created_again_inside_same_parent_Then_existing_control_is_reused()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				root = new GuiControl("root");
		                          				with (root) {
		                          					new GuiControl("container") {
		                          						width = 100;
		                          					}
		                          					new GuiControl("container") {
		                          						width = 200;
		                          					}
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var root      = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["root"].GetValue());
		var container = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["container"].GetValue());
		Assert.Same(container, Assert.Single(root.Controls));
		Assert.Equal(200, container.Width);
	}

	[Fact]
	public async Task Given_named_gui_control_is_destroyed_When_created_again_Then_new_active_control_is_created()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				root = new GuiControl("root");
		                          				with (root) {
		                          					new GuiControl("panel") {
		                          						width = 100;
		                          					}
		                          				}
		                          				panel.destroy();
		                          				with (root) {
		                          					new GuiControl("panel") {
		                          						width = 200;
		                          					}
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var root  = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["root"].GetValue());
		var panel = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["panel"].GetValue());
		Assert.Same(panel, Assert.Single(root.Controls));
		Assert.True(panel.Active);
		Assert.Equal(200, panel.Width);
	}

	[Fact]
	public async Task Given_gui_expression_When_named_control_width_is_subtracted_Then_property_value_is_used()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				container = new GuiControl("container");
		                          				container.clientwidth = 682;
		                          				panel = new GuiControl("panel");
		                          				panel.width = 500;
		                          				panel.x = (container.clientwidth - panel.width) / 2;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var panel = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["panel"].GetValue());
		Assert.Equal(91, panel.X);
	}

	[Fact]
	public async Task Given_with_control_When_instance_function_is_called_Then_function_uses_with_target()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				child = new GuiControl("child");
		                          				with (parent) {
		                          					addcontrol(child);
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var parent = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["parent"].GetValue());
		var child  = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["child"].GetValue());
		Assert.Single(parent.Controls);
		Assert.Same(child, parent.Controls.Single());
	}

	[Fact]
	public async Task Given_with_control_When_this_is_used_Then_this_resolves_to_with_target()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				with (parent) {
		                          					this.text = "current";
		                          				}

		                          				return parent.text;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("current", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_with_control_When_child_is_created_inside_with_Then_child_is_resolved_from_with_target()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				with (parent) {
		                          					child = new GuiControl("child");
		                          					addcontrol(child);
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var parent = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["parent"].GetValue());
		var child  = Assert.IsType<GuiControl>(parent.GetVariable("child").GetValue());
		Assert.Single(parent.Controls);
		Assert.Same(child, parent.Controls.Single());
	}

	[Fact]
	public async Task Given_object_created_inside_with_When_scope_ends_Then_object_name_is_global()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				with (parent) {
		                          					child = new GuiControl("child");
		                          				}
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var parent = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["parent"].GetValue());
		var child  = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["child"].GetValue());
		Assert.Same(child, parent.GetVariable("child").GetValue());
	}

	[Fact]
	public async Task Given_joined_gui_class_When_unqualified_addcontrol_is_called_Then_child_is_added_to_receiver()
	{
		//Arrange
		var canvas = new GuiControl("graalcontrol", null!);
		ScriptProperties<ScriptMachineTests>.AddFunctions(
			null,
			new()
			{
				{
					"addcontrol", "", (_, args) =>
					{
						canvas.AddControl(args.FirstOrDefault()?.GetValue<GuiControl>());
						return 0;
					}
				}
			}
		);
		const string classText = """
		                         			//#CLIENTSIDE
		                         			public function addChild(childControl) {
		                         				addcontrol(childControl);
		                         			}
		                         """;
		CompileScript(classText, "guicontrolclass");
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				child = new GuiControl("child");
		                          				parent.join("guicontrolclass");
		                          				parent.addChild(child);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		await script.Call("onCreated");

		//Assert
		var parent       = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["parent"].GetValue());
		var child        = Assert.IsType<GuiControl>(_scriptManager.GlobalVariables["child"].GetValue());
		var childControl = Assert.Single(parent.Controls);
		Assert.Same(child, childControl);
		Assert.Empty(canvas.Controls);
	}

	[Fact]
	public async Task Given_array_assigned_to_string_property_When_property_is_written_Then_array_is_script_string()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				control = new GuiControl("control");
		                          				control.position = {12, 34};
		                          				return control.position;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("12 34", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_global_properties_in_array_literal_When_assigned_to_extent_Then_property_values_are_used()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				control = new GuiControl("control");
		                          				control.extent = {screenwidth, screenheight};
		                          				return control.extent;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1024 1024", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_client_width_global_property_When_assigning_control_client_width_Then_control_property_is_written()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				control = new GuiControl("control");
		                          				control.clientwidth = screenwidth;
		                          				control.clientheight = screenheight;
		                          				return control.extent;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1024 1024", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_client_width_in_constructor_block_When_assigned_from_global_property_Then_control_property_is_written()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("control") {
		                          					clientwidth = screenwidth;
		                          					clientheight = screenheight;
		                          				}
		                          				return control.extent;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1024 1024", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_in_parent_When_maximized_is_true_Then_control_matches_parent_extent()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				parent.extent = {300, 200};
		                          				with (parent) {
		                          					new GuiControl("child") {
		                          						maximized = true;
		                          					}
		                          				}
		                          				return child.bounds;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0 0 300 200", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_in_parent_When_maximized_is_false_Then_control_keeps_default_extent()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				parent = new GuiControl("parent");
		                          				parent.extent = {300, 200};
		                          				with (parent) {
		                          					new GuiControl("child") {
		                          						maximized = false;
		                          					}
		                          				}
		                          				return child.bounds;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0 0 64 64", result.GetValue()?.ToString());
	}

	[Fact]
	public void Given_gui_control_When_controls_are_added_Then_children_keep_insertion_order()
	{
		//Arrange
		var parent = new GuiControl("parent", null!);
		var first  = new GuiControl("first", null!);
		var second = new GuiControl("second", null!);

		//Act
		parent.AddControl(first);
		parent.AddControl(second);

		//Assert
		Assert.Equal(new[] { first, second }, parent.Controls.OfType<GuiControl>().ToArray());
	}

	[Fact]
	public void Given_gui_control_When_sort_controls_is_called_Then_children_are_ordered_by_position()
	{
		//Arrange
		var parent = new GuiControl("parent", null!);
		var second = new GuiControl("second", null!) { X = 20, Y = 10 };
		var first  = new GuiControl("first", null!) { X  = 10, Y = 10 };
		parent.AddControl(second);
		parent.AddControl(first);

		//Act
		parent.SortControls();

		//Assert
		Assert.Same(first, parent.Controls.OfType<GuiControl>().First());
	}

	[Fact]
	public async Task Given_global_property_When_used_as_function_argument_Then_property_value_is_passed()
	{
		//Arrange
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { "testglobalargumentproperty", "", _ => 3.14d } });
		ScriptProperties<ScriptMachineTests>.AddFunctions(null, new() { { "readargument", "", (_, args) => args[0].GetValue()?.ToString() ?? string.Empty } });

		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return readargument(testglobalargumentproperty);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("3.14", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_global_string_property_When_compared_equal_Then_property_value_is_used()
	{
		//Arrange
		var propertyName = $"globalcomparep{Guid.NewGuid():N}";
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { propertyName, "", _ => "ready" } });
		var script = CompileScript(
			$$"""
			  //#CLIENTSIDE
			  function onCreated() {
			  	return {{propertyName}} == "ready";
			  }
			  """
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_empty_global_string_property_When_compared_not_equal_empty_Then_false_is_returned()
	{
		//Arrange
		var propertyName = $"globalcomparep{Guid.NewGuid():N}";
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { propertyName, "", _ => string.Empty } });
		var script = CompileScript(
			$$"""
			  //#CLIENTSIDE
			  function onCreated() {
			  	return {{propertyName}} != "";
			  }
			  """
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_global_string_property_When_assigned_Then_property_setter_is_called()
	{
		//Arrange
		var propertyName = $"$pref::unit::p{Guid.NewGuid():N}";
		var storedValue  = "en";
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { propertyName, "", _ => storedValue, (_, value) => storedValue = value } });
		var script = CompileScript(
			$$"""
			  //#CLIENTSIDE
			  function onCreated() {
			  	{{propertyName}} = "sv";
			  	return {{propertyName}};
			  }
			  """
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("sv", result.GetValue<string>());
		Assert.Equal("sv", storedValue);
	}

	[Fact]
	public async Task Given_global_bool_property_When_assigned_Then_property_setter_is_called()
	{
		//Arrange
		var propertyName = $"$pref::unit::p{Guid.NewGuid():N}";
		var storedValue  = false;
		ScriptProperties<ScriptMachineTests>.AddProperties(null, new() { { propertyName, "", _ => storedValue, (_, value) => storedValue = value } });
		var script = CompileScript(
			$$"""
			  //#CLIENTSIDE
			  function onCreated() {
			  	{{propertyName}} = 1;
			  	return {{propertyName}};
			  }
			  """
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
		Assert.True(storedValue);
	}

	[Fact]
	public async Task Given_gui_control_When_hide_is_called_Then_visible_is_false()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.hide();
		                          				return test.visible;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_When_color_uses_decimal_points_Then_color_reads_back_with_decimal_points()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.color = "0.5 0.25 1 0.75";
		                          				return test.color;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0.5 0.25 1 0.75", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_createanimation_is_called_Then_animation_properties_are_script_accessible()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				temp.animation = test.createanimation();
		                          				temp.animation.alpha = 0.5;
		                          				return temp.animation.alpha;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.5d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_gui_control_When_visible_is_set_false_Then_inout_animation_is_stopped()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				temp.animation = test.createanimation();
		                          				temp.animation.transition = "in";
		                          				test.visible = false;
		                          				return test.isinanimation;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(0.0d, result.GetValue<double>());
	}

	[Fact]
	public void Given_gui_control_When_createanimation_is_called_Then_control_is_visible_and_needs_repaint()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!) { Visible = false };

		//Act
		var animation = control.CreateAnimation();

		//Assert
		Assert.NotNull(animation);
		Assert.True(control.Visible);
		Assert.True(control.NeedsRepaint);
		Assert.True(control.IsInAnimation);
	}

	[Fact]
	public void Given_gui_animation_When_bounds_are_not_set_Then_owner_bounds_are_returned()
	{
		//Arrange
		var control   = new GuiControl("ctrl", null!) { Bounds = "2 3 40 50" };
		var animation = new GuiAnimation(control);

		//Assert
		Assert.Equal("2 3 40 50", animation.Bounds);
	}

	[Fact]
	public void Given_gui_control_When_stopanimations_is_called_Then_animation_state_is_cleared()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!);
		control.CreateAnimation();

		//Act
		control.StopAnimations();

		//Assert
		Assert.Empty(control.Animations);
		Assert.False(control.IsInAnimation);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times__()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				echo(""@screenwidth);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		_ = await script.Call("onCreated");

		//Assert
		Assert.Equal("1024", _receivedStrings[0]);
	}

	[Fact]
	public async Task Given_object_link_When_assigning_linked_variable_Then_original_variable_is_updated()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.items = {"a"};
		                          				temp.ref = this.items.link();
		                          				temp.ref = {"b"};
		                          				return this.items[0];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("b", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_translate_opcode_When_no_translation_exists_Then_original_string_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.text = "Connect";
		                          				return _(temp.text);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Connect", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_foreach_loop_When_loop_variable_is_assigned_Then_current_array_cell_is_updated()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				this.items = {"a", "b"};
		                          				for (temp.item: this.items) {
		                          					temp.item = temp.item @ "_x";
		                          				}
		                          				return this.items[1];
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("b_x", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_with_statement_When_target_object_is_missing_Then_body_is_skipped()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				with ("missing_object") {
		                          					return "inside";
		                          				}
		                          				return "outside";
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("outside", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_waitfor_opcode_When_event_waiting_is_unavailable_Then_zero_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				temp.done = waitfor(this, "Done", 1);
		                          				return temp.done;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_dynamic_add_opcode_When_operands_are_numbers_Then_sum_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_DYNAMIC_ADD,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("11", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_dynamic_add_opcode_When_operand_is_string_Then_values_are_concatenated()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_DYNAMIC_ADD,
			],
			["item"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("item7", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_object_compare_opcode_When_left_number_is_less_than_right_Then_minus_one_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_OBJ_COMPARE,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("-1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_object_compare_opcode_When_strings_differ_only_by_case_Then_zero_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_OBJ_COMPARE,
			],
			["Test", "test"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_equality_operator_When_string_contains_same_number_Then_values_are_equal()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "4" == 4;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_equality_operator_When_string_contains_different_number_Then_values_are_not_equal()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "4" == 5;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_less_than_operator_When_string_contains_smaller_number_Then_result_is_true()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "4" < 5;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_less_than_operator_When_string_contains_larger_number_Then_result_is_false()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return "6" < 5;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_greater_than_opcode_When_left_string_is_lexically_larger_Then_result_is_true()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_GT,
			],
			["beta", "Alpha"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_less_than_opcode_When_left_string_is_lexically_larger_Then_result_is_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_LT,
			],
			["beta", "Alpha"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_min_function_When_operands_are_strings_Then_lexically_smaller_string_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_MIN,
			],
			["beta", "Alpha"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Alpha", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_max_function_When_operands_are_strings_Then_lexically_larger_string_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_MAX,
			],
			["beta", "Alpha"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("beta", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_optimized_immediate_add_opcode_When_executed_Then_sum_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_UNKNOWN_200,
				0xF3,
				7,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("11", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_optimized_stack_and_opcode_When_right_operand_is_zero_Then_result_is_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_TRUE,
				(byte)Opcode.OP_TYPE_FALSE,
				(byte)Opcode.OP_UNKNOWN_66,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_optimized_immediate_or_opcode_When_left_is_zero_and_right_is_one_Then_result_is_true()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_FALSE,
				(byte)Opcode.OP_UNKNOWN_207,
				0xF3,
				1,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_optimized_immediate_less_than_opcode_When_left_is_greater_Then_result_is_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_UNKNOWN_224,
				0xF3,
				4,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_number_is_followed_by_add_Then_optimized_immediate_add_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_ADD,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_200, script.Bytecode[1].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[2].OpCode);
		Assert.Equal("11", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_number_is_followed_by_less_than_Then_optimized_comparison_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_LT,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_224, script.Bytecode[1].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[2].OpCode);
		Assert.Equal("0", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_immediate_add_is_assigned_Then_optimized_immediate_assignment_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_ADD,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
			],
			["counter"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_216, script.Bytecode[5].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[6].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[7].OpCode);
		Assert.Equal("11", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_stack_add_is_assigned_Then_optimized_stack_assignment_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				7,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_ADD,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
			],
			["counter", "amount"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_208, script.Bytecode[9].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[10].OpCode);
		Assert.Equal("11", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_register_opcodes_When_value_is_saved_and_loaded_Then_loaded_value_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_UNKNOWN_45,
				0xF3,
				0,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_UNKNOWN_46,
				0xF3,
				0,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("4", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_register_float_conversion_opcode_When_string_number_is_loaded_Then_number_is_returned()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_UNKNOWN_45,
				0xF3,
				0,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_UNKNOWN_228,
				0xF3,
				0,
			],
			["4.5"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("4.5", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_register_object_conversion_When_member_is_unset_Then_object_keeps_its_storage()
	{
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_THIS,
				(byte)Opcode.OP_UNKNOWN_234, 0xF0, 0,
				(byte)Opcode.OP_UNKNOWN_45, 0xF3, 0,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_UNKNOWN_230, 0xF3, 0,
				(byte)Opcode.OP_UNKNOWN_234, 0xF0, 1,
				(byte)Opcode.OP_TYPE_NUMBER, 0xF3, 1,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_UNKNOWN_230, 0xF3, 0,
			],
			["options", "enabled"]
		);

		var result  = await script.Call("onCreated");
		var options = Assert.IsType<ScriptVariable>(result.GetValue());
		Assert.Same(options, script.GetVariable("options").GetValue());
		options.AddOrUpdate("allowmass", true.ToStackEntry());
		var again = await script.Call("onCreated");
		Assert.Same(options, again.GetValue());
		Assert.True(options.GetVariable("allowmass").GetValue<bool>());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_registered_variable_is_incremented_Then_optimized_increment_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_UNKNOWN_45,
				0xF3,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_UNKNOWN_46,
				0xF3,
				0,
				(byte)Opcode.OP_INC,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
			],
			["counter"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_231, script.Bytecode[4].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[5].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[6].OpCode);
		Assert.Equal("5", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_missing_this_member_When_incrementing_Then_member_is_created_and_incremented()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	this.testcounter++;

		                          	return this.testcounter;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_temp_member_When_incrementing_Then_member_is_incremented()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.i = 0;
		                          	temp.i++;

		                          	return temp.i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_array_cell_When_incrementing_Then_cell_is_incremented()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	this.values = {0, 0};
		                          	this.values[1]++;

		                          	return this.values[1];
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.Equal(1.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_array_cell_When_decrementing_Then_cell_is_decremented()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	this.values = {1, 0};
		                          	this.values[0]--;

		                          	return this.values[0];
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var result = await script.Call("onCreated");

		Assert.Equal(0.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_with_member_When_incrementing_Then_member_is_incremented()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	new GuiControl("test") {
		                          		for (i = 0; i < 3; ++i) {
		                          		}
		                          	}

		                          	return test.i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_object_with_block_in_for_loop_When_incrementing_Then_loop_exits()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	new GuiControl("child0") {}
		                          	new GuiControl("child1") {}
		                          	new GuiControl("child2") {}

		                          	for (i = 0; i < 3; ++i) {
		                          		with ("child" @ i) {
		                          			visited = i;
		                          		}
		                          	}

		                          	return i @ "|" @ child2.visited;
		                          }
		                          """;
		var script   = CompileScript(scriptText);
		var oldDebug = Tools.DEBUG_ON;
		Tools.DEBUG_ON = false;

		try
		{
			//Act
			var result = await script.Call("onCreated");

			//Assert
			Assert.Equal("3|2", result.GetValue()?.ToString());
		}
		finally
		{
			Tools.DEBUG_ON = oldDebug;
		}
	}

	[Fact]
	public async Task Given_temp_member_exists_When_reading_and_writing_bare_name_Then_temp_member_is_used()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.i = 1;
		                          	i = 2;
		                          	i++;

		                          	return i @ "|" @ temp.i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("3|3", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_temp_alias_in_previous_call_When_calling_another_function_Then_alias_is_not_reused()
	{
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function first() {
		                          	temp.i = 3;
		                          	return i;
		                          }

		                          function second() {
		                          	return i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		var first  = await script.Call("first");
		var second = await script.Call("second");

		Assert.Equal(3, first.GetValue<double>());
		Assert.Equal(0, second.GetValue<double>());
	}

	[Fact]
	public async Task Given_no_call_arguments_When_reading_params_Then_empty_array_is_returned()
	{
		var script = CompileScript("function onCreated() { return params.size(); }");

		var result = await script.Call("onCreated");

		Assert.Equal(0, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_temp_value_in_previous_call_When_reading_temp_in_another_function_Then_value_is_not_reused()
	{
		const string scriptText = """
		                          function first() {
		                          	temp.value = 3;
		                          }

		                          function second() {
		                          	return temp.value;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		await script.Call("first");
		var result = await script.Call("second");

		Assert.Equal(0, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_global_value_in_previous_call_When_reading_name_in_another_function_Then_value_is_preserved()
	{
		const string scriptText = """
		                          function first() {
		                          	value = 3;
		                          }

		                          function second() {
		                          	return value;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		await script.Call("first");
		var result = await script.Call("second");

		Assert.Equal(3, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_object_event_called_during_execution_When_event_writes_temp_Then_caller_temp_is_unchanged()
	{
		//Arrange
		Script? script = null;
		ScriptProperties<ScriptMachineTests>.AddFunctions(
			null,
			new()
			{
				{
					"fireobjectevent", "", (_, _) =>
					{
						script!.Call("target.onAction").ConfigureAwait(false).GetAwaiter().GetResult();
						return 0;
					}
				}
			}
		);
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.value = "before";
		                          	fireobjectevent();

		                          	return temp.value;
		                          }

		                          function target.onAction() {
		                          	temp.value = "after";
		                          }
		                          """;
		script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("before", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_tmp_member_exists_When_reading_and_writing_bare_name_Then_tmp_member_is_not_used()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	tmp.i = 1;
		                          	i = 2;
		                          	i++;

		                          	return i @ "|" @ tmp.i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("3|1", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_nested_function_uses_same_temp_name_When_returning_to_caller_Then_caller_temp_is_preserved()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function helper() {
		                          	for (temp.i = 0; temp.i < 25; temp.i++) {
		                          	}
		                          }

		                          function onCreated() {
		                          	temp.rows = {"a", "b", "c"};
		                          	temp.count = 0;

		                          	for (temp.i = 0; temp.i < temp.rows.size(); temp.i++) {
		                          		helper();
		                          		temp.count++;
		                          	}

		                          	return temp.count;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(3.0d, result.GetValue<double>());
	}

	[Fact]
	public async Task Given_temp_member_exists_inside_with_When_reading_and_writing_bare_name_Then_temp_member_is_used()
	{
		//Arrange
		const string scriptText = """
		                          //#CLIENTSIDE
		                          function onCreated() {
		                          	temp.i = 1;
		                          	new GuiControl("test") {
		                          		i = 2;
		                          		i++;
		                          	}

		                          	return temp.i @ "|" @ test.i;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("3|", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_registered_variable_is_decremented_Then_optimized_decrement_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_UNKNOWN_45,
				0xF3,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				4,
				(byte)Opcode.OP_ASSIGN,
				(byte)Opcode.OP_UNKNOWN_46,
				0xF3,
				0,
				(byte)Opcode.OP_DEC,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
			],
			["counter"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_232, script.Bytecode[4].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[5].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[6].OpCode);
		Assert.Equal("3", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_registered_value_is_copied_Then_optimized_copy_is_used()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				6,
				(byte)Opcode.OP_UNKNOWN_45,
				0xF3,
				0,
				(byte)Opcode.OP_INDEX_DEC,
				(byte)Opcode.OP_UNKNOWN_46,
				0xF3,
				0,
				(byte)Opcode.OP_COPY_LAST_OP,
				(byte)Opcode.OP_ADD,
			]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_233, script.Bytecode[3].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[4].OpCode);
		Assert.Equal("12", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_variable_member_is_read_Then_cached_member_opcode_is_used()
	{
		//Arrange
		var obj = new ScriptVariable();
		obj.AddOrUpdate("value", "ok".ToStackEntry());
		RegisterGlobalObject("obj", obj);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["obj", "value"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_234, script.Bytecode[1].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[2].OpCode);
		Assert.Equal("ok", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_missing_variable_member_is_read_Then_cached_member_returns_default_value()
	{
		//Arrange
		var obj = new ScriptVariable();
		RegisterGlobalObject("obj", obj);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["obj", "missing"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_234, script.Bytecode[1].OpCode);
		Assert.Equal("", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_variable_member_is_converted_to_string_Then_cached_string_member_opcode_is_used()
	{
		//Arrange
		var obj = new ScriptVariable();
		obj.AddOrUpdate("value", 4.ToStackEntry());
		RegisterGlobalObject("obj", obj);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_MEMBER_ACCESS,
				(byte)Opcode.OP_CONV_TO_STRING,
			],
			["obj", "value"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_237, script.Bytecode[1].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[2].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[3].OpCode);
		Assert.Equal("4", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_variable_is_converted_to_object_Then_cached_object_opcode_is_used()
	{
		//Arrange
		var obj = new ScriptVariable();
		obj.AddOrUpdate("value", "ok".ToStackEntry());
		RegisterGlobalObject("obj", obj);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_CONV_TO_OBJECT,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["obj", "value"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_235, script.Bytecode[0].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[1].OpCode);
		Assert.Equal("ok", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_original_bytecode_command_call_When_arguments_are_emitted_right_to_left_Then_command_receives_source_order()
	{
		//Arrange
		_scriptManager.RegisterGlobalVariable("captureargs", (ScriptCommand)((_, args) => string.Join("|", (args ?? []).Select(arg => arg.GetValue()?.ToString() ?? string.Empty)).ToStackEntry()));
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				2,
				(byte)Opcode.OP_CALL,
			],
			["first", "second", "captureargs"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("first|second", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_original_bytecode_script_property_call_When_arguments_are_emitted_right_to_left_Then_property_receives_source_order()
	{
		//Arrange
		ScriptProperties<ScriptMachineTests>.AddFunctions(null, new() { { "capturepropertyargs", "", (_, args) => string.Join("|", args.Select(arg => arg.GetValue()?.ToString() ?? string.Empty)) }, });
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				2,
				(byte)Opcode.OP_CALL,
			],
			["first", "second", "capturepropertyargs"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("first|second", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_original_bytecode_string_method_When_calling_lowercase_Then_receiver_property_function_is_called()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_CALL,
			],
			["Login_Icon.PNG", "lowercase"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("login_icon.png", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_original_bytecode_string_method_When_calling_replaceall_Then_receiver_property_function_gets_arguments_in_source_order()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_ARRAY,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				1,
				(byte)Opcode.OP_TYPE_STRING,
				0xF0,
				2,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				3,
				(byte)Opcode.OP_CALL,
			],
			["_", " ", "Zelda: A Link", "replaceAll"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Zelda:_A_Link", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_bytecode_optimizer_When_number_is_followed_by_array_access_Then_optimized_immediate_array_access_is_used()
	{
		//Arrange
		_scriptManager.GlobalVariables.AddOrUpdate("values", new List<object?> { 1.0d, 8.0d }.ToStackEntry());
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				1,
				(byte)Opcode.OP_ARRAY,
			],
			["values"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(Opcode.OP_UNKNOWN_240, script.Bytecode[1].OpCode);
		Assert.Equal(Opcode.OP_NONE, script.Bytecode[2].OpCode);
		Assert.Equal("8", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_array_literal_When_reading_cells_Then_source_order_is_preserved()
	{
		//Arrange
		var script = CompileScript(
			"""
						//#CLIENTSIDE
						function onCreated() {
							temp.values = {"Playerworlds", "Wow", "Graal 3D", "Classics"};

							return temp.values[0] @ "|" @ temp.values[3];
						}
			"""
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Playerworlds|Classics", result.GetValue()?.ToString());
	}

	[Theory]
	[InlineData("temp.value = 0.6875; return temp.value[0];", "0.6875")]
	[InlineData("temp.value = 0.6875; return temp.value[1];", "0")]
	[InlineData("temp.value = 0.6875; return temp.value[-1];", "0")]
	[InlineData("temp.value = \"Default\"; return temp.value[0];", "Default")]
	[InlineData("temp.value = {0.6875, 1}; temp.distance = temp.value[0]; return temp.distance[0];", "0.6875")]
	[InlineData("temp.value = {}; return temp.value[0];", "0")]
	public async Task Scalar_index_zero_returns_the_scalar_without_changing_array_bounds(string body, string expected)
	{
		var script = CompileScript("function onCreated() { " + body + " }");
		Assert.Equal(expected, (await script.Call("onCreated")).GetValue<string>());
	}

	[Fact]
	public async Task Given_string_array_When_reading_out_of_range_cell_Then_empty_string_is_returned()
	{
		//Arrange
		var script = CompileScript(
			"""
						//#CLIENTSIDE
						function onCreated() {
							temp.values = {"Classics", "Playerworlds"};

							return temp.values[4] @ "Servers";
						}
			"""
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Servers", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_member_assignment_opcode_When_assigning_object_member_Then_member_is_updated()
	{
		//Arrange
		var obj = new ScriptVariable();
		RegisterGlobalObject("obj", obj);
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				9,
				(byte)Opcode.OP_UNKNOWN_54,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				0,
				(byte)Opcode.OP_TYPE_VAR,
				0xF0,
				1,
				(byte)Opcode.OP_MEMBER_ACCESS,
			],
			["obj", "value"]
		);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("9", result.GetValue()?.ToString());
	}

	[Fact]
	public void Given_fix_bad_bytecode_When_branch_target_is_past_end_Then_target_is_clamped_to_bytecode_length()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_IF,
				0xF3,
				99,
			]
		);

		//Assert
		Assert.Equal(script.Bytecode.Length, script.Bytecode[0].Value);
	}

	[Fact]
	public void Given_fix_bad_bytecode_When_branch_target_is_negative_Then_target_is_clamped_to_bytecode_length()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_IF,
				0xF3,
				unchecked((byte)-1),
			]
		);

		//Assert
		Assert.Equal(script.Bytecode.Length, script.Bytecode[0].Value);
	}

	[Fact]
	public void Given_check_only_functions_When_first_opcode_jumps_to_end_Then_has_only_functions_is_true()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_SET_INDEX,
				0xF3,
				3,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				1,
			]
		);

		//Assert
		Assert.True(script.HasOnlyFunctions);
	}

	[Fact]
	public void Given_check_only_functions_When_first_opcode_jumps_before_end_Then_has_only_functions_is_false()
	{
		//Arrange
		var script = CompileRawBytecodeScript(
			[
				(byte)Opcode.OP_SET_INDEX,
				0xF3,
				1,
				(byte)Opcode.OP_TYPE_NUMBER,
				0xF3,
				1,
			]
		);

		//Assert
		Assert.False(script.HasOnlyFunctions);
	}

	[Fact]
	public async Task Given_function_only_script_When_function_is_called_first_time_Then_function_body_runs()
	{
		//Arrange
		var script = CompileScript(
			"""
			function onCreated() {
				this.called = true;
			}
			"""
		);

		//Act
		await script.Call("onCreated");

		//Assert
		Assert.True(script.GetVariable("called").GetValue<bool>());
	}

	[Fact]
	public async Task Given_gs1_event_flag_When_matching_event_is_called_Then_whole_script_runs_with_active_event()
	{
		// Arrange
		var script = CompileScript(
			"""
			if (playerenters) {
				this.called = true;
			}
			""",
			grammar: ScriptGrammar.GS1
		);

		// Act
		await script.Call("onPlayerEnters");

		// Assert
		Assert.True(script.GetVariable("called").GetValue<bool>());
	}

	[Fact]
	public async Task Given_player_touches_me_function_When_legacy_event_name_is_called_Then_function_runs()
	{
		var script = CompileScript(
			"""
			function onPlayerTouchesMe() {
				this.called = true;
			}
			"""
		);

		await script.Call("onPlayerTouchsMe");

		Assert.True(script.GetVariable("called").GetValue<bool>());
	}

	[Fact]
	public async Task Given_legacy_player_touchs_me_function_When_corrected_event_name_is_called_Then_function_runs()
	{
		var script = CompileScript(
			"""
			function onPlayerTouchsMe() {
				this.called = true;
			}
			"""
		);

		await script.Call("onPlayerTouchesMe");

		Assert.True(script.GetVariable("called").GetValue<bool>());
	}

	[Fact]
	public async Task Given_gs1_event_flag_When_different_event_is_called_Then_whole_script_does_not_run()
	{
		// Arrange
		var script = CompileScript(
			"""
			this.ran = true;
			if (playerchats) {
				this.chatEvent = true;
			}
			""",
			grammar: ScriptGrammar.GS1
		);

		// Act
		await script.Call("onPlayerEnters");

		// Assert
		Assert.False(script.GetVariable("ran").GetValue<bool>());
	}

	[Fact]
	public async Task Given_gs1_event_flag_and_event_function_When_matching_event_is_called_Then_both_run()
	{
		// Arrange
		var script = CompileScript(
			"""
			if (playerenters) {
				this.wholeScriptCalled = true;
			}

			function onPlayerEnters() {
				this.functionCalled = true;
			}
			""",
			grammar: ScriptGrammar.GS1
		);

		// Act
		await script.Call("onPlayerEnters");

		// Assert
		Assert.True(script.GetVariable("wholescriptcalled").GetValue<bool>());
		Assert.True(script.GetVariable("functioncalled").GetValue<bool>());
	}

	[Fact]
	public async Task Given_gs1_event_flag_When_matching_event_is_called_twice_Then_whole_script_runs_twice()
	{
		// Arrange
		var script = CompileScript(
			"""
			if (playerenters) {
				this.calls++;
			}
			""",
			grammar: ScriptGrammar.GS1
		);

		// Act
		await script.Call("onPlayerEnters");
		await script.Call("onPlayerEnters");

		// Assert
		Assert.Equal(2.0d, script.GetVariable("calls").GetValue<double>());
	}

	[Fact]
	public async Task Updating_bytecode_preserves_receiver_variables_and_replaces_functions()
	{
		var script = CompileScript(
			"""
			function onCreated() { this.colours = {{255, 255, 255}, {0, 0, 255}}; this.count = 7; }
			function onTimeout() { this.count = 99; }
			"""
		);
		await script.Call("onCreated");
		var update = Interface.CompileCode("function onCreated() { this.count++; this.blue = this.colours[1][2]; }", "weapon", "testScript", withHeader: false);
		Assert.True(update.Success, update.ErrMsg);
		script.UpdateFromByteCode("testScript", update.ByteCode);
		await script.Call("onCreated");
		await script.Call("onTimeout");
		Assert.Equal(8d, script.GetVariable("count").GetValue<double>());
		Assert.Equal(255d, script.GetVariable("blue").GetValue<double>());
	}

	[Fact]
	public async Task Given_updated_script_without_previous_gs1_event_flag_When_previous_event_is_called_Then_whole_script_does_not_run()
	{
		// Arrange
		var script = CompileScript(
			"""
			if (playerenters) {
				this.initialScript = true;
			}
			""",
			grammar: ScriptGrammar.GS1
		);
		var update = Interface.CompileCode(
			"""
			this.updatedScriptRan = true;
			if (playerchats) {
				this.chatEvent = true;
			}
			""",
			"weapon",
			"testScript",
			withHeader: false,
			grammar: ScriptGrammar.GS1
		);
		Assert.True(update.Success, update.ErrMsg);
		script.UpdateFromByteCode("testScript", update.ByteCode);

		// Act
		await script.Call("onPlayerEnters");

		// Assert
		Assert.False(script.GetVariable("updatedscriptran").GetValue<bool>());
	}

	[Fact]
	public async Task Given_joined_class_with_gs1_event_flag_When_matching_event_is_called_Then_owner_whole_script_runs()
	{
		// Arrange
		CompileScript(
			"""
			if (playerenters) {
				this.classEvent = true;
			}
			""",
			"joinedclass",
			grammar: ScriptGrammar.GS1
		);
		var script = CompileScript("this.ownerScriptCalled = true;", "ownerScript", grammar: ScriptGrammar.GS1);
		script.Join("joinedclass");

		// Act
		await script.Call("onPlayerEnters");

		// Assert
		Assert.True(script.GetVariable("ownerscriptcalled").GetValue<bool>());
	}

	[Fact]
	public async Task Given_gui_control_event_helper_When_action_is_triggered_Then_script_callback_runs()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return 0;
		                          			}

		                          			function ctrl.onAction() {
		                                          this.value = 5;
		                          			}

		                          			function readValue() {
		                                          return this.value;
		                          			}
		                          """;
		var script  = CompileScript(scriptText);
		var control = new TestEventGuiControl("ctrl", script);

		//Act
		control.TriggerAction();
		await _scriptManager.DispatchPendingEvents();
		var result = await script.Call("readValue");

		//Assert
		Assert.Equal(5.0d, result.GetValue<double>());
	}

	[Fact]
	public void Given_gui_control_When_repaint_is_called_Then_repaint_state_is_set()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!);

		//Act
		control.Repaint();

		//Assert
		Assert.True(control.NeedsRepaint);
	}

	[Fact]
	public void Given_gui_control_When_startdrag_is_called_Then_drag_state_is_set()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!);

		//Act
		control.StartDrag();

		//Assert
		Assert.True(control.IsDragging);
	}

	[Fact]
	public void Given_gui_control_profile_When_preloadfont_is_called_Then_preloaded_state_is_set()
	{
		//Arrange
		var profile = new GuiControlProfile("profile");

		//Act
		profile.PreloadFont();

		//Assert
		Assert.True(profile.FontPreloaded);
	}

	[Fact]
	public void Given_gui_control_profile_When_created_Then_transparency_defaults_to_one()
	{
		//Arrange
		var profile = new GuiControlProfile("profile");

		//Assert
		Assert.Equal(1d, profile.Transparency);
	}

	[Fact]
	public void Given_unknown_profile_constructor_When_type_name_ends_with_profile_Then_profile_is_created()
	{
		//Arrange
		var script = new Script(_scriptManager, ScriptType.Weapon);

		//Act
		var created = _scriptManager.TryCreateObject("GuiBlueButtonProfile", "buttonprofile", script, out var createdObject);

		//Assert
		Assert.True(created);
		Assert.IsType<GuiControlProfile>(createdObject);
		Assert.True(_scriptManager.GlobalVariables.ContainsVariable("buttonprofile"));
	}

	[Fact]
	public void Given_profile_constructor_uses_existing_profile_name_When_created_Then_profile_values_are_copied()
	{
		//Arrange
		var script = new Script(_scriptManager, ScriptType.Weapon);
		_scriptManager.RegisterGlobalObject("baseprofile", new GuiControlProfile("baseprofile") { FontSize = 22, FillColor = "1 2 3" });

		//Act
		var created = _scriptManager.TryCreateObject("BaseProfile", "copyprofile", script, out var createdObject);

		//Assert
		Assert.True(created);
		var profile = Assert.IsType<GuiControlProfile>(createdObject);
		Assert.Equal(22, profile.FontSize);
		Assert.Equal("1 2 3", profile.FillColor);
	}

	[Fact]
	public async Task Given_gui_control_profile_constructor_When_default_profile_exists_Then_default_values_are_copied()
	{
		//Arrange
		const string scriptText = """
		                          function onCreated() {
		                          	new GuiControlProfile("GuiDefaultProfile") {
		                          		fontType = "Arial";
		                          		fontColor = "255 224 160";
		                          	}

		                          	new GuiControlProfile("childprofile") {
		                          		fontSize = 12;
		                          	}

		                          	return childprofile.fonttype @ "," @ childprofile.fontcolor @ "," @ childprofile.fontsize;
		                          }
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("Arial,255 224 160,12", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_gui_control_When_useownprofile_is_true_Then_profile_member_assignment_is_applied()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");
		                          				test.useownprofile = true;
		                          				test.profile.opaque = true;

		                          				return test.profile.opaque;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_gui_control_constructor_block_When_profile_member_is_assigned_Then_own_profile_is_updated()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("test") {
		                          					useownprofile = true;
		                          					profile.opaque = true;
		                          				}

		                          				return test.profile.opaque;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_gui_control_When_useownprofile_is_enabled_after_shared_profile_Then_profile_is_copied()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControlProfile("baseprofile") {
		                          					border = 3;
		                          				}

		                          				new GuiControl("test") {
		                          					profile = baseprofile;
		                          					useownprofile = true;
		                          					profile.border = 7;
		                          				}

		                          				return baseprofile.border @ "," @ test.profile.border;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal("3,7", result.GetValue()?.ToString());
	}

	[Fact]
	public async Task Given_isobject_When_object_exists_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");

		                          				return isObject("test");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_isobject_When_object_variable_exists_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl("test");

		                          				return isObject(test);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_isobject_When_mixed_case_object_variable_exists_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				GR_LoginScreen = new GuiControl("GR_LoginScreen");

		                          				return isObject(GR_LoginScreen);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_gui_control_constructor_block_When_parent_is_read_Then_new_root_control_has_null_parent()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("test") {
		                          					temp.result = parent == null;
		                          				}

		                          				return temp.result;
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_visible_gui_control_When_awakened_Then_onshow_is_called()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function ctrl.onShow() {
		                          				shown = true;
		                          			}
		                          """;
		var script  = CompileScript(scriptText);
		var control = new GuiControl("ctrl", script);

		//Act
		control.Awaken();

		await _scriptManager.DispatchPendingEvents();

		//Assert
		Assert.True(_scriptManager.GlobalVariables["shown"].GetValue<bool>());
	}

	[Fact]
	public async Task Given_hidden_gui_control_When_awakened_Then_onshow_is_not_called()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function ctrl.onShow() {
		                          				shown = true;
		                          			}
		                          """;
		var script  = CompileScript(scriptText);
		var control = new GuiControl("ctrl", script) { Visible = false };

		//Act
		control.Awaken();

		await _scriptManager.DispatchPendingEvents();

		//Assert
		Assert.False(_scriptManager.GlobalVariables.ContainsVariable("shown"));
	}

	[Fact]
	public void Given_gui_control_When_created_Then_native_clipping_defaults_are_used()
	{
		//Arrange
		var control = new GuiControl("test", null);

		//Assert
		Assert.True(control.ClipChildren);
		Assert.True(control.ClipMove);
		Assert.True(control.ClipToBounds);
	}

	[Fact]
	public void Given_gui_control_When_created_Then_native_move_resize_defaults_are_used()
	{
		//Arrange
		var control = new GuiControl("test", null);

		//Assert
		Assert.False(control.CanMove);
		Assert.False(control.CanResize);
	}

	[Fact]
	public async Task Given_new_gui_control_with_string_without_assignment_When_isobject_variable_is_called_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl("test");

		                          				return isObject(test);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_new_gui_control_with_variable_without_assignment_When_isobject_variable_is_called_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				new GuiControl(test);

		                          				return isObject(test);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_new_gui_control_without_name_assigned_to_variable_When_isobject_variable_is_called_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl();

		                          				return isObject(test);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_new_gui_control_without_name_assigned_to_variable_When_isobject_string_is_called_Then_true_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				test = new GuiControl();

		                          				return isObject("test");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.True(result.GetValue<bool>());
	}

	[Fact]
	public async Task Given_isobject_When_object_is_missing_Then_false_is_returned()
	{
		//Arrange
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				return isObject("missing");
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.False(result.GetValue<bool>());
	}

	[Fact]
	public void Given_gui_control_When_useownprofile_is_false_Then_profile_is_not_created()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!);

		//Assert
		Assert.False(control.UseOwnProfile);
		Assert.Null(control.Profile);
	}

	[Fact]
	public void Given_gui_control_When_child_is_inactive_Then_it_still_draws_but_invisible_child_does_not()
	{
		//Arrange
		var parent    = new GuiControl("parent", null!);
		var visible   = new CountingGuiControl("visible", null!);
		var invisible = new CountingGuiControl("invisible", null!) { Visible = false };
		var inactive  = new CountingGuiControl("inactive", null!) { Active   = false };
		parent.AddControl(visible);
		parent.AddControl(invisible);
		parent.AddControl(inactive);

		//Act
		parent.Draw();

		//Assert
		Assert.Equal(1, visible.DrawCount);
		Assert.Equal(0, invisible.DrawCount);
		Assert.Equal(1, inactive.DrawCount);
	}

	[Fact]
	public void Given_gui_control_When_adding_itself_Then_control_is_not_added()
	{
		//Arrange
		var control = new GuiControl("control", null!);

		//Act
		control.AddControl(control);

		//Assert
		Assert.Empty(control.Controls);
		Assert.Null(control.Parent);
	}

	[Fact]
	public void Given_gui_control_When_adding_ancestor_Then_control_is_not_added()
	{
		//Arrange
		var parent = new GuiControl("parent", null!);
		var child  = new GuiControl("child", null!);
		parent.AddControl(child);

		//Act
		child.AddControl(parent);

		//Assert
		Assert.Empty(child.Controls);
		Assert.Same(parent, child.Parent);
		Assert.Null(parent.Parent);
	}

	[Fact]
	public void Given_gui_control_When_parent_is_set_to_descendant_Then_parent_is_not_changed()
	{
		//Arrange
		var parent = new GuiControl("parent", null!);
		var child  = new GuiControl("child", null!);
		parent.AddControl(child);

		//Act
		parent.Parent = child;

		//Assert
		Assert.Null(parent.Parent);
		Assert.Same(parent, child.Parent);
	}

	[Fact]
	public void Given_gui_control_When_reparented_Then_old_parent_no_longer_contains_child()
	{
		//Arrange
		var oldParent = new GuiControl("oldparent", null!);
		var newParent = new GuiControl("newparent", null!);
		var child     = new GuiControl("child", null!);
		oldParent.AddControl(child);

		//Act
		newParent.AddControl(child);

		//Assert
		Assert.DoesNotContain(child, oldParent.Controls);
		Assert.Contains(child, newParent.Controls);
		Assert.Same(newParent, child.Parent);
	}

	[Fact]
	public void Given_gui_control_When_child_adds_control_during_draw_Then_new_control_is_drawn_next_frame()
	{
		//Arrange
		var parent        = new GuiControl("parent", null!);
		var lateChild     = new CountingGuiControl("latechild", null!);
		var mutatingChild = new MutatingGuiControl("mutatingchild", null!, parent, lateChild);
		parent.AddControl(mutatingChild);

		//Act
		parent.Draw();

		//Assert
		Assert.Equal(0, lateChild.DrawCount);

		//Act
		parent.Draw();

		//Assert
		Assert.Equal(1, lateChild.DrawCount);
	}

	[Fact]
	public void Given_gui_control_When_constructed_Then_default_sizing_matches_cpp_defaults()
	{
		//Arrange
		var control = new GuiControl("ctrl", null!);

		//Assert
		Assert.Equal("right", control.HorizSizing);
		Assert.Equal("bottom", control.VertSizing);
	}

	[Fact]
	public void Given_gui_control_child_When_parent_resizes_and_child_width_sizing_Then_child_width_grows_by_delta()
	{
		//Arrange
		var parent = new GuiControl("parent", null!) { Width = 100, Height = 50 };
		var child = new GuiControl("child", null!)
		{
			X           = 10,
			Y           = 5,
			Width       = 20,
			Height      = 10,
			HorizSizing = "width"
		};
		parent.AddControl(child);

		//Act
		parent.Resize(0, 0, 150, 50);

		//Assert
		Assert.Equal(70, child.Width);
	}

	[Fact]
	public void Given_gui_control_child_When_parent_set_size_and_child_width_sizing_Then_child_width_grows_by_delta()
	{
		//Arrange
		var parent = new GuiControl("parent", null!) { Width = 100, Height = 50 };
		var child = new GuiControl("child", null!)
		{
			X           = 10,
			Y           = 5,
			Width       = 20,
			Height      = 10,
			HorizSizing = "width"
		};
		parent.AddControl(child);

		//Act
		parent.SetSize(150, 50);

		//Assert
		Assert.Equal(70, child.Width);
	}

	[Fact]
	public void Given_gui_control_When_clientwidth_changes_Then_outer_width_changes_by_client_delta()
	{
		//Arrange
		var control = new InsetGuiControl("control", null!) { Width = 110, Height = 70 };

		//Act
		control.ClientWidth = 150;

		//Assert
		Assert.Equal(160, control.Width);
	}

	[Fact]
	public void Given_gui_control_child_When_parent_clientwidth_changes_Then_child_width_sizing_uses_client_delta()
	{
		//Arrange
		var parent = new InsetGuiControl("parent", null!) { Width = 110, Height = 70 };
		var child = new GuiControl("child", null!)
		{
			X           = 10,
			Y           = 5,
			Width       = 20,
			Height      = 10,
			HorizSizing = "width"
		};
		parent.AddControl(child);

		//Act
		parent.ClientWidth = 150;

		//Assert
		Assert.Equal(70, child.Width);
	}

	[Fact]
	public void Given_gui_control_When_set_size_is_called_Then_size_changes_without_moving_control()
	{
		//Arrange
		var control = new GuiControl("control", null!) { X = 3, Y = 4, Width = 1, Height = 1 };

		//Act
		control.SetSize(640, 480);

		//Assert
		Assert.Equal(3, control.X);
		Assert.Equal(4, control.Y);
		Assert.Equal(640, control.Width);
		Assert.Equal(480, control.Height);
	}

	[Fact]
	public void Given_gui_control_When_min_size_is_set_Then_min_extent_reads_same_value()
	{
		//Arrange
		var control = new GuiControl("control", null!);

		//Act
		control.MinSize = "12 13";

		//Assert
		Assert.Equal("12 13", control.MinExtent);
	}

	[Fact]
	public void Given_gui_control_When_set_size_is_below_min_extent_Then_size_is_clamped()
	{
		//Arrange
		var control = new GuiControl("control", null!) { MinExtent = "12 13" };

		//Act
		control.SetSize(1, 2);

		//Assert
		Assert.Equal(12, control.Width);
		Assert.Equal(13, control.Height);
	}

	[Fact]
	public void Given_gui_control_created_by_script_When_checking_script_owner_Then_only_that_script_matches()
	{
		//Arrange
		var script      = new Script(_scriptManager, ScriptType.Weapon);
		var otherScript = new Script(_scriptManager, ScriptType.Weapon);
		var control     = new GuiControl("control", script);

		//Act
		var isOwnedByScript      = control.IsOwnedBy(script);
		var isOwnedByOtherScript = control.IsOwnedBy(otherScript);

		//Assert
		Assert.True(isOwnedByScript);
		Assert.False(isOwnedByOtherScript);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times___()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				echo(1.01+screenwidth);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		_ = await script.Call("onCreated");

		//Assert
		Assert.Equal("1025.01", _receivedStrings[0]);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times____()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText = """
		                          			//#CLIENTSIDE
		                          			function onCreated() {
		                          				showimg(1402, "cog2.png", 85, 60);
		                          				findimg(1402).rotation = 234;
		                          				temp.rot2 = findimg(1402).rotation;
		                          				echo(temp.rot2);
		                          			}
		                          """;
		var script = CompileScript(scriptText);

		//Act
		_ = await script.Call("onCreated");

		//Assert
		Assert.Equal("234", _receivedStrings[0]);
	}

	private sealed class TestEventGuiControl(string id, Script script) : GuiControl(id, script)
	{
		public void TriggerAction() => CallAction();
	}

	private sealed class CountingGuiControl(string id, Script script) : GuiControl(id, script)
	{
		public int DrawCount { get; private set; }

		public override void Draw() => DrawCount++;
	}

	private sealed class MutatingGuiControl(string id, Script script, GuiControl parent, GuiControl childToAdd) : GuiControl(id, script)
	{
		public override void Draw() => parent.AddControl(childToAdd);
	}

	private sealed class InsetGuiControl(string id, Script script) : GuiControl(id, script)
	{
		protected override (int Width, int Height) GetClientSizeForBounds(int width, int height) => (Math.Max(0, width - 10), Math.Max(0, height - 20));
	}

	private class ParentPropertyMergeTestObject
	{
		public int ParentValue { get; set; }
	}

	private sealed class ChildPropertyMergeTestObject : ParentPropertyMergeTestObject
	{
		public int ChildValue { get; set; }
	}

	private sealed class ParentPropertyMergeTestProperties : ScriptProperties<ParentPropertyMergeTestObject>
	{
		public ParentPropertyMergeTestProperties() : base(null)
		{
			AddProperties(this, new() { { "value", "", value => value.ParentValue, (value, propertyValue) => value.ParentValue = propertyValue } });
		}
	}

	private sealed class ChildPropertyMergeTestProperties : ScriptProperties<ChildPropertyMergeTestObject>
	{
		public ChildPropertyMergeTestProperties() : base(typeof(ParentPropertyMergeTestObject))
		{
			AddProperties(this, new() { { "value", "", value => value.ChildValue, (value, propertyValue) => value.ChildValue = propertyValue } });
		}
	}

	private sealed class ObjectPropertyWriteTestObject
	{
		public object? ObjectValue { get; set; }
	}

	private sealed class ObjectPropertyWriteTestProperties : ScriptProperties<ObjectPropertyWriteTestObject>
	{
		public ObjectPropertyWriteTestProperties() : base(null)
		{
			AddProperties(this, new() { { "objectvalue", "", value => value.ObjectValue, (value, propertyValue) => value.ObjectValue = propertyValue } });
		}
	}

	private sealed class TStringPropertyWriteTestObject
	{
		public TString ScriptString { get; set; } = string.Empty;
	}

	private sealed class TStringPropertyWriteTestProperties : ScriptProperties<TStringPropertyWriteTestObject>
	{
		public TStringPropertyWriteTestProperties() : base(null)
		{
			AddProperties(this, new() { { "scriptstring", "", value => value.ScriptString, (value, propertyValue) => value.ScriptString = propertyValue } });
		}
	}

	private sealed class MemberFunctionChildObject(string name) : ScriptVariable(name)
	{
		public new static readonly MemberFunctionChildProperties PropertiesInstance = [];
		public override            IScriptProperties             Properties => PropertiesInstance;

		public bool Called { get; set; }
	}

	private sealed class MemberFunctionChildProperties : ScriptProperties<MemberFunctionChildObject>
	{
		public MemberFunctionChildProperties() : base(typeof(ScriptVariable))
		{
			AddProperties(this, new() { { "called", "", value => value.Called } });
			AddFunctions(
				this,
				new()
				{
					{
						"mark", "", (value, _) =>
						{
							value.Called = true;
							return 0;
						}
					}
				}
			);
			Compile();
		}
	}

	private sealed class ReceiverPropertyScript : Script
	{
		public new static readonly ReceiverPropertyScriptProperties PropertiesInstance = [];
		public override            IScriptProperties                Properties => PropertiesInstance;

		public ReceiverPropertyScript(IScriptManager scriptManager, byte[] bytecode) : base(scriptManager, "receiver", bytecode, type: ScriptType.LevelNpc)
		{
		}

		public double Position { get; set; }
	}

	private sealed class ReceiverPropertyScriptProperties : ScriptProperties<ReceiverPropertyScript>
	{
		public ReceiverPropertyScriptProperties() : base(typeof(Script))
		{
			AddProperties(this, new() { { "position", "", script => script.Position, (script, value) => script.Position = value }, });
			Compile();
		}
	}
}