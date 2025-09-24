using System.Collections.Concurrent;
using System.Reflection;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.TestApp.Objects;
using Xunit;

namespace Preagonal.Scripting.GS2Engine.TestApp;

internal static class Program
{
	private static async Task Main(string[] args)
	{
		/*
	HashSet<Script> scripts = new();
	foreach (string file in Directory.GetFiles($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}scripts"))
	{
		Console.WriteLine($"File: {file}");
		scripts.Add(new Script(file, null, null, null));
	}

	while (true)
	{
		foreach (Script script in scripts) await script.TriggerEvent("onTimeout");

		Thread.Sleep(10);
	}
	*/
		var calledTimes = 0;
		var receivedStrings = new Dictionary<int, string>();
		var path = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}scripts/test.gs2bc";

		/*
		Command echoCommand = delegate (ScriptMachine machine, IStackEntry[]? args)
		{
			if (!(args?.Length > 0)) return 0.ToStackEntry();

			receivedStrings[calledTimes] = machine.GetEntry(args[0]).GetValue()?.ToString() ?? "";

			Console.WriteLine(receivedStrings[calledTimes]);

			calledTimes++;

			return 0.ToStackEntry();
		};

		GlobalFunctions.AddOrUpdate(
			"echo",
			echoCommand,
			(_, _) => echoCommand
		);
		*/

		ConcurrentDictionary<int, Drawing?> Drawings = new();

		/*
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
		*/

		const string scriptText =
			"""
						//#CLIENTSIDE
						function onCreated() {
							this.i = 0;
							setTimer(0.01);
						}
						
						function onTimeout() {
							echo(this.i);
							Main();
							
							this.i++;
							
							if (this.i < 3500) {
								setTimer(0.01);
							}
						}
						
						function Main() {
							Movement();
						}
						
						function Movement() {
							for (temp.k=0;temp.k<4;temp.k++) {
								echo("temp.k:" SPC temp.k);
							}
						}
			""";
		var response = GS2Compiler.Interface.CompileCode(
			scriptText,
			"weapon",
			"testScript"
		);

		if (response.Success)
		{
			Tools.DEBUG_ON = false;

			// Arrange
			var script = new Script("testScript", response.ByteCode);

			// Act
			await script.Call("onCreated");

			// Assert
			while (true)
			{
				if (!(script.GetVariable("i").GetValue<double>() >= 3000)) continue;

				Assert.Equal(8, calledTimes);
				break;
			}


			await script.Call("myFunction", "test", 1, true);
			await script.Call("myFunction", "test", 1, false);
			await script.Call(
				"myFunction2",
				"test",
				-0xFF,
				false,
				1.345,
				true
			);
		}
	}
}