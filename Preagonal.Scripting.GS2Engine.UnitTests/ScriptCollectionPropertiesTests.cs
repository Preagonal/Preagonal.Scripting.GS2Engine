using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptCollectionPropertiesTests
{
	[Fact]
	public void Call_Given_valid_add_arguments_When_called_Then_returns_created_entry()
	{
		//Arrange
		var expected   = new object();
		var collection = CreateCollection(addArgumentCount: 1, add: _ => expected);
		var function   = GetFunction("add");

		//Act
		var actual = function.Call(collection, "entry".ToStackEntry());

		//Assert
		Assert.Same(expected, actual);
	}

	[Fact]
	public void Call_Given_valid_add_arguments_When_called_Then_passes_argument_values()
	{
		//Arrange
		IReadOnlyList<object?>? actual = null;
		var collection = CreateCollection(
			addArgumentCount: 2,
			add: arguments =>
			{
				actual = arguments;
				return null;
			}
		);
		var function = GetFunction("add");

		//Act
		function.Call(collection, 12.0d.ToStackEntry(), "text".ToStackEntry());

		//Assert
		Assert.Equal([12.0d, (TString)"text"], actual);
	}

	[Fact]
	public void Call_Given_wrong_add_argument_count_When_called_Then_does_not_add_entry()
	{
		//Arrange
		var addCount = 0;
		var collection = CreateCollection(
			addArgumentCount: 2,
			add: _ =>
			{
				addCount++;
				return null;
			}
		);
		var function = GetFunction("add");

		//Act
		function.Call(collection, 12.0d.ToStackEntry());

		//Assert
		Assert.Equal(0, addCount);
	}

	[Fact]
	public void Call_Given_valid_remove_index_When_called_Then_returns_true()
	{
		//Arrange
		var values     = new List<object?> { "entry" };
		var collection = CreateCollection(values: values);
		var function   = GetFunction("remove");

		//Act
		var actual = function.Call(collection, 0.0d.ToStackEntry());

		//Assert
		Assert.Equal(true, actual);
	}

	[Fact]
	public void Call_Given_valid_remove_index_When_called_Then_removes_entry()
	{
		//Arrange
		var values     = new List<object?> { "entry" };
		var collection = CreateCollection(values: values);
		var function   = GetFunction("remove");

		//Act
		function.Call(collection, 0.0d.ToStackEntry());

		//Assert
		Assert.Empty(values);
	}

	[Fact]
	public void Call_Given_out_of_range_remove_index_When_called_Then_returns_false()
	{
		//Arrange
		var collection = CreateCollection(values: ["entry"]);
		var function   = GetFunction("remove");

		//Act
		var actual = function.Call(collection, 3.0d.ToStackEntry());

		//Assert
		Assert.Equal(false, actual);
	}

	[Fact]
	public void Call_Given_non_numeric_remove_index_When_called_Then_returns_false()
	{
		//Arrange
		var collection = CreateCollection(values: ["entry"]);
		var function   = GetFunction("remove");

		//Act
		var actual = function.Call(collection, "0".ToStackEntry());

		//Assert
		Assert.Equal(false, actual);
	}

	[Fact]
	public void Call_Given_non_finite_remove_index_When_called_Then_returns_false()
	{
		//Arrange
		var collection = CreateCollection(values: ["entry"]);
		var function   = GetFunction("remove");

		//Act
		var actual = function.Call(collection, double.NaN.ToStackEntry());

		//Assert
		Assert.Equal(false, actual);
	}

	private static ScriptCollection CreateCollection(int addArgumentCount = 0, List<object?>? values = null, Func<IReadOnlyList<object?>, object?>? add = null)
	{
		values ??= [];
		return new(
			addArgumentCount,
			() => values,
			add ?? (_ => null),
			index =>
			{
				values.RemoveAt(index);
				return true;
			}
		);
	}

	private static IScriptProperty GetFunction(string name) => ScriptCollection.PropertiesInstance.Single(property => property.PropertyName == name);
}