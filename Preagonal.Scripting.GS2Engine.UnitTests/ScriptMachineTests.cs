using System.Collections.Concurrent;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;
using Preagonal.Scripting.GS2Engine.UnitTests.Objects;
using Xunit.Abstractions;
using static Preagonal.Scripting.GS2Engine.GS2.Script.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptMachineTests
{
	private          int                     _calledTimes;
	private readonly Dictionary<int, string> _receivedStrings = new();

	public ScriptMachineTests(ITestOutputHelper testOutputHelper)
	{
		Tools.SetDebugFuncWrite(testOutputHelper.WriteLine);
		Tools.SetDebugFuncWriteLine(testOutputHelper.WriteLine);
		Tools.DEBUG_ON = true;

		ConcurrentDictionary<int, Drawing> drawings = new();
		ScriptProperties<ScriptMachineTests>.AddProperties(
			null,
			new()
			{
				{ "screenwidth", "The width of the game screen", _ => 1024 },
				{ "screenheight", "The height of the game screen", _ => 1024 },
			}
		);

		ScriptProperties<ScriptMachineTests>.AddFunctions(
			null,
			new()
			{
				{
					"echo",
					"",
					EchoCallback
				},
				{
					"showimg",
					"",
					(_, args) =>
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
					"findimg",
					"",
					(_, args) =>
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
					"getimgwidth",
					"",
					(_, args) =>
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

		foreach (var property in GlobalProperties.Where(x => !x.Value.Compiled))
		{
			property.Value.Compile();
		}
	}

	private int EchoCallback(ScriptMachineTests _, IStackEntry[] args)
	{
		_receivedStrings[_calledTimes] = args[0]?.GetValue()?.ToString()??"";

		Console.WriteLine(_receivedStrings[_calledTimes]);

		_calledTimes++;

		return 0;
	}

	private static Script CompileScript(string scriptText)
	{
		var response = GS2Compiler.Interface.CompileCode(
			scriptText,
			"weapon",
			"testScript"
		);

		if (response.Success)
		{
			// Arrange
			return new("testScript", response.ByteCode);
		}

		throw new($"Script failure: {response.ErrMsg}");
	}

	private static Script InitializePrebakedScript(string fileName) => new(fileName);

	[Fact]
	public void When_script_is_faulty_Then_exception_is_thrown()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() 
						}
			""";


		//Act
		var result = Assert.Throws<Exception>(() => CompileScript(scriptText));;

		//Assert
		Assert.Equal("Script failure: malformed input at line 3: \t\t\t}\n", result.Message);
	}

	private static void RegisterGlobalObject(string name, ScriptVariable collection) => GlobalObjects[name] = collection;

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
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

	[Fact]
	public async Task When_calling_built_in_sin_Then_correct_sin_value_is_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		var expectedSin = new List<double> { 1, 0, -1, 0 };
		var sin         = new List<double>();
		const string scriptText =
			"""
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
		const string scriptText =
			"""
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
	public async Task Given_temp_var_When_returning_without_temp_prefix_Then_temp_var_should_be_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes   = 0;
		const string scriptText =
			"""
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
		_calledTimes   = 0;
		const string scriptText1 =
			"""
						//#CLIENTSIDE
						public function PubFun() {
							temp.var = "PubFun";
							
							temp.var2 = var;
							
							return var2;
						}
			""";
		var script1 = CompileScript(scriptText1);
		const string scriptText2 =
			"""
						//#CLIENTSIDE
						function onCreated() {
							return (@"script1").PubFun();
						}
			""";
		var script2 = CompileScript(scriptText2);

		GlobalVariables["script1"] = script1.ToStackEntry();

		//Act
		var result = await script2.Call("onCreated");

		//Assert
		Assert.Equal("PubFun", result.GetValue()!.ToString());
	}

	[Fact]
	public async Task Given_this_var3_When_returning_without_this_prefix_Then_0_should_be_returned()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes   = 0;
		const string scriptText =
			"""
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
		_calledTimes   = 0;
		GlobalVariables.Clear();
		const string scriptText =
			"""
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
		Assert.Equal(true, result.GetValue()!);
	}

	[Fact(Skip = "Waiting for fix in GS2Compiler")]
	public async Task Given_temp_update_When_comparing_multiple_or_and_one_variable_is_updated_Then_result_should_be_true()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes   = 0;
		GlobalVariables.Clear();
		GlobalObjects.Clear();
		const string scriptText =
			"""
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
		_calledTimes   = 0;
		GlobalVariables.Clear();
		const string scriptText =
			"""
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
		_calledTimes   = 0;
		const string scriptText =
			"""
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
		Assert.Equal(true, result.GetValue()!);
	}

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_items_Then_properly_for_loop_through_items()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
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
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times_()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
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

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times__()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
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

	[Fact(Skip = "fix later")]
	public async Task When_for_loop_with_8_loops_Then_echo_is_called_8_times___()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
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
		const string scriptText =
			"""
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
}