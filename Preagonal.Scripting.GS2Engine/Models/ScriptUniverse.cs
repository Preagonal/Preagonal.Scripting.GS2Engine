namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptUniverse : ScriptVariable
{
	public new static readonly ScriptUniverseProperties PropertiesInstance = [];

	public override IScriptProperties Properties => PropertiesInstance;
}