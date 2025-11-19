using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class StackEntryExtensionsTests
{
	[Fact]
	public void When_input_is_int_Then_return_StackEntry_with_type_number_and_value_type_double()
	{
		//Arrange
		const int val = 1;

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Number, test.Type);
		Assert.Equal(typeof(double), test.GetValue()?.GetType());
		Assert.Equal((double)val, test.GetValue());
	}

	[Fact]
	public void When_input_is_double_Then_return_StackEntry_with_type_number_and_value_type_double()
	{
		//Arrange
		const double val = 1.23d;

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Number, test.Type);
		Assert.Equal(typeof(double), test.GetValue()?.GetType());
		Assert.Equal((double)val, test.GetValue());
	}

	[Fact]
	public void When_input_is_decimal_Then_return_StackEntry_with_type_number_and_value_type_double()
	{
		//Arrange
		decimal val = new(1.23);

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Number, test.Type);
		Assert.Equal(typeof(double), test.GetValue()?.GetType());
		Assert.Equal((double)val, test.GetValue());
	}

	[Fact]
	public void When_input_is_float_Then_return_StackEntry_with_type_number_and_value_type_double()
	{
		//Arrange
		const float val = 1.23f;

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Number, test.Type);
		Assert.Equal(typeof(double), test.GetValue()?.GetType());
		Assert.Equal((double)val, test.GetValue());
	}

	[Fact]
	public void When_input_is_string_Then_return_StackEntry_with_type_string_and_value_type_TString()
	{
		//Arrange
		const string val = "test";

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.String, test.Type);
		Assert.Equal(typeof(TString), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue()?.ToString());
	}

	[Fact]
	public void When_input_is_TString_Then_return_StackEntry_with_type_string_and_value_type_TString()
	{
		//Arrange
		TString val = "test";

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.String, test.Type);
		Assert.Equal(typeof(TString), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue()?.ToString());
	}

	[Fact]
	public void When_input_is_string_array_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		string[] val = ["test1","test2"];

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Array, test.Type);
		Assert.Equal(typeof(List<string>), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_int_array_Then_return_StackEntry_with_type_array_and_value_type_List_int()
	{
		//Arrange
		int[] val = [1,2];

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Array, test.Type);
		Assert.Equal(typeof(List<int>), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_command_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		Script.Command val = (_, _) => 0.ToStackEntry();

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Function, test.Type);
		Assert.Equal(typeof(Script.Command), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_guicontrol_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		IGuiControl val = new GuiControl("", null);

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Array, test.Type);
		Assert.Equal(typeof(GuiControl), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_variablecollection_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		VariableCollection val = new();

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Array, test.Type);
		Assert.Equal(typeof(VariableCollection), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_typeof_list_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		List<object> val = new();

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Array, test.Type);
		Assert.Equal(typeof(List<object>), test.GetValue()?.GetType());
		Assert.Equal(val, test.GetValue());
	}

	[Fact]
	public void When_input_is_outofrange_Then_return_StackEntry_with_type_array_and_value_type_List_string()
	{
		//Arrange
		Thread val = new(_ =>{} );

		//Act
		//Assert
		var exception = Assert.Throws<ArgumentOutOfRangeException>(() => val.ToStackEntry());
		Assert.Equal("Specified argument was out of the range of valid values. (Parameter 'stackObject')", exception.Message);
	}

	[Fact]
	public void When_input_is_bool_Then_return_StackEntry_with_type_bool_and_value_type_bool()
	{
		//Arrange
		const bool val = true;
		const double expected = 1.0d;

		//Act
		var test = val.ToStackEntry();

		//Assert
		Assert.Equal(StackEntryType.Boolean, test.Type);
		Assert.Equal(typeof(double), test.GetValue()?.GetType());
		Assert.Equal(expected, test.GetValue());
	}
}