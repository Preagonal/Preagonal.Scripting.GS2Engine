using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Compiler;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public sealed class BytecodeBranchOptimizationTests
{
	[Theory]
	[InlineData("true", "temp.a - temp.b", 17)]
	[InlineData("false", "temp.a - temp.b", 5)]
	[InlineData("true", "temp.a - 3", 17)]
	[InlineData("false", "temp.a - 3", 5)]
	[InlineData("true", "temp.a + temp.b", 17)]
	[InlineData("false", "temp.a + temp.b", 11)]
	[InlineData("true", "temp.a * 3", 17)]
	[InlineData("false", "temp.a * 3", 24)]
	public async Task Conditional_assignment_preserves_shared_assignment_target(string condition, string expression, double expected)
	{
		//Arrange
		var manager  = new ScriptManager(new FakeLogger<ScriptManager>());
		var compiled = Interface.CompileCode($"function onCreated() {{ temp.a = 8; temp.b = 3; temp.value = {condition} ? 17 : {expression}; return temp.value; }}", withHeader: false);
		var script   = new Script(manager, "conditional", compiled.ByteCode);

		//Act
		var result = await script.Call("onCreated");

		//Assert
		Assert.Equal(expected, result.GetValue<double>());
	}
}