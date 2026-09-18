namespace Preagonal.Scripting.GS2Engine.Models;

public class TStringProperties : ScriptProperties<TString>
{
	public TStringProperties() : base(null)
	{
		AddFunctions(
			null,
			new()
			{
				{ "lower", "Returns the string converted to lower case.", (_, args) => args.Length > 0 ? Tools.ToScriptString(args[0].GetValue()).ToString().ToLowerInvariant() : string.Empty, [] },
				{ "lowercase", "Returns the string converted to lower case.", (_, args) => args.Length > 0 ? Tools.ToScriptString(args[0].GetValue()).ToString().ToLowerInvariant() : string.Empty, [] },
				{ "upper", "Returns the string converted to upper case.", (_, args) => args.Length > 0 ? Tools.ToScriptString(args[0].GetValue()).ToString().ToUpperInvariant() : string.Empty, [] },
				{ "uppercase", "Returns the string converted to upper case.", (_, args) => args.Length > 0 ? Tools.ToScriptString(args[0].GetValue()).ToString().ToUpperInvariant() : string.Empty, [] },
				{ "replace", "Returns the string with every matching substring replaced.", ReplaceAll, [new("search", typeof(string)), new("replacement", typeof(string))] },
				{ "replaceall", "Returns the string with every matching substring replaced.", ReplaceAll, [new("search", typeof(string)), new("replacement", typeof(string))] }
			}
		);

		AddFunctions(
			this,
			new()
			{
				{ "lower", "Returns the string converted to lower case.", (value, _) => value.ToString().ToLowerInvariant(), [] },
				{ "lowercase", "Returns the string converted to lower case.", (value, _) => value.ToString().ToLowerInvariant(), [] },
				{ "upper", "Returns the string converted to upper case.", (value, _) => value.ToString().ToUpperInvariant(), [] },
				{ "uppercase", "Returns the string converted to upper case.", (value, _) => value.ToString().ToUpperInvariant(), [] },
				{ "replace", "Returns the string with every matching substring replaced.", ReplaceAll, [new("search", typeof(string)), new("replacement", typeof(string))] },
				{ "replaceall", "Returns the string with every matching substring replaced.", ReplaceAll, [new("search", typeof(string)), new("replacement", typeof(string))] }
			}
		);

		Compile();
	}

	private static string ReplaceAll(TString value, IStackEntry[] args)
	{
		var text     = value.ToString();
		var oldValue = args.Length > 0 ? Tools.ToScriptString(args[0].GetValue()) : string.Empty;
		var newValue = args.Length > 1 ? Tools.ToScriptString(args[1].GetValue()) : string.Empty;
		return oldValue.Length == 0 ? text : text.Replace(oldValue, newValue, System.StringComparison.Ordinal);
	}
}