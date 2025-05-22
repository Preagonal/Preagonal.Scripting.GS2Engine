using Preagonal.Scripting.GS2Engine.GS2.ByteCode;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptCom
{
	public Opcode   OpCode       { get; set; }
	public uint     LoopCount    { get; set; }
	public double   Value        { get; set; }
	public TString? VariableName { get; set; }
}