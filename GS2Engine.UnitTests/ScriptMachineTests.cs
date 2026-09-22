using System.Collections.Concurrent;
using System.Reflection;
using GS2Engine.Enums;
using GS2Engine.Extensions;
using GS2Engine.GS2.Script;
using GS2Engine.Models;
using GS2Engine.UnitTests.Objects;
using static GS2Engine.GS2.Script.Script;

namespace GS2Engine.UnitTests;

public class ScriptMachineTests
{
	private          int                     _calledTimes;
	private readonly Dictionary<int, string> _receivedStrings = new();

	public ScriptMachineTests()
	{
		Command echoCommand = delegate (ScriptMachine machine, IStackEntry[]? args)
		{
			if (args?.Length > 0)
			{
				_receivedStrings[_calledTimes] = machine.GetEntry(args[0]).GetValue()?.ToString() ?? "";

				Console.WriteLine(_receivedStrings[_calledTimes]);

				_calledTimes++;
			}

			return 0.ToStackEntry();
		};

		GlobalFunctions.AddOrUpdate(
			"echo",
			echoCommand,
			(_, _) => echoCommand
		);
	}

	private static Script InitializeScript(string scriptText)
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

		throw new("Script failure");
	}

	[Fact]
	public void When_for_loop_with_8_loops_Then_echo_is_called_8_times()
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
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(8, _calledTimes);
		Assert.Equal("test_text_3", _receivedStrings[3]);
		Assert.Equal("test2_text_6", _receivedStrings[6]);
	}

	[Fact]
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
		var script = InitializeScript(scriptText);

		//Act
		var result = (await script.Call("onCreated")).GetValue<TString>();

		//Assert
		Assert.Equal("done!", result?.ToString());
		Assert.Equal(15, _calledTimes);
		Assert.Equal("text_11", _receivedStrings[3]);
		Assert.Equal("text_0", _receivedStrings[14]);
	}

	[Fact]
	public async Task When_for_loop_with_images_Then_properly_for_loop_through_images()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;

		ConcurrentDictionary<int, Drawing?> Drawings = new();

		Command showimgCommand = delegate(ScriptMachine machine, IStackEntry[]? args)
		{
			if (!(args?.Length > 3)) return 0.ToStackEntry();
			try
			{
				var     index = (int)machine.GetEntry(args[0]).GetValue<double>();
				string? image = machine.GetEntry(args[1]).GetValue<TString>() ?? string.Empty;

				var x = (int)machine.GetEntry(args[2]).GetValue<double>();
				var y = (int)machine.GetEntry(args[3]).GetValue<double>();
				if (Drawings.TryGetValue(index, out var value))
				{
					value?.ShowImg(image, x, y);
				}
				else
				{
					value = new(image, x, y);
					Drawings.AddOrUpdate(index, value, (_, _) => value);
				}
			}
			catch (Exception e)
			{
				//_logger.LogDebug(e.Message);
			}

			return 0.ToStackEntry();
		};

		GlobalFunctions.AddOrUpdate(
			"showimg",
			showimgCommand,
			(_, _) => showimgCommand
		);

		Command findimgCommand = delegate(ScriptMachine machine, IStackEntry[]? args)
		{
			if (!(args?.Length > 0)) return 0.ToStackEntry();
			try
			{
				var index = (int)machine.GetEntry(args[0]).GetValue<double>();

				if (Drawings.TryGetValue(index, out var value))
				{
					return value!.ToStackEntry();
				}
			}
			catch (Exception e)
			{
				//_logger.LogDebug(e.Message);
			}

			return 0.ToStackEntry();
		};

		GlobalFunctions.AddOrUpdate(
			"findimg",
			findimgCommand,
			(_, _) => findimgCommand
		);

		Command getimgwidth = delegate(ScriptMachine machine, IStackEntry[]? args)
		{
			if (!(args?.Length > 0)) return 0.ToStackEntry();
			try
			{
				var image = machine.GetEntry(args[0]).GetValue<TString>();

				Console.WriteLine(image);

				return 1!.ToStackEntry();

			}
			catch (Exception e)
			{
				//_logger.LogDebug(e.Message);
			}

			return 0.ToStackEntry();
		};

		GlobalFunctions.AddOrUpdate(
			"getimgwidth",
			getimgwidth,
			(_, _) => getimgwidth
		);
		const string scriptText =
			"""
							//#CLIENTSIDE
							function onCreated() {
								temp.images = {
										"eye_platdownload.gif",
										"sign1.gif",
										"eye_giantbomb.png",
										"eye_platloading.gif",
										"sen_piano.png",
										"sen_tileset_0.png",
										"sen_tileset_1.png",
										"sen_tileset_2.png",
										"sen_tileset_3.png",
										"sen_tileset_4.png",
										"sen_tileset_5.png",
										"sen_tileset_6.png",
										"sen_tileset_7.png",
										"bluelampani2.gif",
										"koni_bomber_vpieces.gif",
										//"pics1.png",
										//"eye_bomber_choc.png",
										"eye_bomber_pcur.png",
										"eye_bomber_pgui.png",
										"eye_bomber_poni.png",
										"eye_bomber_pqui.png",
										"eye_bombsprites-body.png",
										"eye_bombsprites-dec0.png",
										"eye_bombsprites-dec1.png",
										"eye_bombsprites-dec2.png",
										"eye_bombsprites-dec3.png",
										"eye_bombsprites-dec4.png",
										"eye_bombsprites-dec5.png",
										"eye_bombsprites-dec6.png",
										"eye_bombsprites-dec7.png",
										"eye_bombsprites-dec8.png",
										"eye_bombsprites-fire.png",
										"eye_bombsprites-fuse.png",
										"cadavrezcog2.png",
										"koni_vase.png",
										"eye_p1a.png",
										"eye_p1b.png",
										"eye_p1c.png",
										"eye_p1d.png",
										"eye_p1e.png",
										"eye_p1f.png",
										"eye_p1g.png",
										"eye_p1h.png",
										"eye_p1i.png",
										"eye_p1j.png",
										"eye_p1k.png",
										"eye_p1l.png",
										"eye_bomber_coin.png",
										"eye_bomber_coinbag.png",
										"eye_bomber_notice2.png",
										"eye_bomber_notice.png",
										"bmb_pics1.png"
									};
			
									this.tokenscount = temp.images.size();
									echo(this.tokenscount);
									for(this.img=0; this.img < this.tokenscount; this.img++) {
										echo(temp.images[this.img]);
										if(this.img>2) echo("DrawBar()");
										while(getimgwidth(temp.images[this.img])==0) sleep(0.01);
										hideimg(500);
										if(this.img%300==0) sleep(0.01);
										echo(temp.images[this.img]);
									}
							}
			""";
		var script = InitializeScript(scriptText);

		//Act
		var result = (await script.Call("onCreated")).GetValue<double>();

		//Assert
		Assert.Equal(0, result);
		Assert.Equal(148, _calledTimes);
		Assert.Equal("sign1.gif", _receivedStrings[3]);
		Assert.Equal("bmb_pics1.png", _receivedStrings[147]);
	}

	[Fact]
	public void When_bitwise_and_Then_returns_correct_result()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							echo(1024 & 8191);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("1024", _receivedStrings[0]);
	}

	[Fact]
	public void When_bitwise_or_Then_returns_correct_result()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							echo(1024 | 3);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("1027", _receivedStrings[0]);
	}

	[Fact]
	public void When_bitwise_xor_Then_returns_correct_result()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							echo(1024 xor 3);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("1027", _receivedStrings[0]);
	}

	[Fact]
	public void When_shift_right_Then_returns_correct_result()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							echo(16711680 >> 16);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("255", _receivedStrings[0]);
	}

	[Fact]
	public void When_shift_left_Then_returns_correct_result()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							echo(1 << 8);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("256", _receivedStrings[0]);
	}

	[Fact]
	public void When_color_unpack_Then_rgb_components_correct()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							color = 16711680;
							echo(((color >> 16) & 255) / 255);
							echo(((color >> 8) & 255) / 255);
							echo((color & 255) / 255);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(3, _calledTimes);
		Assert.Equal("1", _receivedStrings[0]);
		Assert.Equal("0", _receivedStrings[1]);
		Assert.Equal("0", _receivedStrings[2]);
	}

	[Fact]
	public void When_nested_array_assign_Then_inner_array_readable()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							mem = new[16];
							mem[0] = new[8192];
							inner = mem[0];
							v = inner[1024];
							echo(v);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("0", _receivedStrings[0]);
	}

	[Fact]
	public void When_nested_array_write_read_Then_value_roundtrips()
	{
		//Arrange
		_receivedStrings.Clear();
		_calledTimes = 0;
		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							mem = new[16];
							mem[0] = new[8192];
							inner = mem[0];
							inner[1024] = 42;
							echo(mem[0][1024]);
						}
			""";
		var script = InitializeScript(scriptText);

		//Act
		_ = script.Call("onCreated");

		//Assert
		Assert.Equal(1, _calledTimes);
		Assert.Equal("42", _receivedStrings[0]);
	}

}
