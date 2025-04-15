namespace Preagonal.Scripting.GS2Engine.GS2.ByteCode;

public static class Extensions
{
	public static TString BytecodeSegmentToString(this BytecodeSegment segment) =>
		segment switch
		{
			BytecodeSegment.Gs1EventFlags => "GS1EventFlags",
			BytecodeSegment.FunctionNames => "FunctionNames",
			BytecodeSegment.Strings       => "Strings",
			BytecodeSegment.Bytecode      => "Bytecode",
			_                             => "Unknown",
		};

	public static bool IsBooleanReturningOp(this Opcode opcode) =>
		opcode switch
		{
			Opcode.OP_NOT      => true,
			Opcode.OP_EQ       => true,
			Opcode.OP_NEQ      => true,
			Opcode.OP_LT       => true,
			Opcode.OP_GT       => true,
			Opcode.OP_LTE      => true,
			Opcode.OP_GTE      => true,
			Opcode.OP_IN_RANGE => true,
			Opcode.OP_IN_OBJ   => true,
			_                  => false,
		};

	public static bool IsReservedIdentOp(this Opcode opcode) =>
		opcode switch
		{
			Opcode.OP_THIS    => true,
			Opcode.OP_THISO   => true,
			Opcode.OP_PLAYER  => true,
			Opcode.OP_PLAYERO => true,
			Opcode.OP_LEVEL   => true,
			Opcode.OP_TEMP    => true,
			_                 => false,
		};

	public static bool IsObjectReturningOp(this Opcode opcode) =>
		opcode switch
		{
			Opcode.OP_THIS    => true,
			Opcode.OP_THISO   => true,
			Opcode.OP_PLAYER  => true,
			Opcode.OP_PLAYERO => true,
			Opcode.OP_LEVEL   => true,
			Opcode.OP_TEMP    => true,
			_                 => false,
		};

	public static TString OpcodeToString(this Opcode opcode) =>
		opcode switch
		{
			Opcode.OP_NONE            => "OP_NONE",
			Opcode.OP_ASSIGN          => "OP_ASSIGN",
			Opcode.OP_SET_INDEX       => "OP_SET_INDEX",
			Opcode.OP_SET_INDEX_TRUE  => "OP_SET_INDEX_TRUE",
			Opcode.OP_IF              => "OP_IF",
			Opcode.OP_TYPE_TRUE       => "OP_TRUE",
			Opcode.OP_TYPE_FALSE      => "OP_FALSE",
			Opcode.OP_TYPE_NULL       => "OP_NULL",
			Opcode.OP_ADD             => "OP_ADD",
			Opcode.OP_SUB             => "OP_SUB",
			Opcode.OP_MUL             => "OP_MUL",
			Opcode.OP_DIV             => "OP_DIV",
			Opcode.OP_MOD             => "OP_MOD",
			Opcode.OP_POW             => "OP_POW",
			Opcode.OP_INC             => "OP_INC",
			Opcode.OP_DEC             => "OP_DEC",
			Opcode.OP_UNARYSUB        => "OP_UNARYSUB",
			Opcode.OP_TYPE_NUMBER     => "OP_TYPE_NUMBER",
			Opcode.OP_FORMAT          => "OP_FORMAT",
			Opcode.OP_TYPE_STRING     => "OP_TYPE_STRING",
			Opcode.OP_TYPE_VAR        => "OP_TYPE_VAR",
			Opcode.OP_TYPE_ARRAY      => "OP_TYPE_ARRAY",
			Opcode.OP_ARRAY_END       => "OP_ARRAY_END",
			Opcode.OP_CONV_TO_FLOAT   => "OP_CONV_TO_FLOAT",
			Opcode.OP_CONV_TO_STRING  => "OP_CONV_TO_STRING",
			Opcode.OP_MEMBER_ACCESS   => "OP_MEMBER_ACCESS",
			Opcode.OP_CONV_TO_OBJECT  => "OP_CONV_TO_OBJECT",
			Opcode.OP_NEW_OBJECT      => "OP_NEW_OBJECT",
			Opcode.OP_FUNC_PARAMS_END => "OP_FUNC_PARAMS_END",
			Opcode.OP_CALL            => "OP_CALL",
			Opcode.OP_CMD_CALL        => "OP_CMD_CALL",
			Opcode.OP_JMP             => "OP_JMP",
			Opcode.OP_INDEX_DEC       => "OP_INDEX_DEC",
			Opcode.OP_RET             => "OP_RET",
			Opcode.OP_EQ              => "OP_EQ",
			Opcode.OP_NEQ             => "OP_NEQ",
			Opcode.OP_LT              => "OP_LT",
			Opcode.OP_GT              => "OP_GT",
			Opcode.OP_LTE             => "OP_LTE",
			Opcode.OP_GTE             => "OP_GTE",
			Opcode.OP_NOT             => "OP_NOT",
			Opcode.OP_AND             => "OP_AND",
			Opcode.OP_OR              => "OP_OR",
			Opcode.OP_ARRAY           => "OP_ARRAY[]",
			Opcode.OP_OBJ_CHARAT      => "OP_OBJ_CHARAT",
			Opcode.OP_OBJ_CLEAR       => "OP_OBJ_CLEAR",
			Opcode.OP_OBJ_ENDS        => "OP_OBJ_ENDS",
			Opcode.OP_IN_RANGE        => "OP_IN_RANGE",
			Opcode.OP_IN_OBJ          => "OP_IN_OBJ",
			Opcode.OP_OBJ_INDEX       => "OP_OBJ_INDEX",
			Opcode.OP_OBJ_INDICES     => "OP_OBJ_INDICES",
			Opcode.OP_OBJ_LENGTH      => "OP_OBJ_LENGTH",
			Opcode.OP_OBJ_LINK        => "OP_OBJ_LINK",
			Opcode.OP_OBJ_POS         => "OP_OBJ_POS",
			Opcode.OP_OBJ_POSITIONS   => "OP_OBJ_POSITIONS",
			Opcode.OP_OBJ_SIZE        => "OP_OBJ_SIZE",
			Opcode.OP_OBJ_STARTS      => "OP_OBJ_STARTS",
			Opcode.OP_OBJ_SUBARRAY    => "OP_OBJ_SUBARRAY",
			Opcode.OP_OBJ_SUBSTR      => "OP_OBJ_SUBSTR",
			Opcode.OP_OBJ_TOKENIZE    => "OP_OBJ_TOKENIZE",
			Opcode.OP_OBJ_TRIM        => "OP_OBJ_TRIM",
			Opcode.OP_OBJ_TYPE        => "OP_OBJ_TYPE",
			Opcode.OP_JOIN            => "OP_JOIN",
			Opcode.OP_THIS            => "OP_THIS",
			Opcode.OP_THISO           => "OP_THISO",
			Opcode.OP_PLAYER          => "OP_PLAYER",
			Opcode.OP_PLAYERO         => "OP_PLAYERO",
			Opcode.OP_LEVEL           => "OP_LEVEL",
			Opcode.OP_TEMP            => "OP_TEMP",
			_                         => "OP " + (int)opcode,
		};
}