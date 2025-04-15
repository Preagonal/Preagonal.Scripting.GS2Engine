using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class StackExtensionsTests
{
	[Fact]
	public void When_input_is_int_Then_return_StackEntry_with_type_number_and_value_type_double()
	{
		//Arrange
		var stack = new Stack<StackEntry>();
		stack.Push("asd".ToStackEntry());

		//Act
		var newStack = stack.Clone();

		//Assert
		Assert.Equal(stack.Peek(), newStack.Peek());
	}
}