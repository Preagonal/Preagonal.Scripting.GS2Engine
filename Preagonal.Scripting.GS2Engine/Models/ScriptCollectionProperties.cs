using System.Linq;
using Preagonal.Scripting.GS2Engine.Enums;

namespace Preagonal.Scripting.GS2Engine.Models;

public sealed class ScriptCollectionProperties : ScriptProperties<ScriptCollection>
{
	public ScriptCollectionProperties() : base(null)
	{
		AddFunctions(
			this,
			new()
			{
				{ "add", "Adds an entry to the owning level collection.", (collection, arguments) => collection.Add(arguments.Select(argument => argument.GetValue()).ToArray()), [new("values", typeof(object[]))] },
				{
					"remove", "Removes the entry at the specified index from the owning level collection.", (collection, arguments) =>
					{
						if (arguments.Length != 1 || arguments[0].Type != StackEntryType.Number)
							return false;

						var index = arguments[0].GetValue<double>();
						return double.IsFinite(index) && collection.TryRemoveAt((int)index);
					},
					[new("index", typeof(int))]
				},
			}
		);

		Compile();
	}
}