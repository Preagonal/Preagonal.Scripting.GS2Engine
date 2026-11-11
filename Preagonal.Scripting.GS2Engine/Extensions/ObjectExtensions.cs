using System.Collections.Generic;
using System.Linq;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.Extensions;

public static class ObjectExtensions
{
	extension(object? o)
	{
		public object? GetValueForJson(HashSet<object>? visited = null) =>
			o switch
			{
				int              intValue         => intValue,
				bool             boolValue        => boolValue,
				double           d                => d,
				string           value            => value,
				TString          stringValue      => stringValue?.ToString(),
				ScriptVariable   scriptVariable   => scriptVariable?.GetJsonObject(visited),
				ScriptCollection scriptCollection => scriptCollection.Select(s => s.GetValueForJson(visited)),
				ScriptVariable[] array            => array.Select(s=> s.GetJsonObject(visited)),
				LinkedStackEntry linkedValue      => linkedValue.GetValue(true, visited),
				_                                 => o,
			};
	}
}