namespace Preagonal.Scripting.GS2Engine.GS2.ByteCode;

public enum Opcode
{
	/*  NAME            SEMANTIC                        */
	/* ------------------------------------------------ */
	OP_NONE           = 0,
	OP_SET_INDEX      = 1, //  S(1) =			// likely JMP to opIndex
	OP_SET_INDEX_TRUE = 2,

	OP_OR    = 3,
	OP_IF    = 4, // likely JMPIFNOT
	OP_AND   = 5,
	OP_CALL  = 6,
	OP_RET   = 7, //  Return to location on top of on jump stack
	OP_SLEEP = 8,

	OP_CMD_CALL = 9, //  may just increase the loop count for the loop limit of 10k
	OP_JMP      = 10, //  JUMP to N(0, 4) by byte offset unconditionally

	OP_TYPE_NUMBER        = 20,
	OP_TYPE_STRING        = 21,
	OP_TYPE_VAR           = 22,
	OP_TYPE_ARRAY         = 23,
	OP_TYPE_TRUE          = 24,
	OP_TYPE_FALSE         = 25,
	OP_TYPE_NULL          = 26,
	OP_PI                 = 27,
	OP_COPY_LAST_OP       = 30,
	OP_SWAP_LAST_OPS      = 31,
	OP_INDEX_DEC          = 32,
	OP_CONV_TO_FLOAT      = 33,
	OP_CONV_TO_STRING     = 34,
	OP_MEMBER_ACCESS      = 35,
	OP_CONV_TO_OBJECT     = 36,
	OP_ARRAY_END          = 37,
	OP_ARRAY_NEW          = 38,
	OP_SETARRAY           = 39,
	OP_INLINE_NEW         = 40,
	OP_MAKEVAR            = 41,
	OP_NEW_OBJECT         = 42,
	OP_INLINE_CONDITIONAL = 44,
	OP_ASSIGN             = 50, //  S(1) = S(0)
	OP_FUNC_PARAMS_END    = 51,

	OP_INC = 52, //  SET (S(0) = S(0) + 1)
	OP_DEC = 53, //  SET (S(0) = S(0) - 1)

	OP_ADD = 60, //  PUSH (S(1) + S(0))
	OP_SUB = 61, //  PUSH (S(1) - S(0))
	OP_MUL = 62, //  PUSH (S(1) * S(0))
	OP_DIV = 63, //  PUSH (S(1) / S(0))
	OP_MOD = 64, //  PUSH (S(1) % S(0))
	OP_POW = 65, //  PUSH (S(1) ^ S(0))

	OP_NOT      = 68, //  PUSH (!S(0))
	OP_UNARYSUB = 69,

	OP_EQ  = 70, //  PUSH (S(1) == S(0))
	OP_NEQ = 71,
	OP_LT  = 72, //  PUSH (S(1) < S(0))
	OP_GT  = 73, //  PUSH (S(1) > S(0))
	OP_LTE = 74, //  PUSH (S(1) <= S(0))
	OP_GTE = 75, //  PUSH (S(1) >= S(0))

	OP_BWO = 76, //  PUSH (S(1) | S(0))
	OP_BWA = 77, //  PUSH (S(1) & S(0))

	OP_IN_RANGE  = 80,
	OP_IN_OBJ    = 81,
	OP_OBJ_INDEX = 82,
	OP_OBJ_TYPE  = 83, // gets the type of the var (float 0, string 1, object 2, array 3)

	OP_FORMAT                = 84,
	OP_INT                   = 85,
	OP_ABS                   = 86,
	OP_RANDOM                = 87,
	OP_SIN                   = 88,
	OP_COS                   = 89,
	OP_ARCTAN                = 90,
	OP_EXP                   = 91,
	OP_LOG                   = 92,
	OP_MIN                   = 93,
	OP_MAX                   = 94,
	OP_GETANGLE              = 95,
	OP_GETDIR                = 96,
	OP_VECX                  = 97,
	OP_VECY                  = 98,
	OP_OBJ_INDICES           = 99,
	OP_OBJ_LINK              = 100,
	OP_CHAR                  = 103,
	OP_OBJ_TRIM              = 110,
	OP_OBJ_LENGTH            = 111,
	OP_OBJ_POS               = 112,
	OP_JOIN                  = 113,
	OP_OBJ_CHARAT            = 114,
	OP_OBJ_SUBSTR            = 115,
	OP_OBJ_STARTS            = 116,
	OP_OBJ_ENDS              = 117,
	OP_OBJ_TOKENIZE          = 118,
	OP_TRANSLATE             = 119,
	OP_OBJ_POSITIONS         = 120, // array of positions of the substring in the string
	OP_OBJ_SIZE              = 130,
	OP_ARRAY                 = 131,
	OP_ARRAY_ASSIGN          = 132,
	OP_ARRAY_MULTIDIM        = 133,
	OP_ARRAY_MULTIDIM_ASSIGN = 134,
	OP_OBJ_SUBARRAY          = 135,
	OP_OBJ_ADDSTRING         = 136,
	OP_OBJ_DELETESTRING      = 137,
	OP_OBJ_REMOVESTRING      = 138,
	OP_OBJ_REPLACESTRING     = 139,
	OP_OBJ_INSERTSTRING      = 140,
	OP_OBJ_CLEAR             = 141,
	OP_ARRAY_NEW_MULTIDIM    = 142,
	OP_WITH                  = 150,
	OP_WITHEND               = 151,
	OP_FOREACH               = 163,
	OP_THIS                  = 180,
	OP_THISO                 = 181,
	OP_PLAYER                = 182,
	OP_PLAYERO               = 183,
	OP_LEVEL                 = 184,
	OP_TEMP                  = 189,
	OP_PARAMS                = 190,
	OP_NUM_OPS, //  This is to get the number of operations
}