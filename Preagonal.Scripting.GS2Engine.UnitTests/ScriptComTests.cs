using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptComTests
{
	[Fact]
	public void Given_variable_name_When_normalized_repeatedly_Then_cached_value_is_returned()
	{
		var command = new ScriptCom { VariableName = "Player.Account" };

		var first  = command.NormalizedVariableName;
		var second = command.NormalizedVariableName;

		Assert.Equal("player.account", first?.ToString());
		Assert.Same(first, second);
	}

	[Fact]
	public void Given_cached_variable_name_When_name_changes_Then_normalized_value_is_refreshed()
	{
		var command = new ScriptCom { VariableName = "First" };
		_ = command.NormalizedVariableName;

		command.VariableName = "Second";

		Assert.Equal("second", command.NormalizedVariableName?.ToString());
	}
}