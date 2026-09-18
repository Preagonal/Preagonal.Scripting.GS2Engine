using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class VariableCollectionTests
{
	[Fact]
	public void Given_empty_collection_When_getting_snapshot_Then_empty_snapshot_is_reused()
	{
		var collection = new VariableCollection();

		var first  = collection.GetSnapshot();
		var second = collection.GetSnapshot();

		Assert.Same(first, second);
	}
}