using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public sealed class ScriptPropertyMetadataTests
{
	[Fact]
	public void Parameters_Given_typed_function_When_read_Then_returns_registered_parameters()
	{
		//Arrange
		var function = Script.PropertiesInstance.Single(property => property.PropertyName == "settimer");

		//Act
		var actual = function.Parameters;

		//Assert
		Assert.Equal([new("delay", typeof(double))], actual);
	}

	[Fact]
	public void Parameters_Given_variable_When_read_Then_returns_empty_collection()
	{
		//Arrange
		var property = Script.PropertiesInstance.Single(property => property.PropertyName == "joinedclasses");

		//Act
		var actual = property.Parameters;

		//Assert
		Assert.Empty(actual);
	}

	[Fact]
	public void ReturnType_Given_void_sentinel_function_When_read_Then_returns_void_type()
	{
		//Arrange
		var function = Script.PropertiesInstance.Single(property => property.PropertyName == "settimer");

		//Act
		var actual = function.ReturnType;

		//Assert
		Assert.Equal(typeof(void), actual);
	}

	[Fact]
	public void ReturnType_Given_integer_function_When_read_Then_returns_integer_type()
	{
		//Arrange
		var function = GuiControlProfile.PropertiesInstance.Single(property => property.PropertyName == "gettextwidth");

		//Act
		var actual = function.ReturnType;

		//Assert
		Assert.Equal(typeof(int), actual);
	}
}