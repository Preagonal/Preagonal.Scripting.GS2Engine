using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptPropertiesTests
{
	[Fact]
	public void Given_registered_property_When_looked_up_with_different_case_Then_property_is_returned()
	{
		var result = Script.PropertiesInstance.TryGetProperty("SETTIMER", out var property);

		Assert.True(result);
		Assert.Equal("settimer", property.PropertyName);
	}

	[Fact]
	public void Given_unknown_property_When_looked_up_Then_false_is_returned()
	{
		var result = Script.PropertiesInstance.TryGetProperty("not-a-script-property", out _);

		Assert.False(result);
	}
}