using Preagonal.Scripting.GS2Engine.GS2.ByteCode;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptCom
{
	private TString? _normalizedVariableName;
	private TString? _variableName;

	public Opcode OpCode    { get; set; }
	public uint   LoopCount { get; set; }
	public double Value     { get; set; }

	public TString? VariableName
	{
		get => _variableName;
		set
		{
			_variableName           = value;
			_normalizedVariableName = null;
		}
	}

	public TString? NormalizedVariableName
	{
		get
		{
			if (_normalizedVariableName != null || VariableName == null)
				return _normalizedVariableName;

			_normalizedVariableName = VariableName.ToString().ToLowerInvariant();
			return _normalizedVariableName;
		}
	}
}