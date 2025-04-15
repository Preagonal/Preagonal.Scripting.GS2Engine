using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Exceptions;
using Preagonal.Scripting.GS2Engine.GS2.ByteCode;
using Preagonal.Scripting.GS2Engine.Models;
using static Preagonal.Scripting.GS2Engine.Enums.StackEntryType;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptMachine
{
	private readonly Script             _script;
	private readonly VariableCollection _tempVariables = new();

	private delegate IStackEntry OpcodeHandler(ScriptCom op, ref int index);

	private readonly Dictionary<Opcode, OpcodeHandler> _opcodeHandlers = new();

	private bool _firstRun = true;
	private int  _indexPos;
	private int  _scriptStackSize;
	private bool _useTemp;

	public ScriptMachine(Script script)
	{
		_script = script;
		registerOpcodeHandlers();
	}

	private void registerOpcodeHandlers()
	{

	}

	private Dictionary<string, Script.Command> Functions => _script.ExternalFunctions;

	public async Task<IStackEntry> Execute(string functionName, Stack<IStackEntry>? callStack = null)
	{
		if (!_script.Functions.TryGetValue(functionName.ToLower(), out var value))
			return 0.ToStackEntry();

		Stack<IStackEntry> stack = new();

		var desiredStart = value.BytecodePosition;
		var index        = _firstRun ? 0 : value.BytecodePosition;
		if (_firstRun)
			_firstRun = false;

		//const int maxLoopCount = 10000; // TODO: FIX

		IStackEntry?       opCopy = null;
		Stack<IStackEntry> opWith = new();

		Tools.DebugLine($"Starting to execute function \"{_script.Name}.{functionName}\"");
		while (index < _script.Bytecode.Length)
		{
			var curIndex = index;
			index     = curIndex + 1;
			_indexPos = index;

			var op = _script.Bytecode[curIndex];

			Tools.Debug($"OP: {op.OpCode}");
			if (op.VariableName != null)
				Tools.Debug($" - var: {op.VariableName}");
			if (op.Value != 0)
				Tools.Debug($" - val: {op.Value}");
			Tools.DebugLine("");

			switch (op.OpCode)
			{
				default:
				case Opcode.OP_NONE:
					break;
				case Opcode.OP_SET_INDEX:
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					if (op.Value == _script.Bytecode.Length)
					{
						index        = desiredStart;
						desiredStart = (int)op.Value;
					}
					else
					{
						index = (int)op.Value;
					}

					_indexPos = index;
					break;
				case Opcode.OP_SET_INDEX_TRUE:
					curIndex = _scriptStackSize;
					if (curIndex < 0) return 1.ToStackEntry();
					var sitCompVar = GetEntry(stack.Pop()).GetValue();

					var sitCompare = sitCompVar is bool var
						? var
						: sitCompVar is double && sitCompVar.Equals((double)1);
					if (!sitCompare)
					{
						_scriptStackSize = curIndex + -1;
					}
					else
					{
						index            = (int)op.Value;
						_scriptStackSize = curIndex + -1;
						_indexPos        = index;
					}

					break;
				case Opcode.OP_OR:
					curIndex = _scriptStackSize;
					if (curIndex < 0) return 1.ToStackEntry();

					var orCompVar = GetEntry(stack.Pop()).GetValue();

					var orCompare = orCompVar is bool compVar
						? compVar
						: orCompVar is double && orCompVar.Equals((double)1);

					if (orCompare)
					{
						index     = (int)op.Value;
						_indexPos = index;
						stack.Push(orCompare.ToStackEntry());
					}

					break;
				case Opcode.OP_IF:
					curIndex = stack.Count;
					if (curIndex < 0) return 1.ToStackEntry();

					var ifCompVar = GetEntry(stack.Pop()).GetValue();

					var ifCompare = ifCompVar is bool b ? b : ifCompVar is double && ifCompVar.Equals((double)1);

					if (!ifCompare)
					{
						index            = (int)op.Value;
						_scriptStackSize = curIndex + -1;
						_indexPos        = index;
					}

					break;
				case Opcode.OP_AND:
					if (_scriptStackSize < 0) return 1.ToStackEntry();

					var andCompVar = GetEntry(stack.Pop()).GetValue();

					var andCompare = andCompVar is bool var1
						? var1
						: andCompVar is double && andCompVar.Equals((double)1);

					if (!andCompare)
					{
						index            = (int)op.Value;
						_scriptStackSize = curIndex + -1;
						_indexPos        = index;
					}

					break;
				case Opcode.OP_CALL:
					var callEntry  = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var cmd        = GetEntryValue<object>(callEntry, returnStackEntryIfNotFound: true);
					var parameters = stack.Clone();
					while (stack.Peek()?.Type != ArrayStart) stack.Pop();
					stack.Pop();
					var opWithHasFunc = false;
					if (opWith?.Any() ?? false)
						opWithHasFunc =
							opWith?.Peek()
							      ?.GetValue<VariableCollection>()
							      ?.ContainsVariable(cmd?.ToString()?.ToLower() ?? string.Empty) ??
							false;
					switch (callEntry.Type)
					{
						case StackEntryType.String or Variable when opWithHasFunc:
							var funcRet =
								opWith?.Peek()
								      ?.GetValue<VariableCollection>()
								      ?.GetVariable(cmd?.ToString()?.ToLower() ?? string.Empty)
								      ?.GetValue<Script.Command>()
								      ?.Invoke(this, parameters.ToArray()) ??
								0.ToStackEntry();
							stack.Push(funcRet);
							break;
						case StackEntryType.String or Variable
							when _script.Functions.ContainsKey(cmd?.ToString()?.ToLower() ?? string.Empty):
							stack.Push(await Execute(cmd?.ToString()?.ToLower() ?? string.Empty, parameters).ConfigureAwait(false));
							break;
						case StackEntryType.String or Variable when Functions.TryGetValue(
							cmd?.ToString()?.ToLower() ?? string.Empty,
							out var command
						):
							stack.Push(command.Invoke(this, parameters.ToArray()));
							break;
						case StackEntryType.String or Variable:
							stack.Push(0.ToStackEntry());
							break;
						case Function:
							stack.Push(
								(cmd as Script.Command)?.Invoke(this, parameters.ToArray()) ?? 0.ToStackEntry()
							);
							break;
						case StackEntryType.Script:
							stack.Push(
								(cmd as Script.Command)?.Invoke(this, parameters.ToArray()) ?? 0.ToStackEntry()
							);
							break;
						default:
							stack.Push(0.ToStackEntry());
							break;
					}
					break;
				case Opcode.OP_RET:
					IStackEntry ret = 0.ToStackEntry();
					if (stack.Count > 0)
						ret = stack.Pop();
					return GetEntry(ret);

				case Opcode.OP_SLEEP:
					var sleep = GetEntryValue<double>(stack.Pop());
					sleep *= 1000;
					await Task.Delay((int)sleep).ConfigureAwait(false);
					break;
				case Opcode.OP_CMD_CALL:
					//index = _script.Field170Xc0;
					if ((int)op.Value == index)
					{
						/* TODO: Make a proper fix
						if (maxLoopCount <= op.LoopCount &&
						    !functionName.Equals("onTimeout", StringComparison.CurrentCultureIgnoreCase))
							throw new ScriptException("Loop limit exceeded");
						*/

						op.LoopCount += 1;
						index        =  _indexPos;
					}
					else
					{
						op.Value        = index;
						op.VariableName = null;
						index           = _indexPos;
					}

					break;
				case Opcode.OP_JMP:

				{
					//index = indexPos;
				}

					break;
				case Opcode.OP_TYPE_NUMBER:
					stack.Push(op.Value.ToStackEntry());
					break;
				case Opcode.OP_TYPE_STRING:
					stack.Push((op.VariableName ?? "").ToStackEntry());
					break;
				case Opcode.OP_TYPE_VAR:
					stack.Push((op.VariableName?.ToLower() ?? "").ToStackEntry(true));
					break;
				case Opcode.OP_TYPE_ARRAY:
					stack.Push(new StackEntry(ArrayStart, null));
					break;
				case Opcode.OP_TYPE_TRUE:
					stack.Push(true.ToStackEntry());
					break;
				case Opcode.OP_TYPE_FALSE:
					stack.Push(false.ToStackEntry());
					break;
				case Opcode.OP_TYPE_NULL:
					stack.Push(0.ToStackEntry());
					break;
				case Opcode.OP_PI:
					stack.Push(Math.PI.ToStackEntry());
					break;
				case Opcode.OP_COPY_LAST_OP:
					stack.Push(stack.Peek());
					break;
				case Opcode.OP_SWAP_LAST_OPS:
					if (stack.Count > 1)
					{
						var stackSwap1 = stack.Pop();
						var stackSwap2 = stack.Pop();
						stack.Push(stackSwap1);
						stack.Push(stackSwap2);
					}

					break;
				case Opcode.OP_INDEX_DEC:
					if (stack.Count > 0)
						stack.Pop();
					break;
				case Opcode.OP_CONV_TO_FLOAT:
					var test = GetEntryValue<object>(stack.Pop());

					double convToFloatVal;
					if (test?.GetType() == typeof(TString) &&
					    double.TryParse((TString)test, out var convToFloatParse))
						convToFloatVal = convToFloatParse;
					else if (test?.GetType() == typeof(bool))
						convToFloatVal = (bool)test ? 1 : 0;
					else if (test?.GetType() == typeof(double))
						convToFloatVal = (double)test;
					else
						convToFloatVal = 0;


					stack.Push(convToFloatVal.ToStackEntry());
					break;
				case Opcode.OP_CONV_TO_STRING:
					var convToStringObject = GetEntryValue<object>(stack.Pop());
					if (convToStringObject is IList convListToString && convListToString.GetType().IsGenericType)
					{
						var s = new StringBuilder();
						foreach (var convListString in convListToString)
						{
							if (convListString is bool boolConvListString)
							{
								s.Append($"{(Convert.ToInt32(boolConvListString)).ToString()},");
							}
							else
							{
								s.Append($"{convListString.ToString()},");
							}
						}
						var convListResult = s.ToString();
						s.Clear();
						convToStringObject = convListResult[..^1];
					}
					if (convToStringObject is bool boolConvToStringObject)
					{
						convToStringObject = (Convert.ToInt32(boolConvToStringObject)).ToString();
					}
					stack.Push(convToStringObject?.ToString().ToStackEntry() ?? "".ToStackEntry());
					break;
				case Opcode.OP_MEMBER_ACCESS:
					var stackVal          = stack.Pop();
					var memberAccessParam = GetEntryValue<TString>(stackVal, StackEntryType.String);
					try
					{
						if (stack.Peek()?.Type == StackEntryType.Script)
						{
							var scriptStackEntry     = stack.Pop();
							var scriptObject         = scriptStackEntry.GetValue<Script>();
							var stackValFunctionName = stackVal.GetValue<TString>() ?? "";
							if (scriptObject != null &&
							    scriptObject.Functions.ContainsKey(stackValFunctionName.ToLower()) &&
							    scriptObject.Functions[stackValFunctionName.ToLower()].IsPublic)
							{
								var memberCommand = (Script.Command)((_, args) =>
									// ReSharper disable once CoVariantArrayConversion
									scriptObject.Call(stackValFunctionName.ToLower(), args)
									            .ConfigureAwait(false)
									            .GetAwaiter()
									            .GetResult());
								stack.Push(memberCommand.ToStackEntry());
							}
							else
							{
								var scriptObjectMember             = scriptObject?.GetVariable(memberAccessParam ?? "");
								stack.Push(scriptObjectMember ?? 0.ToStackEntry());
							}
							break;
						}
						var memberAccessEntry  = GetEntry(stack.Pop());
						var memberAccessObject = memberAccessEntry.GetValue<VariableCollection>();
						var member             = memberAccessObject?.GetVariable(memberAccessParam ?? "");
						stack.Push(member ?? 0.ToStackEntry());
					}
					catch (Exception e)
					{
						Tools.DebugLine(e.Message);
						stack.Push(0.ToStackEntry());
					}
					break;
				case Opcode.OP_CONV_TO_OBJECT:
					var convEntry = GetEntry(stack.Pop());
					if (convEntry.Type is StackEntryType.String or Variable)
						stack.Push(
							(opWith is { Count: not 0 }
								? opWith?.Peek()
								        ?.GetValue<VariableCollection>()
								        ?.GetVariable(GetEntryValue<TString>(convEntry)?.ToLower() ?? string.Empty)
								: GetEntry(convEntry, Variable))!
						);
					else
						stack.Push(convEntry);

					break;
				case Opcode.OP_ARRAY_END:

					List<object> stackArr = [];
					//keep popping the stack till we hit an array start
					while (stack.Count > 0 && stack.Peek().Type != ArrayStart)
						stackArr.Add(GetEntry(stack.Pop())?.GetValue() ?? 0.ToStackEntry());
					stack.Pop(); //pop array start marker off

					stack.Push(new StackEntry(StackEntryType.Array, stackArr)); //push new array onto stack
					break;
				case Opcode.OP_ARRAY_NEW:
					var arraySize = (int)stack.Pop().GetValue<double>();
					var newArray  = new object[arraySize];
					for (var newArrayI = 0; newArrayI < arraySize; newArrayI++)
					{
						newArray[newArrayI] = 0d;
					}
					stack.Push(newArray.ToStackEntry());
					break;
				case Opcode.OP_SETARRAY:
					break;
				case Opcode.OP_INLINE_NEW:
					if (stack.Count > 0)
					{
						if (stack.Peek().Type == StackEntryType.String)
						{
							var inlineNew = stack.Pop();
							stack.Push(inlineNew.GetValue().ToStackEntry(true));
						}
					}
					break;
				case Opcode.OP_MAKEVAR:
					break;
				case Opcode.OP_NEW_OBJECT:
					var newObject      = stack.Pop();
					var newObjectParam = stack.Pop();
					try
					{
						var newObjectRet = (VariableCollection)GetInstance(
							GetEntryValue<TString>(newObject) ?? string.Empty,
							GetEntryValue<object>(newObjectParam, returnStackEntryIfNotFound: true)?.ToString()!
						)!;
						stack.Push(newObjectRet?.ToStackEntry() ?? 0.ToStackEntry());
					}
					catch (Exception e)
					{
						Tools.DebugLine(e.Message);
						stack.Push(0.ToStackEntry());
					}

					break;
				case Opcode.OP_INLINE_CONDITIONAL:

					var inlineConditional = stack.Pop();
					if (inlineConditional.GetValue<bool>() != false) {
						inlineConditional.SetValue(1.0d);
					}
					stack.Push(inlineConditional);

					break;
				case Opcode.OP_ASSIGN:
					var val      = stack.Pop();
					var variable = (stack.Count == 0 ? opCopy : /*GetEntry*/(stack.Pop())) ?? 0.ToStackEntry();

					if (opWith is { Count: not 0 })
					{
						try
						{
							opWith?.Peek()
							      ?.GetValue<VariableCollection>()
							      ?.AddOrUpdate((variable?.GetValue() ?? "").ToString()?.ToLower() ?? string.Empty, GetEntry(val));
						}
						catch (Exception e)
						{
							Tools.DebugLine(e.Message);
						}
					}
					else if (variable.Type != Variable /*StackEntryType.String or Number val.Type*/)
					{
						variable.SetValue(GetEntry(val).GetValue());
					}
					else if (!_useTemp)
					{
						Script.GlobalVariables.AddOrUpdate((variable.GetValue() ?? "").ToString()?.ToLower() ?? string.Empty, GetEntry(val));
					}
					else
					{
						_useTemp = false;
						_tempVariables.AddOrUpdate((variable.GetValue() ?? "").ToString()?.ToLower() ?? string.Empty, GetEntry(val));
					}

					break;
				case Opcode.OP_FUNC_PARAMS_END:
					while (stack.Count > 0)
					{
						var funcParam = stack.Pop();
						try
						{
							if (callStack is { Count: > 0 })
							{
								var funcParamVal = callStack.Pop();
								_tempVariables.AddOrUpdate(
									(funcParam.GetValue() ?? "").ToString()?.ToLower() ?? string.Empty,
									funcParamVal ?? 0.ToStackEntry()
								);
							}
						}
						catch (Exception e)
						{
							// ignored
							Tools.DebugLine(e.Message);
						}
					}

					index = _indexPos;
					break;
				case Opcode.OP_INC:
					var incVar = GetEntry(stack.Pop());

					var incVal = GetEntryValue<double>(incVar);
					if (incVar.Type == Number) incVar.SetValue(incVal + 1);

					stack.Push(incVar);
					break;
				case Opcode.OP_DEC:
					var decVar = GetEntry(stack.Pop());
					var decVal = GetEntryValue<double>(decVar);
					if (decVar.Type == Number) decVar.SetValue(decVal - 1);

					stack.Push(decVar);
					break;
				case Opcode.OP_ADD:
					var addA = GetEntryValue<double>(stack.Pop());
					var addB = GetEntryValue<double>(stack.Pop());
					stack.Push((addB + addA).ToStackEntry());
					break;
				case Opcode.OP_SUB:
					var subA = GetEntryValue<double>(stack.Pop());
					var subB = GetEntryValue<double>(stack.Pop());
					stack.Push((subB - subA).ToStackEntry());
					break;
				case Opcode.OP_MUL:
					var mulA = GetEntryValue<double>(stack.Pop());
					var mulB = GetEntryValue<double>(stack.Pop());
					stack.Push((mulB * mulA).ToStackEntry());
					break;
				case Opcode.OP_DIV:
					var divA = GetEntryValue<double>(stack.Pop());
					var divB = GetEntryValue<double>(stack.Pop());
					stack.Push((divB / divA).ToStackEntry());
					break;
				case Opcode.OP_MOD:
					var modA = GetEntryValue<double>(stack.Pop());
					var modB = GetEntryValue<double>(stack.Pop());
					stack.Push((modB % modA).ToStackEntry());
					break;
				case Opcode.OP_POW:
					var powA = GetEntryValue<double>(stack.Pop());
					var powB = GetEntryValue<double>(stack.Pop());
					stack.Push(Math.Pow(powB, powA).ToStackEntry());
					break;
				case Opcode.OP_NOT:
					break;
				case Opcode.OP_UNARYSUB:
					break;
				case Opcode.OP_EQ:
					var eq1 = GetEntryValue<object?>(stack.Pop());
					var eq2 = GetEntryValue<object?>(stack.Pop());
					if (eq1 is IList && eq1.GetType().IsGenericType && eq2 is IList && eq2.GetType().IsGenericType)
						stack.Push(((List<object?>)eq1).SequenceEqual((List<object?>)eq2).ToStackEntry());
					else
						stack.Push((eq1 ?? false).Equals(eq2).ToStackEntry());

					break;
				case Opcode.OP_NEQ:
					var neq1 = GetEntryValue<object?>(stack.Pop());
					var neq2 = GetEntryValue<object?>(stack.Pop());
					stack.Push((!(neq1 ?? false).Equals(neq2)).ToStackEntry());
					break;
				case Opcode.OP_LT:
					var ltA = GetEntryValue<double>(stack.Pop());
					var ltB = GetEntryValue<double>(stack.Pop());
					stack.Push((ltB < ltA).ToStackEntry());
					break;
				case Opcode.OP_GT:
					var gtA = GetEntryValue<double>(stack.Pop());
					var gtB = GetEntryValue<double>(stack.Pop());
					stack.Push((gtB > gtA).ToStackEntry());
					break;
				case Opcode.OP_LTE:
					var lteA = GetEntryValue<double>(stack.Pop());
					var lteB = GetEntryValue<double>(stack.Pop());
					stack.Push((lteB <= lteA).ToStackEntry());
					break;
				case Opcode.OP_GTE:
					var gteA = GetEntryValue<double>(stack.Pop());
					var gteB = GetEntryValue<double>(stack.Pop());
					stack.Push((gteB >= gteA).ToStackEntry());
					break;
				case Opcode.OP_BWO:
					break;
				case Opcode.OP_BWA:
					break;
				case Opcode.OP_IN_RANGE:
					break;
				case Opcode.OP_IN_OBJ:
					break;
				case Opcode.OP_OBJ_INDEX:
					break;
				case Opcode.OP_OBJ_TYPE:
					break;
				case Opcode.OP_FORMAT:
					var format  = stack.Pop();
					var objects = stack.Select(x => GetEntryValue<object>(x)).ToArray();
					stack.Clear();
					var formatted = Tools.Format(GetEntryValue<TString>(format) ?? "", objects);
					stack.Push(formatted.ToStackEntry());
					break;
				case Opcode.OP_INT:
					break;
				case Opcode.OP_ABS:
					stack.Push(Math.Abs(GetEntryValue<double>(stack.Pop())).ToStackEntry());
					break;
				case Opcode.OP_RANDOM:
					break;
				case Opcode.OP_SIN:
					var sinVal = Math.Sin(GetEntryValue<double>(stack.Pop()));
					sinVal = Math.Abs(sinVal) < 0.000001 ? 0.0f : sinVal;
					stack.Push(sinVal.ToStackEntry());
					break;
				case Opcode.OP_COS:
					var cosVal = Math.Cos(GetEntryValue<double>(stack.Pop()));
					cosVal = Math.Abs(cosVal) < 0.000001 ? 0.0f : cosVal;
					stack.Push(cosVal.ToStackEntry());
					break;
				case Opcode.OP_ARCTAN:
					var atanVal = Math.Atan(GetEntryValue<double>(stack.Pop()));
					atanVal = Math.Abs(atanVal) < 0.000001 ? 0.0f : atanVal;
					stack.Push(atanVal.ToStackEntry());
					break;
				case Opcode.OP_EXP:
					break;
				case Opcode.OP_LOG:
					break;
				case Opcode.OP_MIN:
					break;
				case Opcode.OP_MAX:
					break;
				case Opcode.OP_GETANGLE:
					break;
				case Opcode.OP_GETDIR:
					break;
				case Opcode.OP_VECX:
					var vecxDir = (int)stack.Pop().GetValue<double>();
					var vecxVal = (vecxDir % 4) switch
					{
						1 => -1,
						3 => 1,
						_ => 0,
					};
					stack.Push(vecxVal.ToStackEntry());
					break;
				case Opcode.OP_VECY:
					var vecyDir = (int)stack.Pop().GetValue<double>();
					var vecyVal = (vecyDir % 4) switch
					{
						0 => -1,
						2 => 1,
						_ => 0,
					};
					stack.Push(vecyVal.ToStackEntry());
					break;
				case Opcode.OP_OBJ_INDICES:
					break;
				case Opcode.OP_OBJ_LINK:
					break;
				case Opcode.OP_CHAR:
					break;
				case Opcode.OP_OBJ_TRIM:
					break;
				case Opcode.OP_OBJ_LENGTH:
					break;
				case Opcode.OP_OBJ_POS:
					break;
				case Opcode.OP_JOIN:
					var joinA = GetEntryValue<TString>(stack.Pop());
					var joinB = GetEntryValue<TString>(stack.Pop());
					stack.Push($"{joinB}{joinA}".ToStackEntry());
					break;
				case Opcode.OP_OBJ_CHARAT:
					break;
				case Opcode.OP_OBJ_SUBSTR:
					break;
				case Opcode.OP_OBJ_STARTS:
					var obj        = GetEntryValue<TString>(stack.Pop()) ?? "";
					var startsWith = GetEntryValue<TString>(stack.Pop()) ?? "";
					stack.Push(
						obj.StartsWith(startsWith, StringComparison.CurrentCultureIgnoreCase).ToStackEntry()
					);
					break;
				case Opcode.OP_OBJ_ENDS:
					break;
				case Opcode.OP_OBJ_TOKENIZE:
					break;
				case Opcode.OP_TRANSLATE:
					break;
				case Opcode.OP_OBJ_POSITIONS:
					break;
				case Opcode.OP_OBJ_SIZE:
					var objSizeVar = GetEntry(stack.Pop()).GetValue();

					if (objSizeVar is VariableCollection vc)
						stack.Push(vc.GetDictionary().Count.ToStackEntry());
					else if (objSizeVar is List<string> ls)
						stack.Push(ls.Count.ToStackEntry());
					else if (objSizeVar is List<object> lo)
						stack.Push(lo.Count.ToStackEntry());
					else
						stack.Push(0.ToStackEntry());

					break;
				case Opcode.OP_ARRAY:
					var arrayIndex = GetEntryValue<double>(stack.Pop());
					var array      = GetEntryValue<object>(stack.Pop());

					if (array?.GetType() == typeof(List<string>))
						stack.Push(
							((List<string>?)array)?[(int)arrayIndex].ToStackEntry() ?? new object().ToStackEntry()
						);
					else if (array?.GetType() == typeof(List<int>))
						stack.Push(
							((List<int>?)array)?[(int)arrayIndex].ToStackEntry() ?? new object().ToStackEntry()
						);
					else
						stack.Push(
							((List<object>?)array)?[(int)arrayIndex].ToStackEntry() ?? new object().ToStackEntry()
						);

					break;
				case Opcode.OP_ARRAY_ASSIGN:
					try
					{
						var arrAssVal   = GetEntry(stack.Pop());
						var arrAssIndex = GetEntry(stack.Pop()).GetValue<double>();
						var arrAssObj   = GetEntry(stack.Pop()).GetValue<VariableCollection>();
						arrAssObj?.AddOrUpdate(((int)arrAssIndex).ToString(), arrAssVal);
					}
					catch (Exception e)
					{
						Tools.DebugLine(e.Message);
					}

					break;
				case Opcode.OP_ARRAY_MULTIDIM:
					break;
				case Opcode.OP_ARRAY_MULTIDIM_ASSIGN:
					break;
				case Opcode.OP_OBJ_SUBARRAY:
					break;
				case Opcode.OP_OBJ_ADDSTRING:
					break;
				case Opcode.OP_OBJ_DELETESTRING:
					break;
				case Opcode.OP_OBJ_REMOVESTRING:
					break;
				case Opcode.OP_OBJ_REPLACESTRING:
					break;
				case Opcode.OP_OBJ_INSERTSTRING:
					break;
				case Opcode.OP_OBJ_CLEAR:
					break;
				case Opcode.OP_ARRAY_NEW_MULTIDIM:
					break;
				case Opcode.OP_WITH:
					opWith?.Push(stack.Pop());
					break;
				case Opcode.OP_WITHEND:
					opWith?.Pop(); //stack.Push();
					break;
				case Opcode.OP_FOREACH:
					if (stack.ToArray().Length > 1 && stack.ToArray()[1].Type == StackEntryType.Array)
					{
						var arrForeachIndexEntry = GetEntry(stack.Pop());
						var arrForeachIndex      = arrForeachIndexEntry.GetValue<double>();

						var arrForeachObjEntry   = GetEntry(stack.Pop());
						var arrForeachObj        = arrForeachObjEntry.GetValue<List<object>>();

						if ((int)arrForeachIndex == arrForeachObj?.Count)
						{
							index     = (int)op.Value;
							_indexPos = index;
							break;
						}
						var tempVar              = stack.Pop();

						tempVar.SetValue(arrForeachObj?[(int)arrForeachIndex] ?? "");

						stack.Push(tempVar);
						stack.Push(arrForeachObjEntry);
						stack.Push(arrForeachIndexEntry);
					}

					break;
				case Opcode.OP_THIS:
					stack.Push(_script.ToStackEntry());
					break;
				case Opcode.OP_THISO:
					stack.Push(_script.ToStackEntry());
					break;
				case Opcode.OP_PLAYER:
					stack.Push(
						Script.GlobalObjects.TryGetValue("player", out var o)
							? o.ToStackEntry()
							: 0.ToStackEntry()
					);
					break;
				case Opcode.OP_PLAYERO:
					break;
				case Opcode.OP_LEVEL:
					break;
				case Opcode.OP_TEMP:
					stack.Push(new StackEntry(StackEntryType.Array, _tempVariables));
					_useTemp = true;
					break;
				case Opcode.OP_PARAMS:
					break;
				case Opcode.OP_NUM_OPS:
					break;
			}
		}

		return 0.ToStackEntry();
	}

	private object? GetInstance(string className, string arg)
	{
		if (string.IsNullOrEmpty(className)) return null;
		if (className.EndsWith("Profile", StringComparison.CurrentCultureIgnoreCase))
			return new GuiControl(arg, _script);

		var type = Type.GetType(className);
		if (type != null)
			return Activator.CreateInstance(type, arg, _script);
		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var asm in assemblies)
		{
			var types = asm.GetTypes();
			type = types
			          .FirstOrDefault(x => x.Name.Equals(className, StringComparison.CurrentCultureIgnoreCase)) ??
			       null;
			if (type != null)
				return Activator.CreateInstance(type, arg, _script);
		}

		throw new ScriptException($"Missing Class: {className}");
	}

	public IStackEntry GetEntry(IStackEntry stackEntry, StackEntryType? overrideStackType = null, bool returnStackEntryIfNotFound = false)
	{
		StackEntryType? type          = overrideStackType ?? stackEntry.Type;
		var             retVal        = stackEntry;
		var             foundVariable = false;
		switch (type)
		{
			case Variable
				when _tempVariables.ContainsVariable(stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty):
				_useTemp = false;
				retVal = _tempVariables.GetVariable(stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty);
				foundVariable = true;
				break;
			case Variable when _script.RefObject != null &&
			                   _script.RefObject.ContainsVariable(
				                   stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty
			                   ):
				retVal        = _script.RefObject[stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty];
				foundVariable = true;
				break;
			case Variable
				when Script.GlobalVariables.ContainsVariable(
					stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty
				):
				retVal        = Script.GlobalVariables[stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty];
				foundVariable = true;
				break;
			case Variable when Script.GlobalObjects.ContainsKey(
				stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty
			):
				retVal = Script.GlobalObjects[stackEntry.GetValue()?.ToString()?.ToLower() ?? string.Empty]
				               .ToStackEntry();
				foundVariable = true;
				break;
			default:
				break;
		}

		if (type is Variable && !foundVariable && !returnStackEntryIfNotFound)
		{
			Tools.DebugLine(
				$"GetEntry, Type: {type}, FoundVariable: {foundVariable}, StackEntry: {stackEntry.GetValue()}, ReturnStackEntryIfNotFound: {returnStackEntryIfNotFound}"
			);
			retVal = 0.ToStackEntry();
		}

		return retVal;
	}

	private T? GetEntryValue<T>(IStackEntry stackEntry, StackEntryType? overrideStackType = null, bool returnStackEntryIfNotFound = false) =>
		(T?)GetEntry(stackEntry, overrideStackType, returnStackEntryIfNotFound).GetValue();

	public void Reset()
	{
		_firstRun = true;
		_tempVariables.Clear();
	}
}