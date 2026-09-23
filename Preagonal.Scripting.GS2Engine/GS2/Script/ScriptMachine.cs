using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Exceptions;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.ByteCode;
using Preagonal.Scripting.GS2Engine.Models;
using static Preagonal.Scripting.GS2Engine.Enums.StackEntryType;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptMachine
{
	private readonly Script                              _script;
	private readonly ScriptVariable                      _rootTempVariables       = new();
	private readonly HashSet<string>                     _rootTempAliases         = new(StringComparer.OrdinalIgnoreCase);
	private readonly AsyncLocal<ExecutionState?>         _executionState          = new();
	private readonly AsyncLocal<ScriptExecutionContext?> _scriptExecutionContext  = new();
	private readonly Lock                                _reportedDiagnosticsSync = new();
	private readonly HashSet<string>                     _reportedDiagnostics     = new(StringComparer.Ordinal);

	private sealed record MissingMemberReference(string ObjectName, object Instance);

	private sealed record MissingObjectCreatorResult;

	internal ScriptExecutionContext? ScriptExecutionContext
	{
		get => _scriptExecutionContext.Value;
		set => _scriptExecutionContext.Value = value;
	}

	private ExecutionState         State            => _executionState.Value ?? throw new InvalidOperationException("No active script execution.");
	private Stack<ScriptVariable>  _tempFrames      => State.TempFrames;
	private Stack<ScriptVariable>  _localFrames     => State.LocalFrames;
	private Stack<HashSet<string>> _tempAliasFrames => State.TempAliasFrames;
	private Stack<string>          _functionFrames  => State.FunctionFrames;

	private ScriptVariable? _receiverOverride
	{
		get => _executionState.Value?.ReceiverOverride;
		set => State.ReceiverOverride = value;
	}

	private int _indexPos
	{
		get => State.IndexPos;
		set => State.IndexPos = value;
	}

	private bool _useTemp
	{
		get => State.UseTemp;
		set => State.UseTemp = value;
	}

	private string _activeEvent
	{
		get => State.ActiveEvent;
		set => State.ActiveEvent = value;
	}

	private ScriptVariable  ThisObject => _executionState.Value?.ReceiverOverride ?? _script;
	private ScriptVariable? RefObject  => _executionState.Value?.ReceiverOverride ?? _script.RefObject;

	private delegate IStackEntry OpcodeHandler(ScriptCom op, ref int index);

	private readonly Dictionary<Opcode, OpcodeHandler> _opcodeHandlers = new();

	public ScriptMachine(Script script)
	{
		_script = script;
		registerOpcodeHandlers();
	}

	public ScriptVariable CurrentReceiver => ThisObject;

	public Script CurrentScript => _script;

	public bool HasFunction(ScriptVariable receiver, string functionName)
	{
		var normalizedFunctionName = NormalizeScriptVariableName(functionName);
		if (string.IsNullOrEmpty(normalizedFunctionName)) return false;

		if (receiver.Properties.TryGetProperty(normalizedFunctionName, out var property) && property.IsFunction)
			return true;

		if (receiver.TryGetVariable(normalizedFunctionName, out var functionEntry) && functionEntry?.GetValue() is ScriptCommand or IScriptProperty { IsFunction: true })
			return true;

		var requirePublic = !ReferenceEquals(receiver, ThisObject);
		if (receiver is Script script && HasScriptFunction(script, normalizedFunctionName, requirePublic))
			return true;

		if (receiver.OwnerScript is { } ownerScript && !string.IsNullOrWhiteSpace(receiver.Name) && HasScriptFunction(ownerScript, $"{receiver.Name}.{normalizedFunctionName}", requirePublic))
			return true;

		return HasJoinedClassFunction(receiver, normalizedFunctionName, requirePublic, []);
	}

	private ScriptVariable _tempVariables => _tempFrames.Count > 0 ? _tempFrames.Peek() : _rootTempVariables;

	private ScriptVariable _localVariables => _localFrames.Count > 0 ? _localFrames.Peek() : _tempVariables;

	private HashSet<string> _tempAliases => _tempAliasFrames.Count > 0 ? _tempAliasFrames.Peek() : _rootTempAliases;

	private ScriptVariable CreateTempFrame()
	{
		var frame = new ScriptVariable();
		frame.AddOrUpdate(_rootTempVariables);
		return frame;
	}

	private HashSet<string> CreateTempAliasFrame() => new(_rootTempAliases, StringComparer.OrdinalIgnoreCase);

	private string CurrentFunctionName => _functionFrames.Count > 0 ? _functionFrames.Peek() : string.Empty;

	internal bool IsExecuting => _executionState.Value?.FunctionFrames.Count > 0;

	private void registerOpcodeHandlers()
	{
	}

	public Task<IStackEntry> Execute(string functionName, Stack<IStackEntry>? callStack = null, ScriptVariable? receiverOverride = null, bool inheritTempFrame = false) => Execute(functionName, callStack, receiverOverride, inheritTempFrame, null);

	internal Task<IStackEntry> ExecuteScript(string eventName, Stack<IStackEntry>? callStack = null, ScriptVariable? receiverOverride = null) => Execute(string.Empty, callStack, receiverOverride, false, eventName);

	private async Task<IStackEntry> Execute(string functionName, Stack<IStackEntry>? callStack, ScriptVariable? receiverOverride, bool inheritTempFrame, string? activeEvent)
	{
		var ownsExecutionState = _executionState.Value == null;
		if (ownsExecutionState)
			_executionState.Value = new();

		var previousReceiver    = _receiverOverride;
		var previousIndexPos    = _indexPos;
		var previousActiveEvent = _activeEvent;
		var previousWithScope   = State.WithScope;
		var currentReceiver = previousWithScope.Count > 0 ? previousWithScope.Peek().GetValue() : ThisObject;
		if (receiverOverride != null && !ReferenceEquals(receiverOverride, currentReceiver))
			State.WithScope = new();
		var withDepth = State.WithScope.Count;
		_activeEvent = activeEvent?.ToLowerInvariant() ?? string.Empty;
		if (receiverOverride != null)
			_receiverOverride = receiverOverride;

		var pushTempFrame = !inheritTempFrame;
		if (pushTempFrame)
		{
			_tempFrames.Push(CreateTempFrame());
			_tempAliasFrames.Push(CreateTempAliasFrame());
		}

		_localFrames.Push(new());
		_functionFrames.Push(functionName);
		try
		{
			return await ExecuteCore(functionName, callStack).ConfigureAwait(false);
		}
		finally
		{
			Tools.DebugLine($"[SCRIPT] leave {_script.Name}.{functionName.ToLowerInvariant()}");
			_functionFrames.Pop();
			_localFrames.Pop();
			if (pushTempFrame)
			{
				_tempAliasFrames.Pop();
				_tempFrames.Pop();
			}

			_indexPos         = previousIndexPos;
			_receiverOverride = previousReceiver;
			_activeEvent      = previousActiveEvent;
			while (State.WithScope.Count > withDepth)
				State.WithScope.Pop();
			State.WithScope = previousWithScope;
			if (ownsExecutionState)
				_executionState.Value = null;
		}
	}

	private async Task<IStackEntry> ExecuteCore(string functionName, Stack<IStackEntry>? callStack)
	{
		var normalizedFunctionName = functionName.Trim().ToLowerInvariant();
		var executeWholeScript     = normalizedFunctionName.Length == 0;
		Tools.DebugLine($"[SCRIPT] enter {_script.Name}.{(executeWholeScript ? "<script>" : normalizedFunctionName)}");
		FunctionParams value = default;
		if (!executeWholeScript && !_script.Functions.TryGetValue(normalizedFunctionName, out value))
		{
			if (TryGetJoinedClassFunction(ThisObject, normalizedFunctionName, out var joinedCommand))
				return joinedCommand(this, callStack?.ToArray() ?? []);

			Tools.DebugLine($"[SCRIPT] missing {_script.Name}.{normalizedFunctionName}");
			return 0.ToStackEntry();
		}

		Stack<IStackEntry> stack = new();
		SetCallParameters(callStack);

		var desiredStart        = executeWholeScript ? 0 : value.BytecodePosition;
		var otherFunctionStarts = executeWholeScript ? null : _script.Functions.Values.Select(function => function.BytecodePosition).Where(position => position != desiredStart).ToHashSet();
		var index               = desiredStart;
		Tools.DebugLine($"[SCRIPT] range {normalizedFunctionName} start={desiredStart}");

		const int maxLoopCount = 100000;

		IStackEntry?                 opCopy             = null;
		Stack<IStackEntry>           opWith             = State.WithScope;
		Dictionary<int, IStackEntry> callStackRegisters = new();
		Dictionary<int, int>         loopCounts         = new();
		var                          lastIncrement      = "<none>";

		IStackEntry ReadCallStackRegister(double registerIndex) => callStackRegisters.TryGetValue(ToScriptInt(registerIndex), out var entry) ? GetEntry(entry, returnStackEntryIfNotFound: true) : 0.ToStackEntry();

		IStackEntry ReadRawCallStackRegister(double registerIndex) => callStackRegisters.TryGetValue(ToScriptInt(registerIndex), out var entry) ? entry : 0.ToStackEntry();

		void                      StoreCallStackRegister(double registerIndex, IStackEntry entry) => callStackRegisters[ToScriptInt(registerIndex)] = CopyStackEntry(entry);
		static Stack<IStackEntry> CreateCallStack(IEnumerable<IStackEntry> entries)               => new(entries.Reverse());

		IStackEntry PopOrZero()                       => stack.Count > 0 ? stack.Pop() : 0.ToStackEntry();
		IStackEntry PopOrEmptyString()                => stack.Count > 0 ? stack.Pop() : string.Empty.ToStackEntry();
		IStackEntry PopOrCopyOrZero()                 => stack.Count > 0 ? stack.Pop() : opCopy ?? 0.ToStackEntry();
		string      DescribeEntry(IStackEntry? entry) => entry == null ? "<empty>" : $"{entry.Type}:{Tools.ToScriptString(UnwrapScriptValue(entry.GetValue()))}";

		Tools.DebugLine($"Starting to execute function \"{_script.Name}.{functionName}\"");
		while (index < _script.Bytecode.Length)
		{
			var curIndex = index;
			// Inline function bodies are jumped over; only falling into another entry point ends this call.
			if (otherFunctionStarts?.Contains(curIndex) == true)
				return 0.ToStackEntry();

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
					Tools.DebugLine($"[SCRIPT] set_index target={op.Value} current={curIndex}");
					index     = (int)op.Value;
					_indexPos = index;
					break;
				case Opcode.OP_SET_INDEX_TRUE:
					var sitCompEntry = GetEntry(PopOrZero());
					var sitCompVar   = sitCompEntry.GetValue();
					var sitCompare   = IsScriptTruthy(sitCompVar);
					Tools.DebugLine($"[SCRIPT] set_index_true target={op.Value} value={DescribeEntry(sitCompEntry)} truthy={sitCompare}");

					if (sitCompare)
					{
						index     = (int)op.Value;
						_indexPos = index;
					}

					break;
				case Opcode.OP_OR:
					var orCompEntry = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					var orCompVar   = orCompEntry.GetValue();
					var orCompare   = IsScriptTruthy(orCompVar);

					if (orCompare)
					{
						index     = (int)op.Value;
						_indexPos = index;
						stack.Push(orCompEntry);
					}

					break;
				case Opcode.OP_IF:
					curIndex = stack.Count;
					if (curIndex < 0) return 1.ToStackEntry();

					var ifCompVar = GetEntry(PopOrZero()).GetValue();
					var ifCompare = IsScriptTruthy(ifCompVar);

					if (!ifCompare)
					{
						index     = (int)op.Value;
						_indexPos = index;
					}

					break;
				case Opcode.OP_AND:
					var andCompEntry = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					var andCompVar   = andCompEntry.GetValue();
					var andCompare   = IsScriptTruthy(andCompVar);

					if (!andCompare)
					{
						index     = (int)op.Value;
						_indexPos = index;
						stack.Push(andCompEntry);
					}

					break;
				case Opcode.OP_CALL:
					var rawCallEntry = PopOrZero();
					// Resolve a qualified call by receiver and name, independently of same-named fields.
					if (rawCallEntry.Type == Variable && rawCallEntry.GetParent() is ScriptVariable callReceiver && TryGetWithFunctionEntry(callReceiver.ToStackEntry(), rawCallEntry.GetValue()?.ToString() ?? string.Empty, out var qualifiedFunction))
						rawCallEntry = qualifiedFunction;
					var callEntry     = rawCallEntry.Type == ScriptProperty ? rawCallEntry : GetEntry(rawCallEntry, returnStackEntryIfNotFound: true, reportMissingProperty: false, resolveFunctionReferences: false);
					var cmd           = callEntry.Type == ScriptProperty ? callEntry.GetValue() : GetEntryValue<object>(callEntry, returnStackEntryIfNotFound: true, reportMissingProperty: false, resolveFunctionReferences: false);
					var rawCommand    = rawCallEntry.Type is StackEntryType.String or Variable ? rawCallEntry.GetValue()?.ToString() ?? string.Empty : cmd?.ToString() ?? string.Empty;
					var callParams    = new List<IStackEntry>();
					var rawCallParams = new List<IStackEntry>();

					while (stack.Count > 0 && stack.Peek().Type != ArrayStart)
					{
						var parameterEntry = stack.Pop();
						rawCallParams.Add(parameterEntry);
						var resolvedParameterEntry = ResolveEntryForRead(parameterEntry, opWith, returnStackEntryIfNotFound: true);
						var parameterEntryValue    = resolvedParameterEntry.GetValue();

						if (parameterEntryValue is IScriptProperty { HasReadMethod: true } parameterProperty)
						{
							parameterEntryValue = parameterProperty.Read(ResolveScriptPropertyInstance(parameterProperty, resolvedParameterEntry.GetParent())!);
						}

						callParams.Add(parameterEntryValue.ToStackEntry());
					}

					if (stack.Count > 0) stack.Pop();
					if (rawCallEntry.Type is StackEntryType.String or Variable && opWith is { Count: > 0 } && TryGetWithFunctionEntry(opWith.Peek(), rawCommand, out var withFunctionEntry))
					{
						callEntry = withFunctionEntry;
						cmd       = GetEntryValue<object>(callEntry, returnStackEntryIfNotFound: true, reportMissingProperty: false);
					}
					else if (rawCallEntry.Type is StackEntryType.String or Variable &&
					         _receiverOverride != null &&
					         !_script.Functions.ContainsKey(rawCommand.ToLowerInvariant()) &&
					         TryGetWithFunctionEntry(ThisObject.ToStackEntry(), rawCommand, out var receiverFunctionEntry))
					{
						callEntry = receiverFunctionEntry;
						cmd       = GetEntryValue<object>(callEntry, returnStackEntryIfNotFound: true, reportMissingProperty: false);
					}

					Tools.DebugLine($"[SCRIPT] call {_script.Name}.{normalizedFunctionName} -> {rawCommand} ({callEntry.Type}) params={callParams.Count} rawparams={rawCallParams.Count}");
					switch (callEntry.Type)
					{
						case StackEntryType.String or Variable when _script.Functions.ContainsKey(cmd?.ToString()?.ToLowerInvariant() ?? string.Empty):
							stack.Push(await Execute(cmd?.ToString()?.ToLowerInvariant() ?? string.Empty, CreateCallStack(callParams), null, false, null).ConfigureAwait(false));
							break;
						case StackEntryType.String or Variable when TryGetWithFunctionEntry(ThisObject.ToStackEntry(), cmd?.ToString() ?? string.Empty, out var thisFunctionEntry):
							var thisFunctionCommand = GetEntryValue<object>(thisFunctionEntry, returnStackEntryIfNotFound: true);
							switch (thisFunctionEntry.Type)
							{
								case Function:
									stack.Push((thisFunctionCommand as ScriptCommand)?.Invoke(this, callParams.ToArray()) ?? 0.ToStackEntry());
									break;
								case ScriptProperty:
									var thisFunctionProperty = thisFunctionCommand as IScriptProperty;
									var thisFunctionInstance = thisFunctionProperty != null ? ResolveScriptPropertyInstance(thisFunctionProperty, thisFunctionEntry.GetParent()) : null;
									stack.Push((thisFunctionProperty?.Call(this, thisFunctionInstance!, callParams.ToArray()) ?? 0).ToStackEntry());
									break;
								default:
									stack.Push(0.ToStackEntry());
									break;
							}

							break;
						case StackEntryType.String or Variable when TryGetJoinedClassFunction(ThisObject, cmd?.ToString() ?? string.Empty, out var joinedFunction):
							stack.Push(joinedFunction.Invoke(this, callParams.ToArray()));
							break;
						case StackEntryType.String or Variable when TryCallBuiltInFunction(cmd?.ToString(), callParams, rawCallParams, out var builtInResult):
							stack.Push(builtInResult);
							break;
						case StackEntryType.String or Variable when TryCallReceiverPropertyFunction(cmd?.ToString(), callParams, out var receiverResult):
							stack.Push(receiverResult);
							break;

						/*
						case StackEntryType.String or Variable when Functions.TryGetValue(
							cmd?.ToString()?.ToLower() ?? string.Empty,
						out var command
					):
						stack.Push(command.Invoke(this, callParams.ToArray()));
						break;
					*/

						case StackEntryType.String or Variable:
							LogMissingFunction(rawCommand);
							stack.Push(0.ToStackEntry());
							break;
						case Function:
							stack.Push((cmd as ScriptCommand)?.Invoke(this, callParams.ToArray()) ?? 0.ToStackEntry());
							break;
						case ScriptProperty:
							var scriptProperty = cmd as IScriptProperty;
							var inst           = scriptProperty != null ? ResolveScriptPropertyInstance(scriptProperty, callEntry.GetParent()) : null;

							if (scriptProperty != null && inst == null && opWith is { Count: > 0 })
								inst = ResolveScriptPropertyInstance(scriptProperty, opWith.Peek().GetValue());

							stack.Push((scriptProperty?.Call(this, inst!, callParams.ToArray()) ?? 0).ToStackEntry());
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
					var retVal = GetEntry(ret);

					if (retVal.Type == ScriptProperty && retVal.GetValue<IScriptProperty>() is { } retScriptProperty)
					{
						var inst = ResolveScriptPropertyInstance(retScriptProperty, retVal.GetParent());
						retVal = retScriptProperty.Read(inst!).ToStackEntry();
					}

					return retVal;


				case Opcode.OP_SLEEP:
					var sleep = GetEntryValue<double>(stack.Pop());
					sleep *= 1000;
					await Task.Delay((int)sleep).ConfigureAwait(false);
					break;
				case Opcode.OP_WAITFOR:
					if (stack.Count > 0)
						_ = GetEntryValue<double>(stack.Pop());
					if (stack.Count > 0)
						_ = GetEntryValue<TString>(stack.Pop());
					if (stack.Count > 0)
						_ = GetEntryValue<TString>(stack.Pop(), returnStackEntryIfNotFound: true);
					stack.Push(0.ToStackEntry());
					break;
				case Opcode.OP_CMD_CALL:
					//index = _script.Field170Xc0;
					if ((int)op.Value == index)
					{
						var loopCount = loopCounts.GetValueOrDefault(curIndex) + 1;
						loopCounts[curIndex] = loopCount;
						if (maxLoopCount <= loopCount && !normalizedFunctionName.Equals("ontimeout", StringComparison.OrdinalIgnoreCase))
						{
							var debugVariable = ResolveEntryForRead("i".ToStackEntry(isVariable: true), opWith, returnStackEntryIfNotFound: true);
							var message =
								$"Loop limit exceeded in {_script.Name}.{normalizedFunctionName} at bytecode {curIndex}; i={DescribeEntry(debugVariable)}; stackTop={DescribeEntry(stack.Count > 0 ? stack.Peek() : null)}; withDepth={opWith.Count}; withTop={DescribeEntry(opWith.Count > 0 ? opWith.Peek() : null)}; lastIncrement={lastIncrement}";
							Tools.DebugLine(message);
							throw new ScriptException(message);
						}

						op.LoopCount += 1;
					}
					else
					{
						op.Value             = index;
						op.VariableName      = null;
						loopCounts[curIndex] = 0;
					}

					index = _indexPos;

					break;
				case Opcode.OP_JMP:
					//index = indexPos;
					Tools.DebugLine("");
					break;
				case Opcode.OP_TYPE_NUMBER:
					stack.Push(op.Value.ToStackEntry());
					break;
				case Opcode.OP_TYPE_STRING:
					stack.Push((op.VariableName ?? "").ToStackEntry());
					break;
				case Opcode.OP_TYPE_VAR when op.NormalizedVariableName?.ToString() == "this":
					goto case Opcode.OP_THIS;
				case Opcode.OP_TYPE_VAR:
					stack.Push((op.NormalizedVariableName ?? (TString)string.Empty).ToStackEntry(true));
					break;
				case Opcode.OP_TYPE_ARRAY:
					stack.Push(new StackEntry(ArrayStart, null));
					break;
				case Opcode.OP_TYPE_TRUE:
					stack.Push(1.ToStackEntry());
					break;
				case Opcode.OP_TYPE_FALSE:
					stack.Push(0.ToStackEntry());
					break;
				case Opcode.OP_TYPE_NULL:
					stack.Push(new StackEntry(Null, null));
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
					stack.Push(ConvertToFloatEntry(PopOrZero(), opWith));
					break;
				case Opcode.OP_CONV_TO_STRING:
					stack.Push(ConvertToStringEntry(PopOrZero(), opWith));
					break;
				case Opcode.OP_MEMBER_ACCESS:
					var stackVal          = PopOrZero();
					var memberAccessParam = GetEntryValue<TString>(stackVal, returnStackEntryIfNotFound: true);
					try
					{
						if (stack.Count > 0 && stack.Peek()?.Type == StackEntryType.Array)
						{
							try
							{
								var              memberParent = stack.Pop().GetValue<VariableCollection>();
								var              objTest      = memberParent as ScriptVariable;
								var              props        = objTest?.Properties;
								IScriptProperty? prop         = null;
								props?.TryGetProperty(memberAccessParam ?? string.Empty, out prop);

								if (prop != null)
								{
									stack.Push(prop.ToStackEntry(parent: objTest));
								}
								else
								{
									if (TryGetJoinedClassFunction(objTest, memberAccessParam ?? string.Empty, out var joinedArrayCommand))
										stack.Push(joinedArrayCommand.ToStackEntry());
									else
									{
										var scriptObjectMember = memberParent == null ? null : CreateMemberVariableEntry(memberParent, memberAccessParam ?? "");
										stack.Push(scriptObjectMember ?? 0.ToStackEntry());
									}
								}
							}
							catch (Exception e)
							{
								Tools.DebugLine(e.Message);
							}

							break;
						}

						if (stack.Count > 0 && stack.Peek()?.Type == StackEntryType.Script)
						{
							var scriptStackEntry     = stack.Pop();
							var scriptObject         = scriptStackEntry.GetValue<Script>();
							var stackValFunctionName = memberAccessParam ?? "";
							if (TryGetScriptMemberFunction(scriptObject, stackValFunctionName, out var memberCommand))
								stack.Push(memberCommand.ToStackEntry());
							else if (TryGetJoinedClassFunction(scriptObject, stackValFunctionName, out var joinedMemberCommand))
								stack.Push(joinedMemberCommand.ToStackEntry());
							else
							{
								IScriptProperty? prop = null;
								scriptObject?.Properties.TryGetProperty(memberAccessParam ?? string.Empty, out prop);
								if (prop != null)
								{
									stack.Push(prop.ToStackEntry(parent: scriptObject));
								}
								else
								{
									var scriptObjectMember = scriptObject == null ? null : CreateMemberVariableEntry(scriptObject, memberAccessParam ?? "");
									stack.Push(scriptObjectMember ?? 0.ToStackEntry());
								}
							}

							break;
						}


						var memberAccessEntry  = GetEntry(PopOrZero());
						var memberAccessScript = memberAccessEntry.GetValue<Script>();
						if (TryGetPublicScriptFunction(memberAccessScript, memberAccessParam ?? string.Empty, out var resolvedMemberCommand))
						{
							stack.Push(resolvedMemberCommand.ToStackEntry());
							break;
						}

						var memberAccessObject = memberAccessEntry.GetValue<ScriptVariable>();
						if (TryGetJoinedClassFunction(memberAccessObject, memberAccessParam ?? string.Empty, out var joinedCommand))
						{
							stack.Push(joinedCommand.ToStackEntry());
							break;
						}

						var memberContainer = memberAccessEntry.GetValue<VariableCollection>();
						var member          = memberContainer == null ? null : GetMemberValue(memberContainer.ToStackEntry(), memberAccessParam ?? "");
						stack.Push(member ?? 0.ToStackEntry());
					}
					catch (Exception e)
					{
						Tools.DebugLine(e.Message);
						stack.Push(0.ToStackEntry());
					}

					break;
				case Opcode.OP_CONV_TO_OBJECT:
					stack.Push(ConvertToObjectEntry(PopOrZero(), opWith));
					break;
				case Opcode.OP_ARRAY_END:

					List<object> stackArr = [];
					//keep popping the stack till we hit an array start
					while (stack.Count > 0 && stack.Peek().Type != ArrayStart)
					{
						var arrayEntry = ResolveReadableScriptProperty(ResolveEntryForRead(stack.Pop(), opWith));
						stackArr.Add(arrayEntry.GetValue() ?? 0);
					}

					if (stack.Count > 0)
						stack.Pop(); //pop array start marker off

					stack.Push(new StackEntry(StackEntryType.Array, stackArr)); //push new array onto stack
					break;
				case Opcode.OP_ARRAY_NEW:
					var arraySize = ClampScriptArraySize(ToScriptInt(PopOrZero().GetValue<double>()));
					stack.Push(CreateScriptArray(arraySize).ToStackEntry());
					break;
				case Opcode.OP_SETARRAY:
					var setArraySize = ClampScriptArraySize(ToScriptInt(GetEntry(PopOrZero()).GetValue<double>()));
					var setArray     = GetMutableScriptArray(GetEntry(PopOrZero(), returnStackEntryIfNotFound: true));
					if (setArray != null)
						ResizeScriptArray(setArray, setArraySize);
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
					var makeVarName = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push(GetOrCreateScriptVariable(makeVarName));
					break;
				case Opcode.OP_NEW_OBJECT:
					if (stack.Count == 0)
					{
						stack.Push(0.ToStackEntry());
						break;
					}

					var newObject          = stack.Pop();
					var newObjectParam     = stack.Count > 0 ? stack.Pop() : (stack.Count == 0 && GetRawStackString(newObject).Length > 0 ? "unknown_object".ToStackEntry() : string.Empty.ToStackEntry());
					var newObjectClassName = string.Empty;
					var newObjectName      = string.Empty;
					try
					{
						newObjectClassName = GetRawStackString(newObject);
						newObjectName      = GetRawStackString(newObjectParam);
						if (IsImplicitObjectName(newObjectName) && stack.TryPeek(out var assignmentTarget))
						{
							var targetName = GetRawStackString(assignmentTarget);
							if (!string.IsNullOrEmpty(targetName))
								newObjectName = targetName;
						}

						var newObjectRet = CreateScriptObject(newObjectClassName, newObjectName);
						if (newObjectRet is GuiControl newGuiControl && opWith.Count > 0 && UnwrapScriptValue(opWith.Peek().GetValue()) is GuiControl parentGuiControl)
						{
							parentGuiControl.AddControl(newGuiControl);
						}

						stack.Push(newObjectRet is MissingObjectCreatorResult ? new(StackEntryType.Array, newObjectRet) : newObjectRet?.ToStackEntry() ?? 0.ToStackEntry());
					}
					catch (Exception e)
					{
						Tools.DebugLine($"Error creating {newObjectClassName} '{newObjectName}' in {_script.Name}: {e}");
						stack.Push(0.ToStackEntry());
					}

					break;
				case Opcode.OP_OBJ_FROM_STR:
					var objFromStrName = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push(MakeOldScriptVariable(objFromStrName)?.ToStackEntry() ?? 0.ToStackEntry());
					break;
				case Opcode.OP_INLINE_CONDITIONAL:

					var inlineConditional = PopOrZero();
					if (inlineConditional.GetValue<double>() != 0)
					{
						inlineConditional.SetValue(1.0d);
					}

					stack.Push(inlineConditional);

					break;
				case Opcode.OP_UNKNOWN_45:
					if (stack.Count > 0)
						StoreCallStackRegister(op.Value, stack.Peek());
					break;
				case Opcode.OP_UNKNOWN_46:
					stack.Push(CopyStackEntry(ReadRawCallStackRegister(op.Value)));
					break;
				case Opcode.OP_UNKNOWN_47:
					if (stack.Count > 0)
						stack.Push(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true));
					break;
				case Opcode.OP_ASSIGN:
					var val      = PopOrZero();
					var variable = (stack.Count == 0 ? opCopy : /*GetEntry*/(PopOrZero())) ?? 0.ToStackEntry();
					while (!IsAssignmentTargetEntry(variable) && stack.Count > 0 && IsAssignmentTargetEntry(stack.Peek()))
						variable = PopOrZero();
					opCopy = CopyStackEntry(variable);
					AssignValue(variable, val, opWith);
					break;
				case Opcode.OP_FUNC_PARAMS_END:
					while (stack.Count > 0)
					{
						var funcParam = stack.Pop();
						try
						{
							if (callStack is { Count: > 0 })
							{
								var funcParamVal  = callStack.Pop();
								var funcParamName = (funcParam.GetValue() ?? "").ToString()?.ToLowerInvariant() ?? string.Empty;
								Tools.DebugLine($"[SCRIPT] param {funcParamName}={DescribeEntry(funcParamVal)}");
								if (funcParam.Type == Variable && funcParam.GetParent() is VariableCollection parentCollection)
								{
									if (ReferenceEquals(parentCollection, _tempVariables))
										_tempAliases.Add(funcParamName);
									parentCollection.AddOrUpdate(funcParamName, funcParamVal ?? 0.ToStackEntry());
								}
								else
								{
									_localVariables.AddOrUpdate(funcParamName, funcParamVal ?? 0.ToStackEntry());
								}
							}
						}
						catch (Exception e)
						{
							// ignored
							Tools.DebugLine(e.Message);
						}
					}

					_useTemp = false;
					index    = _indexPos;
					break;
				case Opcode.OP_INC:
					var incVar    = stack.Count > 0 ? stack.Pop() : opCopy ?? 0.ToStackEntry();
					var incTarget = IncrementEntry(incVar, 1.0d, opWith);
					lastIncrement = $"{curIndex}:{DescribeEntry(incVar)}->{DescribeEntry(incTarget)}";
					stack.Push(incVar);
					break;
				case Opcode.OP_DEC:
					var decVar    = stack.Count > 0 ? stack.Pop() : opCopy ?? 0.ToStackEntry();
					var decTarget = IncrementEntry(decVar, -1.0d, opWith);
					lastIncrement = $"{curIndex}:{DescribeEntry(decVar)}->{DescribeEntry(decTarget)}";
					stack.Push(decVar);
					break;
				case Opcode.OP_UNKNOWN_54:
					if (stack.Count >= 3)
					{
						var memberAssignValue  = stack.Pop();
						var memberAssignName   = GetEntryValue<TString>(stack.Pop(), returnStackEntryIfNotFound: true)?.ToString() ?? string.Empty;
						var memberAssignTarget = GetMemberValue(stack.Pop(), memberAssignName, createMissingParent: true);

						AssignValue(memberAssignTarget, memberAssignValue, opWith);
					}

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
					stack.Push((divA != 0.0d ? divB / divA : 0.0d).ToStackEntry());
					break;
				case Opcode.OP_MOD:
					var modA = GetEntryValue<double>(stack.Pop());
					var modB = GetEntryValue<double>(stack.Pop());
					stack.Push((modA != 0.0d ? modB - modA * Math.Floor(modB / modA) : 0.0d).ToStackEntry());
					break;
				case Opcode.OP_POW:
					var powA = GetEntryValue<double>(stack.Pop());
					var powB = GetEntryValue<double>(stack.Pop());
					var pow  = Math.Pow(powB, powA);
					stack.Push((double.IsNaN(pow) ? 0.0d : pow).ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_200:
				case Opcode.OP_UNKNOWN_201:
				case Opcode.OP_UNKNOWN_202:
				case Opcode.OP_UNKNOWN_203:
				case Opcode.OP_UNKNOWN_204:
				case Opcode.OP_UNKNOWN_205:
					var optimizedImmediateLeft = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					stack.Push(CalculateOptimizedImmediate(op.OpCode, ToScriptDouble(optimizedImmediateLeft.GetValue()), op.Value).ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_206:
				case Opcode.OP_UNKNOWN_207:
					var optimizedLogicalLeft = ToScriptDouble(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
					stack.Push(CalculateOptimizedLogical(op.OpCode, optimizedLogicalLeft, op.Value).ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_208:
				case Opcode.OP_UNKNOWN_209:
				case Opcode.OP_UNKNOWN_210:
				case Opcode.OP_UNKNOWN_211:
				case Opcode.OP_UNKNOWN_212:
				case Opcode.OP_UNKNOWN_213:
				case Opcode.OP_UNKNOWN_214:
				case Opcode.OP_UNKNOWN_215:
					var optimizedAssignRight  = ToScriptDouble(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
					var optimizedAssignLeft   = ToScriptDouble(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
					var optimizedAssignTarget = stack.Pop();
					var optimizedAssignValue = op.OpCode is Opcode.OP_UNKNOWN_214 or Opcode.OP_UNKNOWN_215
						? CalculateOptimizedLogical((Opcode)((byte)op.OpCode - 148), optimizedAssignLeft, optimizedAssignRight)
						: CalculateOptimizedImmediate((Opcode)((byte)op.OpCode - 8), optimizedAssignLeft, optimizedAssignRight);
					AssignValue(optimizedAssignTarget, optimizedAssignValue.ToStackEntry(), opWith);
					opCopy = CopyStackEntry(optimizedAssignTarget);
					break;
				case Opcode.OP_UNKNOWN_216:
				case Opcode.OP_UNKNOWN_217:
				case Opcode.OP_UNKNOWN_218:
				case Opcode.OP_UNKNOWN_219:
				case Opcode.OP_UNKNOWN_220:
				case Opcode.OP_UNKNOWN_221:
				case Opcode.OP_UNKNOWN_222:
				case Opcode.OP_UNKNOWN_223:
					var optimizedImmediateAssignLeft   = ToScriptDouble(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
					var optimizedImmediateAssignTarget = stack.Pop();
					var optimizedImmediateAssignValue = op.OpCode is Opcode.OP_UNKNOWN_222 or Opcode.OP_UNKNOWN_223
						? CalculateOptimizedLogical((Opcode)((byte)op.OpCode - 156), optimizedImmediateAssignLeft, op.Value)
						: CalculateOptimizedImmediate((Opcode)((byte)op.OpCode - 16), optimizedImmediateAssignLeft, op.Value);
					AssignValue(optimizedImmediateAssignTarget, optimizedImmediateAssignValue.ToStackEntry(), opWith);
					opCopy = CopyStackEntry(optimizedImmediateAssignTarget);
					break;
				case Opcode.OP_UNKNOWN_224:
				case Opcode.OP_UNKNOWN_225:
				case Opcode.OP_UNKNOWN_226:
				case Opcode.OP_UNKNOWN_227:
					var optimizedCompareLeft  = ResolveEntryForComparison(PopOrZero(), opWith);
					var optimizedCompareRight = ResolveReadableScriptProperty(op.Value.ToStackEntry());
					var optimizedCompare      = CompareScriptValues(optimizedCompareLeft, optimizedCompareRight);
					stack.Push(IsOptimizedImmediateComparisonTrue(op.OpCode, optimizedCompare).ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_228:
					stack.Push(ConvertToFloatEntry(ReadCallStackRegister(op.Value), opWith));
					break;
				case Opcode.OP_UNKNOWN_229:
					stack.Push(ConvertToStringEntry(ReadCallStackRegister(op.Value), opWith));
					break;
				case Opcode.OP_UNKNOWN_230:
					stack.Push(ConvertToObjectEntry(ReadRawCallStackRegister(op.Value), opWith));
					break;
				case Opcode.OP_UNKNOWN_231:
				case Opcode.OP_UNKNOWN_232:
					if (callStackRegisters.TryGetValue(ToScriptInt(op.Value), out var registerEntry))
					{
						var registerTarget = IncrementEntry(registerEntry, op.OpCode == Opcode.OP_UNKNOWN_231 ? 1.0d : -1.0d, opWith);
						lastIncrement = $"{curIndex}:{DescribeEntry(registerEntry)}->{DescribeEntry(registerTarget)}";
					}

					break;
				case Opcode.OP_UNKNOWN_233:
					var copiedRegisterEntry = ReadRawCallStackRegister(op.Value);
					stack.Push(CopyStackEntry(copiedRegisterEntry));
					stack.Push(CopyStackEntry(copiedRegisterEntry));
					break;
				case Opcode.OP_UNKNOWN_234:
					stack.Push(stack.Count > 0 ? GetMemberValue(stack.Pop(), op.VariableName ?? string.Empty, createMissingParent: true) : 0.ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_235:
					stack.Push(ConvertToObjectEntry((op.VariableName ?? string.Empty).ToString().ToStackEntry(true), opWith));
					break;
				case Opcode.OP_UNKNOWN_236:
					stack.Push(ConvertToFloatEntry(stack.Count > 0 ? GetMemberValue(stack.Pop(), op.VariableName ?? string.Empty) : 0.ToStackEntry(), opWith));
					break;
				case Opcode.OP_UNKNOWN_237:
					stack.Push(ConvertToStringEntry(stack.Count > 0 ? GetMemberValue(stack.Pop(), op.VariableName ?? string.Empty) : 0.ToStackEntry(), opWith));
					break;
				case Opcode.OP_UNKNOWN_238:
					stack.Push(ConvertToObjectEntry(stack.Count > 0 ? GetMemberValue(stack.Pop(), op.VariableName ?? string.Empty) : 0.ToStackEntry(), opWith));
					break;
				case Opcode.OP_UNKNOWN_239:
					stack.Push(stack.Count > 0 ? GetMemberValue(stack.Pop(), op.VariableName ?? string.Empty) : 0.ToStackEntry());
					break;
				case Opcode.OP_UNKNOWN_240:
					var optimizedArrayTarget = stack.Count > 0 ? GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue() : null;
					stack.Push(GetScriptArrayCellEntry(optimizedArrayTarget, ToScriptInt(op.Value)));
					break;
				case Opcode.OP_UNKNOWN_241:
					stack.Push(GetEntry((op.VariableName ?? string.Empty).ToString().ToStackEntry(true), returnStackEntryIfNotFound: true));
					break;
				case Opcode.OP_UNKNOWN_242:
					if (stack.Count > 0)
						StoreCallStackRegister(op.Value, stack.Peek());
					break;
				case Opcode.OP_UNKNOWN_66:
				case Opcode.OP_UNKNOWN_67:
					var optimizedLogicalRight     = ToScriptDouble(GetEntry(PopOrZero(), returnStackEntryIfNotFound: true).GetValue());
					var optimizedLogicalStackLeft = ToScriptDouble(GetEntry(PopOrZero(), returnStackEntryIfNotFound: true).GetValue());
					stack.Push(CalculateOptimizedLogical(op.OpCode, optimizedLogicalStackLeft, optimizedLogicalRight).ToStackEntry());
					break;
				case Opcode.OP_NOT:
					var notVar = ToScriptDouble(ResolveEntryForComparison(PopOrZero(), opWith).GetValue());

					stack.Push((notVar == 0 ? true : false).ToStackEntry());
					break;
				case Opcode.OP_UNARYSUB:
					stack.Push((-GetEntryValue<double>(PopOrZero())).ToStackEntry());
					break;
				case Opcode.OP_EQ:
					var eqRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var eqLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push(ScriptEntriesEqual(eqLeft, eqRight).ToStackEntry());
					break;
				case Opcode.OP_NEQ:
					var neqRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var neqLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push((!ScriptEntriesEqual(neqLeft, neqRight)).ToStackEntry());
					break;
				case Opcode.OP_LT:
					var ltRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var ltLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push((CompareScriptValues(ltLeft, ltRight) < 0).ToStackEntry());
					break;
				case Opcode.OP_GT:
					var gtRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var gtLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push((CompareScriptValues(gtLeft, gtRight) > 0).ToStackEntry());
					break;
				case Opcode.OP_LTE:
					var lteRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var lteLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push((CompareScriptValues(lteLeft, lteRight) <= 0).ToStackEntry());
					break;
				case Opcode.OP_GTE:
					var gteRight = ResolveEntryForComparison(PopOrZero(), opWith);
					var gteLeft  = ResolveEntryForComparison(PopOrCopyOrZero(), opWith);
					stack.Push((CompareScriptValues(gteLeft, gteRight) >= 0).ToStackEntry());
					break;
				case Opcode.OP_BWO:
					var bwoRight = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					var bwoLeft  = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					stack.Push(((double)(bwoLeft | bwoRight)).ToStackEntry());
					break;
				case Opcode.OP_BWA:
					var bwaRight = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					var bwaLeft  = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					stack.Push(((double)(bwaLeft & bwaRight)).ToStackEntry());
					break;
				case Opcode.OP_BWX:
					var bwxRight = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					var bwxLeft  = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					stack.Push(((double)(bwxLeft ^ bwxRight)).ToStackEntry());
					break;
				case Opcode.OP_BWI:
					stack.Push(((double)~ToScriptInt(GetEntryValue<double>(stack.Pop()))).ToStackEntry());
					break;
				case Opcode.OP_BW_LEFTSHIFT:
					var leftShiftRight = ToScriptInt(GetEntryValue<double>(stack.Pop())) & 31;
					var leftShiftLeft  = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					stack.Push(((double)(leftShiftLeft << leftShiftRight)).ToStackEntry());
					break;
				case Opcode.OP_BW_RIGHTSHIFT:
					var rightShiftRight = ToScriptInt(GetEntryValue<double>(stack.Pop())) & 31;
					var rightShiftLeft  = ToScriptInt(GetEntryValue<double>(stack.Pop()));
					stack.Push(((double)(rightShiftLeft >> rightShiftRight)).ToStackEntry());
					break;
				case Opcode.OP_IN_RANGE:
					var rangeEnd        = GetEntry(stack.Pop()).GetValue<double>();
					var rangeStart      = GetEntry(stack.Pop()).GetValue<double>();
					var rangeValueEntry = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var rangeMode       = ToScriptInt(op.Value);
					var rangeValues     = GetArrayValues(rangeValueEntry.GetValue());

					stack.Push((rangeValues is { Count: > 0 } ? ValuesInRange(rangeValues, rangeStart, rangeEnd, rangeMode) : ValueInRange(rangeValueEntry.GetValue<double>(), rangeStart, rangeEnd, rangeMode)).ToStackEntry());
					break;
				case Opcode.OP_IN_OBJ:
					var inObj          = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var isIn           = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var inObjValues    = GetArrayValues(inObj.GetValue());
					var inNeedleValues = GetArrayValues(isIn.GetValue());

					stack.Push((inObjValues != null && (inNeedleValues is { Count: > 0 } ? ContainsAllScriptValues(inObjValues, inNeedleValues) : IndexOfScriptValue(inObjValues, isIn.GetValue()) >= 0)).ToStackEntry());
					break;
				case Opcode.OP_OBJ_INDEX:
					var objIndexNeedle      = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					var objIndexTargetEntry = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var objIndexTarget      = objIndexTargetEntry.GetValue();
					var objIndex = objIndexTargetEntry.Type == StackEntryType.String || objIndexTarget is TString or string
						? (double)Tools.ToScriptString(objIndexTarget).IndexOf(Tools.ToScriptString(objIndexNeedle), StringComparison.Ordinal)
						: IndexOfScriptValue(GetArrayValues(objIndexTarget), objIndexNeedle);
					stack.Push(objIndex.ToStackEntry());
					break;
				case Opcode.OP_OBJ_TYPE:
					stack.Push(GetScriptObjectType(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true)).ToStackEntry());
					break;
				case Opcode.OP_FORMAT:
					var format  = stack.Count > 0 ? stack.Pop() : string.Empty.ToStackEntry();
					var objects = new List<object?>();
					while (stack.Count > 0 && stack.Peek().Type != ArrayStart)
						objects.Add(ResolveReadableScriptProperty(ResolveEntryForRead(stack.Pop(), opWith, returnStackEntryIfNotFound: true)).GetValue());
					if (stack.Count > 0)
						stack.Pop();
					var formatted = Tools.Format(GetEntryValue<TString>(format) ?? "", objects.ToArray());
					stack.Push(formatted.ToStackEntry());
					break;
				case Opcode.OP_INT:
					stack.Push(ToScriptInt(GetEntryValue<double>(PopOrZero())).ToStackEntry());
					break;
				case Opcode.OP_ABS:
					stack.Push(Math.Abs(GetEntryValue<double>(PopOrZero())).ToStackEntry());
					break;
				case Opcode.OP_RANDOM:
					var randomEnd   = GetEntryValue<double>(stack.Pop());
					var randomStart = GetEntryValue<double>(stack.Pop());
					stack.Push(GetRandomValue(randomStart, randomEnd).ToStackEntry());
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
					stack.Push(Math.Exp(GetEntryValue<double>(stack.Pop())).ToStackEntry());
					break;
				case Opcode.OP_LOG:
					var logValue = GetEntryValue<double>(stack.Pop());
					var logBase  = GetEntryValue<double>(stack.Pop());
					var logResult = logBase switch
					{
						10.0d  => Math.Log10(logValue),
						> 0.0d => Math.Log(logValue) / Math.Log(logBase),
						_      => 0.0d,
					};
					stack.Push((double.IsNaN(logResult) ? 0.0d : logResult).ToStackEntry());
					break;
				case Opcode.OP_MIN:
					var minRight = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var minLeft  = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					stack.Push((CompareScriptValues(minLeft, minRight) > 0 ? minRight.GetValue() : minLeft.GetValue()).ToStackEntry());
					break;
				case Opcode.OP_MAX:
					var maxRight = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var maxLeft  = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					stack.Push((CompareScriptValues(maxLeft, maxRight) < 0 ? maxRight.GetValue() : maxLeft.GetValue()).ToStackEntry());
					break;
				case Opcode.OP_GETANGLE:
					var angleY = GetEntryValue<double>(stack.Pop());
					var angleX = GetEntryValue<double>(stack.Pop());
					stack.Push(GetAngle(angleX, angleY).ToStackEntry());
					break;
				case Opcode.OP_GETDIR:
					var dirY = GetEntryValue<double>(stack.Pop());
					var dirX = GetEntryValue<double>(stack.Pop());
					stack.Push(GetDirection(dirX, dirY).ToStackEntry());
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
					var indicesNeedle = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true).GetValue();
					var indicesTarget = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					var indicesValues = GetArrayValues(indicesTarget.GetValue());
					var indices       = new List<object?>();
					if (indicesValues != null)
					{
						for (var i = 0; i < indicesValues.Count; i++)
							if (ScriptValuesEqual(indicesValues[i], indicesNeedle))
								indices.Add((double)i);
					}

					stack.Push(indices.ToStackEntry());
					break;
				case Opcode.OP_OBJ_LINK:
					var linkTarget = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					stack.Push(new LinkedStackEntry(linkTarget));
					break;
				case Opcode.OP_OBJ_COMPARE:
					var compareRight = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					var compareLeft  = GetEntry(PopOrZero(), returnStackEntryIfNotFound: true);
					stack.Push(CompareScriptValues(compareLeft, compareRight).ToStackEntry());
					break;
				case Opcode.OP_CHAR:
					stack.Push(((char)ToScriptInt(GetEntryValue<double>(PopOrZero()))).ToString().ToStackEntry());
					break;
				case Opcode.OP_OBJ_TRIM:
					stack.Push((GetEntryValue<TString>(PopOrEmptyString())?.ToString().Trim() ?? string.Empty).ToStackEntry());
					break;
				case Opcode.OP_OBJ_LENGTH:
					stack.Push((GetEntryValue<TString>(PopOrEmptyString())?.ToString().Length ?? 0).ToStackEntry());
					break;
				case Opcode.OP_OBJ_POS:
					var objPosNeedle   = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					var objPosHaystack = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push(objPosHaystack.IndexOf(objPosNeedle, StringComparison.Ordinal).ToStackEntry());
					break;
				case Opcode.OP_JOIN:
					var joinA = GetEntryValue<TString>(stack.Count > 0 ? stack.Pop() : string.Empty.ToStackEntry());
					var joinB = GetEntryValue<TString>(stack.Count > 0 ? stack.Pop() : string.Empty.ToStackEntry());
					stack.Push($"{joinB}{joinA}".ToStackEntry());
					break;
				case Opcode.OP_OBJ_CHARAT:
					var charAtIndex = ToScriptInt(GetEntryValue<double>(PopOrZero()));
					var charAtValue = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push((charAtIndex >= 0 && charAtIndex < charAtValue.Length ? charAtValue[charAtIndex].ToString() : string.Empty).ToStackEntry());
					break;
				case Opcode.OP_OBJ_SUBSTR:
					var subStrLen   = ToScriptInt(GetEntryValue<double>(PopOrZero()));
					var subStrStart = ToScriptInt(GetEntryValue<double>(PopOrZero()));
					var subStr      = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push(GetScriptSubstring(subStr, subStrStart, subStrLen).ToStackEntry());
					break;
				case Opcode.OP_OBJ_STARTS:
					var startsWith = GetEntryValue<TString>(PopOrEmptyString()) ?? "";
					var obj        = GetEntryValue<TString>(PopOrEmptyString()) ?? "";
					stack.Push(obj.StartsWith(startsWith, StringComparison.CurrentCultureIgnoreCase).ToStackEntry());
					break;
				case Opcode.OP_OBJ_ENDS:
					var endsNeedle = GetEntryValue<TString>(PopOrEmptyString()) ?? "";
					var endsValue  = GetEntryValue<TString>(PopOrEmptyString()) ?? "";
					stack.Push(endsValue.ToString().EndsWith(endsNeedle.ToString(), StringComparison.CurrentCultureIgnoreCase).ToStackEntry());
					break;
				case Opcode.OP_OBJ_TOKENIZE:
					var tokenizer = GetEntryValue<TString>(stack.Pop())?.ToString() ?? string.Empty;
					var tokenized = GetEntryValue<TString>(stack.Pop())?.ToString() ?? string.Empty;
					stack.Push(tokenized.TokenizeForScript(tokenizer).Cast<object?>().ToStackEntry());
					break;
				case Opcode.OP_TRANSLATE:
					var originalText = GetEntryValue<TString>(stack.Pop())?.ToString() ?? string.Empty;
					stack.Push((_script.ScriptManager.TranslationProvider?.Translate(originalText) ?? originalText).ToStackEntry());
					break;
				case Opcode.OP_OBJ_POSITIONS:
					var positionsNeedle = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					var positionsValue  = GetEntryValue<TString>(PopOrEmptyString())?.ToString() ?? string.Empty;
					stack.Push(positionsValue.PositionsOf(positionsNeedle).Cast<object?>().ToStackEntry());
					break;
				case Opcode.OP_DYNAMIC_ADD:
					var dynamicAddRight = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var dynamicAddLeft  = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					if (dynamicAddLeft.Type == StackEntryType.String || dynamicAddRight.Type == StackEntryType.String)
					{
						stack.Push((Tools.ToScriptString(dynamicAddLeft.GetValue()) + Tools.ToScriptString(dynamicAddRight.GetValue())).ToStackEntry());
					}
					else
					{
						stack.Push((dynamicAddLeft.GetValue<double>() + dynamicAddRight.GetValue<double>()).ToStackEntry());
					}

					break;
				case Opcode.OP_OBJ_SIZE:
					stack.Push(GetScriptArraySize(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue()).ToStackEntry());
					break;
				case Opcode.OP_ARRAY:
					var arrayIndex = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var array      = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					stack.Push(GetScriptArrayCellEntry(array, arrayIndex));
					break;
				case Opcode.OP_ARRAY_ASSIGN:
					try
					{
						var arrAssVal   = UnwrapScriptValue(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
						var arrAssIndex = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
						var arrAssObj   = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
						SetScriptArrayCell(arrAssObj, arrAssIndex, arrAssVal);
					}
					catch (Exception e)
					{
						Tools.DebugLine(e.Message);
					}

					break;
				case Opcode.OP_ARRAY_MULTIDIM:
					var multiArrayY = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var multiArrayX = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var multiArray  = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					stack.Push(GetScriptArrayCellEntry(GetScriptArrayCell(multiArray, multiArrayX), multiArrayY));
					break;
				case Opcode.OP_ARRAY_MULTIDIM_ASSIGN:
					var multiArrayValue        = UnwrapScriptValue(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue());
					var multiArrayAssignY      = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var multiArrayAssignX      = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var multiArrayAssignTarget = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					SetScriptArrayCell2(multiArrayAssignTarget, multiArrayAssignX, multiArrayAssignY, multiArrayValue);
					break;
				case Opcode.OP_OBJ_SUBARRAY:
					var subArrayLength = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var subArrayStart  = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var subArrayTarget = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					stack.Push(GetScriptSubArray(subArrayTarget, subArrayStart, subArrayLength).ToStackEntry());
					break;
				case Opcode.OP_OBJ_ADDSTRING:
					var addValue = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					var addArray = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					GetMutableScriptArray(addArray)?.Add(addValue);
					break;
				case Opcode.OP_OBJ_DELETESTRING:
					var deleteIndex = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var deleteArray = GetMutableScriptArray(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true));
					if (deleteArray != null && deleteIndex >= 0 && deleteIndex < deleteArray.Count)
						deleteArray.RemoveAt(deleteIndex);
					break;
				case Opcode.OP_OBJ_REMOVESTRING:
					var removeValue = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					var removeArray = GetMutableScriptArray(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true));
					var removeIndex = IndexOfScriptValue(removeArray, removeValue);
					if (removeArray != null && removeIndex >= 0)
						removeArray.RemoveAt(removeIndex);
					break;
				case Opcode.OP_OBJ_REPLACESTRING:
					var replaceIndex = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var replaceValue = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					var replaceArray = GetMutableScriptArray(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true));
					if (replaceArray != null)
					{
						if (replaceIndex >= 0 && replaceIndex < replaceArray.Count)
							replaceArray[replaceIndex] = replaceValue;
						else
							replaceArray.Add(replaceValue);
					}

					break;
				case Opcode.OP_OBJ_INSERTSTRING:
					var insertIndex = ToScriptInt(GetEntry(stack.Pop()).GetValue<double>());
					var insertValue = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true).GetValue();
					var insertArray = GetMutableScriptArray(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true));
					if (insertArray != null)
					{
						if (insertIndex < 0 || insertIndex >= insertArray.Count)
							insertArray.Add(insertValue);
						else
							insertArray.Insert(insertIndex, insertValue);
					}

					break;
				case Opcode.OP_OBJ_CLEAR:
					GetMutableScriptArray(GetEntry(stack.Pop(), returnStackEntryIfNotFound: true))?.Clear();
					break;
				case Opcode.OP_ARRAY_NEW_MULTIDIM:
					var multiDimSize = ClampScriptArraySize(ToScriptInt(GetEntry(stack.Pop()).GetValue<double>()));
					ExpandScriptArray(GetEntry(stack.Peek(), returnStackEntryIfNotFound: true), multiDimSize);
					break;
				case Opcode.OP_WITH:
					var withTarget = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var withValue  = UnwrapScriptValue(withTarget.GetValue());
					if (withValue is null || withTarget.Type == Number && ToScriptDouble(withValue) == 0.0d || !IsObjectEntry(withTarget))
					{
						index     = (int)op.Value;
						_indexPos = index;
						break;
					}

					opWith.Push(withTarget);
					break;
				case Opcode.OP_WITHEND:
					if (opWith.Count > 0)
						opWith.Pop(); //stack.Push();
					break;
				case Opcode.OP_FOREACH:
					if (stack.Count < 3)
					{
						index     = (int)op.Value;
						_indexPos = index;
						break;
					}

					var arrForeachIndexEntry = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var arrForeachIndex      = ToScriptInt(arrForeachIndexEntry.GetValue<double>());
					var arrForeachObjEntry   = GetEntry(stack.Pop(), returnStackEntryIfNotFound: true);
					var arrForeachTarget     = stack.Pop();
					var arrForeachObj        = GetMutableScriptArray(arrForeachObjEntry);

					if (arrForeachObj == null || arrForeachIndex < 0 || arrForeachIndex >= arrForeachObj.Count)
					{
						index     = (int)op.Value;
						_indexPos = index;
						break;
					}

					var arrForeachTargetEntry = arrForeachTarget.Type == Variable && arrForeachTarget.GetParent() is VariableCollection arrForeachParent
						?
						arrForeachParent.GetVariable(NormalizeScriptVariableName(arrForeachTarget.GetValue()?.ToString() ?? string.Empty))
						: arrForeachTarget.Type == Variable
							? GetOrCreateScriptVariable(arrForeachTarget.GetValue()?.ToString() ?? string.Empty)
							: arrForeachTarget;
					arrForeachTargetEntry.SetValue(new LinkedStackEntry(new ListCellStackEntry(arrForeachObj, arrForeachIndex)));
					_useTemp = false;

					stack.Push(arrForeachTargetEntry);
					stack.Push(arrForeachObjEntry);
					stack.Push(arrForeachIndexEntry);
					break;
				case Opcode.OP_THIS:
					stack.Push(opWith.Count > 0 ? opWith.Peek() : ThisObject.ToStackEntry());
					break;
				case Opcode.OP_THISO:
					stack.Push((RefObject ?? _script).ToStackEntry());
					break;
				case Opcode.OP_PLAYER:
					stack.Push(ScriptExecutionContext?.Player?.ToStackEntry() ?? GetNamedGlobalValue("player"));
					break;
				case Opcode.OP_PLAYERO:
					stack.Push((ScriptExecutionContext?.PlayerObject ?? ScriptExecutionContext?.Player)?.ToStackEntry() ?? GetNamedGlobalValue("playero", "player"));
					break;
				case Opcode.OP_LEVEL:
					stack.Push(ScriptExecutionContext?.Level?.ToStackEntry() ?? GetNamedGlobalValue("level"));
					break;
				case Opcode.OP_TEMP:
					stack.Push(new StackEntry(StackEntryType.Array, _tempVariables));
					_useTemp = true;
					break;
				case Opcode.OP_PARAMS:
					stack.Push(GetCallParameters());
					break;
				case Opcode.OP_NUM_OPS:
					break;
			}
		}

		return 0.ToStackEntry();
	}

	private void SetCallParameters(Stack<IStackEntry>? callStack)
	{
		if (callStack is not { Count: > 0 }) return;

		var parameters = new List<object?>(callStack.Count);
		foreach (var entry in callStack)
			parameters.Add(UnwrapScriptValue(entry.GetValue()));

		_localVariables.AddOrUpdate("params", new StackEntry(StackEntryType.Array, parameters));
	}

	private IStackEntry GetCallParameters()
	{
		if (_localVariables.TryGetVariable("params", out var parameters))
			return parameters!;

		return _localVariables.AddOrUpdate("params", new StackEntry(StackEntryType.Array, new List<object?>()));
	}

	private ScriptVariable? MakeOldScriptVariable(string name, bool allowGlobalLookup = false)
	{
		var segments = name.Split('.', StringSplitOptions.TrimEntries);
		if (segments.Length == 0 || segments.Any(string.IsNullOrEmpty))
			return null;

		VariableCollection? current = null;
		foreach (var segmentValue in segments)
		{
			var segment = NormalizeScriptVariableName(segmentValue);
			if (current != null)
			{
				var childEntry = current.GetVariable(segment);
				if (UnwrapScriptValue(childEntry.GetValue()) is not VariableCollection child)
				{
					child = new ScriptVariable(segment);
					childEntry.SetValue(child);
				}

				current = child;
				continue;
			}

			current = segment switch
			{
				"this"                                                                                      => ThisObject,
				"thiso"                                                                                     => RefObject ?? _script,
				"temp"                                                                                      => _tempVariables,
				"player"                                                                                    => UnwrapScriptValue(GetNamedGlobalValue("player").GetValue()) as VariableCollection,
				"playero"                                                                                   => UnwrapScriptValue(GetNamedGlobalValue("playero", "player").GetValue()) as VariableCollection,
				"client" or "clientr" or "serverr"                                                          => UnwrapScriptValue(GetNamedGlobalValue(segment).GetValue()) as VariableCollection,
				_ when allowGlobalLookup && _script.ScriptManager.GlobalVariables.ContainsVariable(segment) => UnwrapScriptValue(_script.ScriptManager.GlobalVariables[segment].GetValue()) as VariableCollection,
				_                                                                                           => null,
			};
		}

		return current as ScriptVariable;
	}

	private object? CreateScriptObject(string className, string objectName)
	{
		if (string.IsNullOrEmpty(className)) return null;
		var owner = ThisObject as Script ?? ThisObject.OwnerScript ?? _script;
		if (_script.ScriptManager.TryCreateObject(className, objectName, owner, out var createdObject))
			return createdObject;

		LogDiagnostic($"Script: Object creator {className} not found in function {GetDiagnosticFunctionName()} in script of {GetScriptDescription()}");
		return new MissingObjectCreatorResult();
	}

	private static string GetRawStackString(IStackEntry stackEntry)
	{
		var value = stackEntry.GetValue();
		return value switch
		{
			null                               => string.Empty,
			TString text                       => text.ToString(),
			string text                        => text,
			_ when stackEntry.Type == Variable => value.ToString() ?? string.Empty,
			_                                  => string.Empty,
		};
	}

	private static bool IsImplicitObjectName(string name) => name.Equals("unknown_object", StringComparison.OrdinalIgnoreCase);

	private static bool TryGetPublicScriptFunction(Script? script, string functionName, out ScriptCommand command) => TryGetScriptFunction(script, functionName, out command, requirePublic: true);

	private bool TryGetScriptMemberFunction(Script? script, string functionName, out ScriptCommand command) => TryGetScriptFunction(script, functionName, out command, requirePublic: !ReferenceEquals(script, ThisObject), receiver: script);

	private static bool TryGetScriptFunction(Script? script, string functionName, out ScriptCommand command, bool requirePublic, ScriptVariable? receiver = null)
	{
		var normalizedFunctionName = functionName.ToLowerInvariant();
		if (script != null && script.Functions.TryGetValue(normalizedFunctionName, out var function) && (!requirePublic || function.IsPublic))
		{
			command = new BoundScriptFunction(script, normalizedFunctionName, receiver).Invoke;
			return true;
		}

		command = null!;
		return false;
	}

	private static bool HasScriptFunction(Script? script, string functionName, bool requirePublic) => script != null && script.Functions.TryGetValue(functionName.ToLowerInvariant(), out var function) && (!requirePublic || function.IsPublic);

	private bool HasJoinedClassFunction(ScriptVariable scriptVariable, string functionName, bool requirePublic, HashSet<string> visitedClassNames)
	{
		foreach (var className in scriptVariable.JoinedClassNames)
		{
			if (!visitedClassNames.Add(className) || !_script.ScriptManager.GlobalVariables.ContainsVariable(className)) continue;

			var classScript = UnwrapScriptValue(_script.ScriptManager.GlobalVariables[className].GetValue()) as Script;
			if (HasScriptFunction(classScript, functionName, requirePublic))
				return true;

			if (classScript != null && HasJoinedClassFunction(classScript, functionName, requirePublic, visitedClassNames))
				return true;
		}

		return false;
	}

	private bool TryGetJoinedClassFunction(ScriptVariable? scriptVariable, string functionName, out ScriptCommand command) => TryGetJoinedClassFunction(scriptVariable, scriptVariable, functionName, out command, []);

	private bool TryGetJoinedClassFunction(ScriptVariable? scriptVariable, ScriptVariable? receiver, string functionName, out ScriptCommand command, HashSet<string> visitedClassNames)
	{
		if (scriptVariable != null)
		{
			foreach (var className in scriptVariable.JoinedClassNames)
			{
				if (!visitedClassNames.Add(className)) continue;
				if (!_script.ScriptManager.GlobalVariables.ContainsVariable(className)) continue;

				var classScript = UnwrapScriptValue(_script.ScriptManager.GlobalVariables[className].GetValue()) as Script;
				if (TryGetScriptFunction(classScript, functionName, out command, requirePublic: false, receiver: receiver))
					return true;

				if (TryGetJoinedClassFunction(classScript, receiver, functionName, out command, visitedClassNames))
					return true;
			}
		}

		command = null!;
		return false;
	}

	private static IStackEntry CopyStackEntry(IStackEntry entry) => entry is LinkedStackEntry or ListCellStackEntry ? entry : new StackEntry(entry.Type, entry.GetValue(), entry.GetParent());

	private static bool IsAssignmentTargetEntry(IStackEntry entry) => entry is LinkedStackEntry or ListCellStackEntry || entry.Type is Variable or ScriptProperty;

	private object? ResolveScriptPropertyInstance(IScriptProperty property, object? candidate)
	{
		if (property.MainType == typeof(Script))
			return _script;

		if (candidate != null && property.MainType.IsInstanceOfType(candidate))
			return candidate;

		if (RefObject != null && property.MainType.IsInstanceOfType(RefObject))
			return RefObject;

		if (property.MainType.IsInstanceOfType(_script))
			return _script;

		return null;
	}

	private IStackEntry ConvertToFloatEntry(IStackEntry entry, Stack<IStackEntry>? opWith)
	{
		var resolvedEntry = ResolveEntryForRead(entry, opWith, returnStackEntryIfNotFound: true);
		var value         = resolvedEntry.GetValue();
		if (value is IScriptProperty { HasReadMethod: true } property)
		{
			var inst = ResolveScriptPropertyInstance(property, resolvedEntry.GetParent());
			value = property.Read(inst!);
		}

		return ToScriptDouble(value).ToStackEntry();
	}

	private IStackEntry ConvertToStringEntry(IStackEntry entry, Stack<IStackEntry>? opWith)
	{
		var resolvedEntry = ResolveEntryForRead(entry, opWith, returnStackEntryIfNotFound: true);
		var value         = resolvedEntry.GetValue();
		if (value is IList list && list.GetType().IsGenericType)
		{
			value = string.Join(",", list.Cast<object?>().Select(item => item is bool boolItem ? Convert.ToInt32(boolItem, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : Tools.ToScriptString(item)));
		}

		if (value is bool boolValue)
			value = Convert.ToInt32(boolValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

		if (value is IScriptProperty { HasReadMethod: true } property)
		{
			var inst = ResolveScriptPropertyInstance(property, resolvedEntry.GetParent());
			value = property.Read(inst!);
		}

		return value is TString bytes ? bytes.ToStackEntry() : Tools.ToScriptString(value).ToStackEntry();
	}

	private IStackEntry ConvertToObjectEntry(IStackEntry entry, Stack<IStackEntry>? opWith)
	{
		if (entry.Type == Variable)
		{
			var unresolvedVariableName = NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty);
			if (unresolvedVariableName == "this")
				return ThisObject.ToStackEntry();
			if (unresolvedVariableName == "thiso")
				return (RefObject ?? _script).ToStackEntry();
		}

		var resolvedEntry = GetEntry(entry, returnStackEntryIfNotFound: true);
		if (entry.Type == Variable && entry.GetParent() is VariableCollection && resolvedEntry.Type == StackEntryType.String && Tools.ToScriptString(resolvedEntry.GetValue()).Length == 0)
			return entry;

		if (resolvedEntry.Type is not (StackEntryType.String or Variable))
			return resolvedEntry;

		var variableName = GetEntryValue<TString>(resolvedEntry, returnStackEntryIfNotFound: true)?.ToString().ToLowerInvariant() ?? string.Empty;
		if (opWith is { Count: > 0 } && TryGetWithMemberEntry(opWith.Peek(), variableName, out var withMemberEntry))
			return withMemberEntry;

		if (variableName is "temp")
			return _tempVariables.ToStackEntry();

		if (TryGetObjectEntryByName(variableName, out var objectEntry))
			return objectEntry;

		return resolvedEntry;
	}

	private IStackEntry GetOrCreateScopedObjectVariable(string name)
	{
		var normalizedName = NormalizeScriptVariableName(name);
		if (string.IsNullOrEmpty(normalizedName))
			return 0.ToStackEntry();

		if (normalizedName == "this")
			return ThisObject.ToStackEntry();
		if (normalizedName == "thiso")
			return (RefObject ?? _script).ToStackEntry();

		var collection = _script.ScriptManager.GlobalVariables;
		if (collection.TryGetVariable(normalizedName, out var existingEntry) && UnwrapScriptValue(existingEntry?.GetValue()) is VariableCollection)
		{
			return existingEntry!;
		}

		var created = new ScriptVariable(normalizedName);
		return collection.AddOrUpdate(normalizedName, created.ToStackEntry());
	}

	private bool TryGetObjectEntryByName(string name, out IStackEntry objectEntry)
	{
		objectEntry = 0.ToStackEntry();
		var normalizedName = NormalizeScriptVariableName(name);
		if (string.IsNullOrEmpty(normalizedName))
			return false;

		if (_script.ScriptManager.GlobalVariables.TryGetVariable(normalizedName, out var globalEntry) && UnwrapScriptValue(globalEntry?.GetValue()) is ScriptVariable)
		{
			objectEntry = globalEntry!;
			return true;
		}

		if (_tempVariables.TryGetVariable(normalizedName, out var tempEntry) && UnwrapScriptValue(tempEntry?.GetValue()) is ScriptVariable)
		{
			objectEntry = tempEntry!;
			return true;
		}

		if (_localVariables.TryGetVariable(normalizedName, out var localEntry) && UnwrapScriptValue(localEntry?.GetValue()) is ScriptVariable)
		{
			objectEntry = localEntry!;
			return true;
		}

		return false;
	}

	private IStackEntry GetMemberValue(IStackEntry parentStackEntry, string memberName, bool createMissingParent = false)
	{
		var normalizedMemberName = NormalizeScriptVariableName(memberName);
		var parentName           = GetRawStackString(parentStackEntry);
		try
		{
			var parentEntry = GetEntry(parentStackEntry, returnStackEntryIfNotFound: true);
			var parentValue = UnwrapScriptValue(parentEntry.GetValue());
			if (parentValue is MissingObjectCreatorResult)
				return 0.ToStackEntry();
			if (createMissingParent && parentEntry.Type == Variable && parentEntry.GetParent() is null)
			{
				parentEntry = GetOrCreateScopedObjectVariable(Tools.ToScriptString(parentValue));
				parentValue = UnwrapScriptValue(parentEntry.GetValue());
			}
			else if (createMissingParent &&
			         parentStackEntry.Type == Variable &&
			         parentStackEntry.GetParent() is VariableCollection &&
			         parentEntry.Type == StackEntryType.String &&
			         Tools.ToScriptString(parentValue).Length == 0 &&
			         !TryGetRegisteredInstanceProperty(parentValue, normalizedMemberName, out _))
			{
				parentValue = new ScriptVariable(NormalizeScriptVariableName(GetRawStackString(parentStackEntry)));
				parentEntry.SetValue(parentValue);
			}

			if (parentValue is Script script)
			{
				if (script.Properties.TryGetProperty(normalizedMemberName, out var property))
					return property.ToStackEntry(parent: script);

				return CreateMemberVariableEntry(script, normalizedMemberName);
			}

			if (parentValue is VariableCollection variableCollection)
			{
				if (parentValue is ScriptVariable scriptVariable)
				{
					if (scriptVariable.Properties.TryGetProperty(normalizedMemberName, out var prop))
						return prop.ToStackEntry(parent: scriptVariable);
				}

				return CreateMemberVariableEntry(variableCollection, normalizedMemberName);
			}

			if (TryGetRegisteredInstanceProperty(parentValue, normalizedMemberName, out var registeredProperty))
				return registeredProperty.ToStackEntry(parent: parentValue);

			if (parentValue != null)
				return new StackEntry(Variable, normalizedMemberName, new MissingMemberReference(parentName, parentValue));
		}
		catch (Exception e)
		{
			Tools.DebugLine(e.Message);
		}

		return 0.ToStackEntry();
	}

	private static bool TryGetRegisteredInstanceProperty(object? instance, string memberName, out IScriptProperty property)
	{
		property = null!;
		if (instance == null || string.IsNullOrWhiteSpace(memberName))
			return false;

		var instanceType = instance.GetType();
		foreach (var properties in ScriptManager.GlobalProperties.Values)
		{
			if (properties.TryGetProperty(memberName, out property) && property.MainType.IsAssignableFrom(instanceType))
				return true;
		}

		return false;
	}

	private bool TryCallBuiltInFunction(string? functionName, IReadOnlyList<IStackEntry> args, IReadOnlyList<IStackEntry> rawArgs, out IStackEntry result)
	{
		result = 0.ToStackEntry();

		switch (NormalizeScriptVariableName(functionName ?? string.Empty))
		{
			case "isobject":
				result = IsObject(args.Count > 0 ? args[0] : null, rawArgs.Count > 0 ? rawArgs[0] : null).ToStackEntry();
				return true;
			default:
				return false;
		}
	}

	private bool TryCallReceiverPropertyFunction(string? functionName, IReadOnlyList<IStackEntry> args, out IStackEntry result)
	{
		result = 0.ToStackEntry();
		if (args.Count == 0)
			return false;

		var normalizedFunctionName = NormalizeScriptVariableName(functionName ?? string.Empty);
		if (string.IsNullOrEmpty(normalizedFunctionName))
			return false;

		var receiver = UnwrapScriptValue(args[0].GetValue());
		if (!TryGetRegisteredInstanceProperty(receiver, normalizedFunctionName, out var property) || !property.IsFunction)
		{
			return false;
		}

		result = (property.Call(this, receiver!, args.Skip(1).ToArray()) ?? 0).ToStackEntry();
		return true;
	}

	private bool IsObject(IStackEntry? resolvedEntry, IStackEntry? rawEntry)
	{
		return IsObjectEntry(resolvedEntry) || IsObjectReference(rawEntry);
	}

	private bool IsObjectEntry(IStackEntry? entry)
	{
		var value = UnwrapScriptValue(entry?.GetValue());
		if (value is ScriptVariable)
			return true;

		return TryGetObjectByName(Tools.ToScriptString(value));
	}

	private bool IsObjectReference(IStackEntry? entry)
	{
		if (entry == null)
			return false;

		var name = Tools.ToScriptString(entry.GetValue());
		if (string.IsNullOrWhiteSpace(name))
			return false;

		if (entry.Type == Variable && entry.GetParent() is VariableCollection parent && TryGetObjectFromCollection(parent, name))
		{
			return true;
		}

		return TryGetObjectByName(name);
	}

	private bool TryGetObjectByName(string? name)
	{
		var normalizedName = NormalizeScriptVariableName(name ?? string.Empty);
		return !string.IsNullOrEmpty(normalizedName) &&
		       (_script.ScriptManager.GlobalVariables.TryGetVariable(normalizedName, out var globalEntry) && UnwrapScriptValue(globalEntry?.GetValue()) is ScriptVariable ||
		        _tempVariables.TryGetVariable(normalizedName, out var tempEntry) && UnwrapScriptValue(tempEntry?.GetValue()) is ScriptVariable);
	}

	private static bool TryGetGlobalScriptProperty(string variableName, out IScriptProperty property)
	{
		foreach (var propertySetName in new[] { nameof(ScriptUniverse), nameof(Script) })
		{
			if (!ScriptManager.GlobalProperties.TryGetValue(propertySetName, out var properties))
				continue;

			if (properties.TryGetProperty(variableName, out property))
				return true;
		}

		property = null!;
		return false;
	}

	private static bool TryGetObjectFromCollection(VariableCollection collection, string name)
	{
		var normalizedName = NormalizeScriptVariableName(name);
		return !string.IsNullOrEmpty(normalizedName) && collection.TryGetVariable(normalizedName, out var entry) && UnwrapScriptValue(entry?.GetValue()) is ScriptVariable;
	}

	private IStackEntry ResolveEntryForRead(IStackEntry entry, Stack<IStackEntry>? opWith, bool returnStackEntryIfNotFound = false)
	{
		if (entry.Type == Variable && entry.GetParent() is VariableCollection)
			return GetEntry(entry, returnStackEntryIfNotFound: returnStackEntryIfNotFound);

		if (entry.Type == Variable && opWith is { Count: > 0 } && TryGetWithMemberEntry(opWith.Peek(), entry.GetValue()?.ToString() ?? string.Empty, out var withMemberEntry))
		{
			if (withMemberEntry.GetValue() is IScriptProperty { HasReadMethod: true } property)
			{
				var propertyValue = property.Read(ResolveScriptPropertyInstance(property, withMemberEntry.GetParent())!);
				var variableName  = NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty);
				if (propertyValue == null && _script.ScriptManager.GlobalVariables.TryGetVariable(variableName, out var globalEntry) && globalEntry != null && UnwrapScriptValue(globalEntry?.GetValue()) is ScriptVariable)
				{
					return globalEntry!;
				}
			}

			return withMemberEntry;
		}

		if (entry.Type == Variable &&
		    entry.GetParent() is null &&
		    _tempAliases.Contains(NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty)) &&
		    _tempVariables.ContainsVariable(NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty)))
			return GetEntry(entry, returnStackEntryIfNotFound: returnStackEntryIfNotFound);

		return GetEntry(entry, returnStackEntryIfNotFound: returnStackEntryIfNotFound);
	}

	private bool TryGetWithFunctionEntry(IStackEntry parentStackEntry, string functionName, out IStackEntry functionEntry)
	{
		functionEntry = 0.ToStackEntry();
		var normalizedFunctionName = NormalizeScriptVariableName(functionName);
		if (string.IsNullOrEmpty(normalizedFunctionName)) return false;

		if (TryGetWithMemberEntry(parentStackEntry, normalizedFunctionName, out var memberEntry) && (memberEntry.Type == Function || memberEntry.GetValue() is ScriptCommand or IScriptProperty { IsFunction: true }))
		{
			functionEntry = memberEntry;
			return true;
		}

		return false;
	}

	private void LogMissingFunction(string functionName) => LogDiagnostic($"Script: Function {functionName} not found in function {GetDiagnosticFunctionName()} in script of {GetScriptDescription()}");

	private void LogMissingProperty(string parentName, object parent, string propertyName)
	{
		var objectName = !string.IsNullOrWhiteSpace(parentName) ? parentName : parent is ScriptVariable scriptVariable && !string.IsNullOrWhiteSpace(scriptVariable.Name) ? scriptVariable.Name : parent.GetType().Name;
		LogDiagnostic($"Script: Property {objectName}.{propertyName} not found in function {GetDiagnosticFunctionName()} in script of {GetScriptDescription()}");
	}

	private void LogDiagnostic(string message)
	{
		lock (_reportedDiagnosticsSync)
			if (!_reportedDiagnostics.Add(message))
				return;

		Tools.LogLine(message);
	}

	private string GetDiagnosticFunctionName() => string.IsNullOrWhiteSpace(CurrentFunctionName) ? "<script>" : CurrentFunctionName;

	private string GetScriptDescription() => $"{_script.Type} {_script.Name}";

	private bool TryGetWithMemberEntry(IStackEntry parentStackEntry, string memberName, out IStackEntry memberEntry)
	{
		memberEntry = 0.ToStackEntry();
		var normalizedMemberName = NormalizeScriptVariableName(memberName);
		if (string.IsNullOrEmpty(normalizedMemberName)) return false;

		var parentEntry = GetEntry(parentStackEntry, returnStackEntryIfNotFound: true);
		var parentValue = UnwrapScriptValue(parentEntry.GetValue());

		if (parentValue is Script script)
		{
			if (TryGetScriptMemberFunction(script, normalizedMemberName, out var memberCommand))
			{
				memberEntry = memberCommand.ToStackEntry();
				return true;
			}

			if (script.Properties.TryGetProperty(normalizedMemberName, out var property))
			{
				memberEntry = property.ToStackEntry(parent: script);
				return true;
			}

			if (TryGetJoinedClassFunction(script, normalizedMemberName, out var joinedCommand))
			{
				memberEntry = joinedCommand.ToStackEntry();
				return true;
			}

			if (script.ContainsVariable(normalizedMemberName))
			{
				memberEntry = script.GetVariable(normalizedMemberName);
				return true;
			}
		}

		if (parentValue is ScriptVariable scriptVariable)
		{
			if (scriptVariable.Properties.TryGetProperty(normalizedMemberName, out var property))
			{
				memberEntry = property.ToStackEntry(parent: scriptVariable);
				return true;
			}

			if (TryGetJoinedClassFunction(scriptVariable, normalizedMemberName, out var joinedCommand))
			{
				memberEntry = joinedCommand.ToStackEntry();
				return true;
			}
		}

		if (parentValue is VariableCollection variableCollection && variableCollection.ContainsVariable(normalizedMemberName))
		{
			memberEntry = variableCollection.GetVariable(normalizedMemberName);
			return true;
		}

		return false;
	}

	private IStackEntry IncrementEntry(IStackEntry entry, double delta, Stack<IStackEntry>? opWith)
	{
		IStackEntry target;
		if (entry.Type == Variable && entry.GetParent() is not VariableCollection && opWith is { Count: > 0 } && TryGetWithMemberEntry(opWith.Peek(), entry.GetValue()?.ToString() ?? string.Empty, out var withMemberEntry))
		{
			target = withMemberEntry;
		}
		else if (entry.Type == Variable && entry.GetParent() is null && _localVariables.ContainsVariable(NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty)))
		{
			target = GetEntry(entry, returnStackEntryIfNotFound: true);
		}
		else if (entry.Type == Variable &&
		         entry.GetParent() is null &&
		         _tempAliases.Contains(NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty)) &&
		         _tempVariables.ContainsVariable(NormalizeScriptVariableName(entry.GetValue()?.ToString() ?? string.Empty)))
		{
			target = GetEntry(entry, returnStackEntryIfNotFound: true);
		}
		else
		{
			target = entry.Type == Variable && entry.GetParent() is not VariableCollection ? GetEntry(entry, returnStackEntryIfNotFound: true) : GetEntry(entry, returnStackEntryIfNotFound: true);
		}

		if (target.Type == Variable && target.GetParent() is null)
			target = GetOrCreateScriptVariable(target.GetValue()?.ToString() ?? string.Empty);

		target.SetValue(ToScriptDouble(target.GetValue()) + delta);
		return target;
	}

	private void AssignValue(IStackEntry variable, IStackEntry val, Stack<IStackEntry>? opWith)
	{
		var variableName           = (variable.GetValue() ?? "").ToString() ?? string.Empty;
		var normalizedVariableName = NormalizeScriptVariableName(variableName);
		var assignEntry            = ResolveEntryForRead(val, opWith);
		var assignValue            = GetAssignmentValue(assignEntry);
		var assignStorageEntry     = GetAssignmentStorageEntry(assignEntry, assignValue);
		if (variable.Type == Variable && variable.GetParent() is MissingMemberReference missingMember)
		{
			LogMissingProperty(missingMember.ObjectName, missingMember.Instance, variableName);
			return;
		}

		if (variable.Type == Variable && variable.GetParent() == null && TryGetGlobalScriptProperty(normalizedVariableName, out var globalProperty) && globalProperty.HasWriteMethod)
		{
			var inst = ResolveScriptPropertyInstance(globalProperty, variable.GetParent());
			globalProperty.Write(inst!, assignValue);
			return;
		}

		if (variable.Type == Variable && variable.GetParent() is VariableCollection parentCollection)
		{
			if (ReferenceEquals(parentCollection, _tempVariables))
				_tempAliases.Add(normalizedVariableName);
			parentCollection.AddOrUpdate(normalizedVariableName, assignStorageEntry);
			_useTemp = false;
			return;
		}

		if (variable.Type == ScriptProperty)
		{
			var scriptProp = variable.GetValue<IScriptProperty>();
			var inst       = scriptProp != null ? ResolveScriptPropertyInstance(scriptProp, variable.GetParent()) : null;
			scriptProp?.Write(inst!, assignValue);
			return;
		}

		if (opWith is { Count: not 0 } && TryGetWithMemberEntry(opWith.Peek(), variableName, out var withMemberEntry))
		{
			AssignValue(withMemberEntry, assignStorageEntry, null);
			return;
		}

		if (variable.Type == Variable && variable.GetParent() == null && _localVariables.ContainsVariable(normalizedVariableName))
		{
			_localVariables.AddOrUpdate(normalizedVariableName, assignStorageEntry);
			_useTemp = false;
			return;
		}

		if (variable.Type == Variable && variable.GetParent() == null && _tempAliases.Contains(normalizedVariableName) && _tempVariables.ContainsVariable(normalizedVariableName))
		{
			_tempVariables.AddOrUpdate(normalizedVariableName, assignStorageEntry);
			_useTemp = false;
			return;
		}

		if (variable.Type == Variable &&
		    variable.GetParent() == null &&
		    TryGetWithMemberEntry(ThisObject.ToStackEntry(), normalizedVariableName, out var receiverMemberEntry) &&
		    receiverMemberEntry.GetValue() is IScriptProperty { HasWriteMethod: true })
		{
			AssignValue(receiverMemberEntry, assignStorageEntry, null);
			return;
		}

		if (opWith is { Count: not 0 })
		{
			try
			{
				var              objTest = opWith.Peek().GetValue<ScriptVariable>();
				var              props   = objTest?.Properties;
				IScriptProperty? prop    = null;
				props?.TryGetProperty(variableName, out prop);

				if (prop != null)
				{
					prop.Write(objTest!, assignValue);
				}
				else
				{
					objTest?.AddOrUpdate(normalizedVariableName, assignStorageEntry);
					RegisterGlobalObjectAlias(normalizedVariableName, assignStorageEntry);
				}
			}
			catch (Exception e)
			{
				Tools.DebugLine(e.Message);
			}
		}
		else if (variable.Type != Variable && variable.Type != ScriptProperty)
		{
			variable.SetValue(assignEntry is LinkedStackEntry ? assignEntry : assignValue);
		}
		else if (!_useTemp && _script.ScriptManager.GlobalVariables.ContainsVariable(normalizedVariableName))
		{
			_script.ScriptManager.GlobalVariables.AddOrUpdate(normalizedVariableName, assignStorageEntry);
		}
		else if (!_useTemp && CurrentFunctionName.Contains('.'))
		{
			_script.ScriptManager.GlobalVariables.AddOrUpdate(normalizedVariableName, assignStorageEntry);
		}
		else
		{
			_useTemp = false;
			_script.ScriptManager.GlobalVariables.AddOrUpdate(normalizedVariableName, assignStorageEntry);
		}
	}

	private object? GetAssignmentValue(IStackEntry entry)
	{
		if (entry.GetValue() is IScriptProperty { HasReadMethod: true } property)
			return property.Read(ResolveScriptPropertyInstance(property, entry.GetParent())!);

		return entry is LinkedStackEntry ? entry : entry.GetValue();
	}

	private static IStackEntry GetAssignmentStorageEntry(IStackEntry entry, object? value) => entry is LinkedStackEntry ? entry : value.ToStackEntry();

	private void RegisterGlobalObjectAlias(string variableName, IStackEntry entry)
	{
		if (string.IsNullOrEmpty(variableName))
			return;

		if (UnwrapScriptValue(entry.GetValue()) is ScriptVariable)
			_script.ScriptManager.GlobalVariables.AddOrUpdate(variableName, entry);
	}

	private IStackEntry GetNamedGlobalValue(string name, string? fallbackName = null)
	{
		var normalizedName = name.ToLowerInvariant();
		if (_script.ScriptManager.GlobalVariables.ContainsVariable(normalizedName))
			return _script.ScriptManager.GlobalVariables[normalizedName];

		if (!string.IsNullOrEmpty(fallbackName))
		{
			var normalizedFallbackName = fallbackName.ToLowerInvariant();
			if (_script.ScriptManager.GlobalVariables.ContainsVariable(normalizedFallbackName))
				return _script.ScriptManager.GlobalVariables[normalizedFallbackName];
		}

		var propertyEntry = GetEntry(normalizedName.ToStackEntry(isVariable: true), returnStackEntryIfNotFound: true);
		if (propertyEntry.GetValue() is IScriptProperty { HasReadMethod: true } property)
			return (property.Read(ResolveScriptPropertyInstance(property, propertyEntry.GetParent())!) ?? 0).ToStackEntry();

		return 0.ToStackEntry();
	}

	public IStackEntry GetEntry(IStackEntry stackEntry, StackEntryType? overrideStackType = null, bool returnStackEntryIfNotFound = false, bool reportMissingProperty = true, bool resolveFunctionReferences = true)
	{
		StackEntryType? type          = overrideStackType ?? stackEntry.Type;
		var             retVal        = stackEntry;
		var             foundVariable = false;
		switch (type)
		{
			case Variable when NormalizeScriptVariableName(stackEntry.GetValue()?.ToString() ?? string.Empty) == "this":
				retVal        = ThisObject.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when NormalizeScriptVariableName(stackEntry.GetValue()?.ToString() ?? string.Empty) == "thiso":
				retVal        = (RefObject ?? _script).ToStackEntry();
				foundVariable = true;
				break;
			case Variable when stackEntry.GetParent() is VariableCollection parentCollection:
				var parentVariableName = NormalizeScriptVariableName(stackEntry.GetValue()?.ToString() ?? string.Empty);
				if (ReferenceEquals(parentCollection, _tempVariables))
					_tempAliases.Add(parentVariableName);
				retVal = !parentCollection.ContainsVariable(parentVariableName) && parentCollection is ScriptVariable memberReceiver && TryGetWithFunctionEntry(memberReceiver.ToStackEntry(), parentVariableName, out var memberFunction)
					? memberFunction
					: parentCollection.GetVariable(parentVariableName);
				_useTemp      = false;
				foundVariable = true;
				break;
			case Variable when stackEntry.GetParent() is MissingMemberReference missingMember:
				if (reportMissingProperty)
					LogMissingProperty(missingMember.ObjectName, missingMember.Instance, stackEntry.GetValue()?.ToString() ?? string.Empty);
				retVal        = 0.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when TryGetGlobalProperty(nameof(ScriptUniverse), stackEntry.GetValue()?.ToString(), out var property):
				retVal        = property.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when TryGetGlobalProperty(nameof(Script), stackEntry.GetValue()?.ToString(), out var property):
				retVal        = property.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when _activeEvent.Equals(stackEntry.GetValue()?.ToString(), StringComparison.OrdinalIgnoreCase):
				retVal        = 1.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when _localVariables.ContainsVariable(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty):
				_useTemp      = false;
				retVal        = _localVariables.GetVariable(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty);
				foundVariable = true;
				break;
			case Variable when _tempAliases.Contains(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty) && _tempVariables.ContainsVariable(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty):
				_useTemp      = false;
				retVal        = _tempVariables.GetVariable(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty);
				foundVariable = true;
				break;
			case Variable when TryGetWithMemberEntry(ThisObject.ToStackEntry(), stackEntry.GetValue()?.ToString() ?? string.Empty, out var receiverMemberEntry) && receiverMemberEntry.GetValue() is IScriptProperty { HasReadMethod: true }:
				retVal        = receiverMemberEntry;
				foundVariable = true;
				break;
			case Variable when _script.ScriptManager.GlobalVariables.ContainsVariable(stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty):
				retVal        = _script.ScriptManager.GlobalVariables[stackEntry.GetValue()?.ToString()?.ToLowerInvariant() ?? string.Empty];
				foundVariable = true;
				break;
			case Variable when resolveFunctionReferences && TryGetScriptFunction(_script, stackEntry.GetValue()?.ToString() ?? string.Empty, out var scriptFunction, requirePublic: false, receiver: ThisObject):
				retVal        = scriptFunction.ToStackEntry();
				foundVariable = true;
				break;
			case Variable when resolveFunctionReferences && TryGetJoinedClassFunction(ThisObject, stackEntry.GetValue()?.ToString() ?? string.Empty, out var joinedFunction):
				retVal        = joinedFunction.ToStackEntry();
				foundVariable = true;
				break;
			default:


				if (stackEntry.GetValue() is IScriptProperty { HasReadMethod: true } scriptProperty)
				{
					retVal        = scriptProperty.Read(ResolveScriptPropertyInstance(scriptProperty, stackEntry.GetParent())!).ToStackEntry();
					foundVariable = true;
				}


				break;
		}

		if (type is Variable && !foundVariable && !returnStackEntryIfNotFound)
		{
			Tools.DebugLine($"GetEntry, Type: {type}, FoundVariable: {foundVariable}, StackEntry: {stackEntry.GetValue()}, ReturnStackEntryIfNotFound: {returnStackEntryIfNotFound}");
			retVal = 0.ToStackEntry();
		}

		return retVal;
	}

	private static bool TryGetGlobalProperty(string ownerName, string? propertyName, out IScriptProperty property)
	{
		property = null!;
		if (string.IsNullOrWhiteSpace(propertyName) || !ScriptManager.GlobalProperties.TryGetValue(ownerName, out var properties))
		{
			return false;
		}

		return properties.TryGetProperty(propertyName, out property);
	}

	private static IStackEntry CreateMemberVariableEntry(VariableCollection parent, string memberName) => new StackEntry(Variable, NormalizeScriptVariableName(memberName), parent);

	private T? GetEntryValue<T>(IStackEntry stackEntry, StackEntryType? overrideStackType = null, bool returnStackEntryIfNotFound = false, bool reportMissingProperty = true, bool resolveFunctionReferences = true) =>
		GetEntry(stackEntry, overrideStackType, returnStackEntryIfNotFound, reportMissingProperty, resolveFunctionReferences).GetValue<T>();

	private IStackEntry ResolveReadableScriptProperty(IStackEntry entry)
	{
		if (entry.GetValue() is IScriptProperty { HasReadMethod: true } property)
			return (property.Read(ResolveScriptPropertyInstance(property, entry.GetParent())!) ?? 0).ToStackEntry();

		return entry;
	}

	private IStackEntry ResolveEntryForComparison(IStackEntry entry, Stack<IStackEntry>? opWith) => ResolveReadableScriptProperty(ResolveEntryForRead(entry, opWith));

	private IStackEntry GetOrCreateScriptVariable(string name)
	{
		var segments = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (segments.Length == 0)
			return 0.ToStackEntry();

		var                root = NormalizeScriptVariableName(segments[0]);
		VariableCollection current;
		var                index = 0;

		switch (root)
		{
			case "temp":
				current = _tempVariables;
				index   = 1;
				break;
			case "this":
				current = ThisObject;
				index   = 1;
				break;
			case "thiso":
				current = RefObject ?? _script;
				index   = 1;
				break;
			default:
				current = _script.ScriptManager.GlobalVariables;
				break;
		}

		if (index >= segments.Length)
			return current.ToStackEntry();

		for (; index < segments.Length - 1; index++)
		{
			var childName  = NormalizeScriptVariableName(segments[index]);
			var childEntry = current.GetVariable(childName);
			if (UnwrapScriptValue(childEntry.GetValue()) is not VariableCollection child)
			{
				child = new ScriptVariable(childName);
				childEntry.SetValue(child);
			}

			current = child;
		}

		return current.GetVariable(NormalizeScriptVariableName(segments[^1]));
	}

	private static string NormalizeScriptVariableName(string value) => value.ToLowerInvariant();

	private static object? UnwrapScriptValue(object? value)
	{
		while (value is IStackEntry entry)
			value = entry.GetValue();
		return value;
	}

	private static List<object?>? GetArrayValues(object? value, bool includeVariableCollections = true)
	{
		value = UnwrapScriptValue(value);
		return value switch
		{
			null                                                        => null,
			TString or string                                           => null,
			VariableCollection variable when includeVariableCollections => variable.GetDictionary().OrderBy(pair => GetScriptKeyOrder(pair.Key)).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => UnwrapScriptValue(pair.Value)).ToList(),
			IDictionary<string, IStackEntry> dictionary                 => dictionary.OrderBy(pair => GetScriptKeyOrder(pair.Key)).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => UnwrapScriptValue(pair.Value)).ToList(),
			IDictionary dictionary => dictionary.Cast<DictionaryEntry>()
			                                    .OrderBy(entry => GetScriptKeyOrder(entry.Key?.ToString() ?? string.Empty))
			                                    .ThenBy(entry => entry.Key?.ToString() ?? string.Empty, StringComparer.Ordinal)
			                                    .Select(entry => UnwrapScriptValue(entry.Value))
			                                    .ToList(),
			IEnumerable enumerable => enumerable.Cast<object?>().Select(UnwrapScriptValue).ToList(),
			_                      => null,
		};
	}

	private static int GetScriptKeyOrder(string key) => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : int.MaxValue;

	private static double ToScriptDouble(object? value)
	{
		value = UnwrapScriptValue(value);
		try
		{
			return value switch
			{
				null                     => 0.0d,
				bool b                   => b ? 1.0d : 0.0d,
				TString t                => double.TryParse(t.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0d,
				string s                 => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0.0d,
				IConvertible convertible => Convert.ToDouble(convertible, CultureInfo.InvariantCulture),
				_                        => 0.0d,
			};
		}
		catch (Exception e)
		{
			Tools.DebugLine(e.Message);
			return 0.0d;
		}
	}

	private static bool IsScriptTruthy(object? value) => ToScriptDouble(value) != 0.0d;

	private static bool ValueInRange(double value, double rangeStart, double rangeEnd, int mode)
	{
		const double negativeTolerance = -0.0001d;
		const double positiveTolerance = 0.0001d;

		return mode switch
		{
			0 => value - rangeStart > negativeTolerance && positiveTolerance > value - rangeEnd,
			1 => value - rangeStart > negativeTolerance && negativeTolerance > value - rangeEnd,
			2 => value - rangeStart > positiveTolerance && positiveTolerance > value - rangeEnd,
			3 => value - rangeStart > positiveTolerance && negativeTolerance > value - rangeEnd,
			_ => false,
		};
	}

	private static bool ValuesInRange(IEnumerable<object?> values, double rangeStart, double rangeEnd, int mode)
	{
		foreach (var value in values)
			if (!ValueInRange(ToScriptDouble(value), rangeStart, rangeEnd, mode))
				return false;

		return true;
	}

	private static bool ContainsAllScriptValues(IEnumerable values, IEnumerable<object?> needles)
	{
		foreach (var needle in needles)
			if (IndexOfScriptValue(values, needle) < 0)
				return false;

		return true;
	}

	private static int IndexOfScriptValue(IEnumerable? values, object? needle)
	{
		if (values == null)
			return -1;

		var index = 0;
		foreach (var value in values)
		{
			if (ScriptValuesEqual(value, needle))
				return index;
			index++;
		}

		return -1;
	}

	private static bool ScriptValuesEqual(object? left, object? right)
	{
		left  = UnwrapScriptValue(left);
		right = UnwrapScriptValue(right);

		if (ReferenceEquals(left, right))
			return true;
		if (left == null || right == null)
			return false;
		if (IsScriptNumeric(left) && IsScriptNumeric(right))
			return Math.Abs(ToScriptDouble(left) - ToScriptDouble(right)) < 0.0001d;
		if (left is TString or string || right is TString or string)
			return string.Equals(Tools.ToScriptString(left), Tools.ToScriptString(right), StringComparison.Ordinal);
		return left.Equals(right);
	}

	private static bool ScriptEntriesEqual(IStackEntry leftEntry, IStackEntry rightEntry)
	{
		if (leftEntry.Type == Null || rightEntry.Type == Null)
		{
			var other = leftEntry.Type == Null ? rightEntry : leftEntry;
			var value = UnwrapScriptValue(other.GetValue());
			return value == null || value is TString or string && string.IsNullOrEmpty(value.ToString()) || other.Type == Number && ToScriptDouble(value) == 0;
		}

		var leftArray  = GetArrayValues(leftEntry.GetValue(), includeVariableCollections: false);
		var rightArray = GetArrayValues(rightEntry.GetValue(), includeVariableCollections: false);
		if (leftArray != null && rightArray != null)
			return leftArray.Count == rightArray.Count && leftArray.Zip(rightArray).All(pair => ScriptValuesEqual(pair.First, pair.Second));

		if (IsComparableScriptScalar(leftEntry) && IsComparableScriptScalar(rightEntry))
			return CompareScriptValues(leftEntry, rightEntry) == 0;

		return ScriptValuesEqual(leftEntry.GetValue(), rightEntry.GetValue());
	}

	private static int CompareScriptValues(IStackEntry leftEntry, IStackEntry rightEntry)
	{
		var left  = UnwrapScriptValue(leftEntry.GetValue());
		var right = UnwrapScriptValue(rightEntry.GetValue());

		if (leftEntry.Type == StackEntryType.String && rightEntry.Type == StackEntryType.String)
			return Math.Sign(string.Compare(Tools.ToScriptString(left), Tools.ToScriptString(right), StringComparison.OrdinalIgnoreCase));

		if (leftEntry.Type == StackEntryType.String && rightEntry.Type == Number || leftEntry.Type == Number && rightEntry.Type == StackEntryType.String || leftEntry.Type == Number && rightEntry.Type == Number)
			return CompareScriptNumbers(ToScriptDouble(left), ToScriptDouble(right));

		return 0;
	}

	private static bool IsComparableScriptScalar(IStackEntry entry) => entry.Type is StackEntryType.String or Number;

	private static int CompareScriptNumbers(double left, double right)
	{
		const double tolerance = 0.0001d;
		if (right > left + tolerance)
			return -1;
		return left > right + tolerance ? 1 : 0;
	}

	private static double CalculateOptimizedImmediate(Opcode opcode, double left, double right)
	{
		var result = opcode switch
		{
			Opcode.OP_UNKNOWN_200 => left + right,
			Opcode.OP_UNKNOWN_201 => left - right,
			Opcode.OP_UNKNOWN_202 => left * right,
			Opcode.OP_UNKNOWN_203 => right != 0.0d ? left / right : 0.0d,
			Opcode.OP_UNKNOWN_204 => right != 0.0d ? left - right * Math.Floor(left / right) : 0.0d,
			Opcode.OP_UNKNOWN_205 => Math.Pow(left, right),
			_                     => 0.0d,
		};

		return double.IsNaN(result) ? 0.0d : result;
	}

	private static double CalculateOptimizedLogical(Opcode opcode, double left, double right) =>
		opcode switch
		{
			Opcode.OP_UNKNOWN_66 or Opcode.OP_UNKNOWN_206 => left != 0.0d && right != 0.0d ? 1.0d : 0.0d,
			Opcode.OP_UNKNOWN_67 or Opcode.OP_UNKNOWN_207 => left != 0.0d || right != 0.0d ? 1.0d : 0.0d,
			_                                             => 0.0d,
		};

	private static bool IsOptimizedImmediateComparisonTrue(Opcode opcode, int comparison) =>
		opcode switch
		{
			Opcode.OP_UNKNOWN_224 => comparison < 0,
			Opcode.OP_UNKNOWN_225 => comparison > 0,
			Opcode.OP_UNKNOWN_226 => comparison <= 0,
			Opcode.OP_UNKNOWN_227 => comparison >= 0,
			_                     => false,
		};

	private static bool IsScriptNumeric(object value) => value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

	private static double GetScriptArraySize(object? value) => GetArrayValues(value)?.Count ?? 0.0d;

	private static object? GetScriptArrayCell(object? array, int index)
	{
		var values = GetArrayValues(array);
		if (values == null)
			return index == 0 ? UnwrapScriptValue(array) ?? 0.0d : 0.0d;

		if (index >= 0 && index < values.Count)
			return values[index];

		return GetOutOfRangeArrayCellValue(values);
	}

	private static IStackEntry GetScriptArrayCellEntry(object? array, int index)
	{
		var value = UnwrapScriptValue(array);
		if (value is IList { IsReadOnly: false } list && index >= 0 && index < list.Count)
			return new ListCellStackEntry(list, index);

		return (GetScriptArrayCell(value, index) ?? 0.0d).ToStackEntry();
	}

	private static object? GetOutOfRangeArrayCellValue(IEnumerable<object?> values)
	{
		foreach (var value in values.Select(UnwrapScriptValue))
		{
			if (value is TString or string)
				return string.Empty;

			if (value is VariableCollection or IDictionary or IEnumerable)
				return 0.0d;
		}

		return 0.0d;
	}

	private static object? GetScriptArrayCell2(object? array, int x, int y) => GetScriptArrayCell(GetScriptArrayCell(array, x), y);

	private static void SetScriptArrayCell(IStackEntry arrayEntry, int index, object? value)
	{
		if (index < 0)
			return;

		var values = GetMutableScriptArray(arrayEntry);
		if (values == null)
			return;

		EnsureScriptArraySize(values, index + 1);
		values[index] = value;
	}

	private static void SetScriptArrayCell2(IStackEntry arrayEntry, int x, int y, object? value)
	{
		if (x < 0 || y < 0)
			return;

		var values = GetMutableScriptArray(arrayEntry);
		if (values == null)
			return;

		EnsureScriptArraySize(values, x + 1);
		var nested = GetMutableScriptArrayValue(values[x]);
		values[x] = nested;

		EnsureScriptArraySize(nested, y + 1);
		nested[y] = value;
	}

	private static List<object?> CreateScriptArray(int size)
	{
		var array = new List<object?>(size);
		EnsureScriptArraySize(array, size);
		return array;
	}

	private static int ClampScriptArraySize(int size) => Math.Clamp(size, 0, 10000);

	private static void ResizeScriptArray(IList array, int size)
	{
		EnsureScriptArraySize(array, size);
		while (array.Count > size)
			array.RemoveAt(array.Count - 1);
	}

	private static void EnsureScriptArraySize(IList array, int size)
	{
		while (array.Count < size)
			array.Add(0.0d);
	}

	private static void ExpandScriptArray(IStackEntry arrayEntry, int size)
	{
		var values = GetMutableScriptArray(arrayEntry);
		if (values == null)
			return;

		ExpandScriptArrayNodes(values, size);
	}

	private static void ExpandScriptArrayNodes(IList values, int size)
	{
		for (var i = 0; i < values.Count; i++)
		{
			var value = UnwrapScriptValue(values[i]);
			if (value is IList { IsReadOnly: false, IsFixedSize: false } child)
			{
				ExpandScriptArrayNodes(child, size);
				continue;
			}

			if (value is IEnumerable enumerable && value is not TString && value is not string)
			{
				var mutableChild = enumerable.Cast<object?>().Select(UnwrapScriptValue).ToList();
				values[i] = mutableChild;
				ExpandScriptArrayNodes(mutableChild, size);
				continue;
			}

			values[i] = CreateScriptArray(size);
		}
	}

	private static IList GetMutableScriptArrayValue(object? value)
	{
		value = UnwrapScriptValue(value);
		if (value is IList { IsReadOnly: false, IsFixedSize: false } list)
			return list;
		if (value is IEnumerable enumerable && value is not TString && value is not string)
			return enumerable.Cast<object?>().Select(UnwrapScriptValue).ToList();
		return new List<object?>();
	}

	private static List<object?> GetScriptSubArray(object? array, int start, int length)
	{
		var values = GetArrayValues(array) ?? [];
		if (start < 0)
			start = 0;
		if (start > values.Count)
			start = values.Count;
		if (length < 0)
			length = values.Count;

		var end = start + length;
		if (end > values.Count)
			end = values.Count;
		return values.GetRange(start, end - start);
	}

	private static IList? GetMutableScriptArray(IStackEntry entry)
	{
		var value = UnwrapScriptValue(entry.GetValue());
		if (value is IList { IsReadOnly: false, IsFixedSize: false } list)
			return list;
		if (value is TString or string && !string.IsNullOrEmpty(value.ToString()))
			return null;
		if (value is IEnumerable enumerable)
		{
			var copy = enumerable.Cast<object?>().Select(UnwrapScriptValue).ToList();
			entry.SetValue(copy);
			return copy;
		}

		if (value == null || value is TString or string || entry.Type == Number && ToScriptDouble(value) == 0.0d)
		{
			var listCopy = new List<object?>();
			entry.SetValue(listCopy);
			return listCopy;
		}

		return null;
	}

	private static double GetScriptObjectType(IStackEntry entry)
	{
		var value = UnwrapScriptValue(entry.GetValue());
		if (GetArrayValues(value, includeVariableCollections: false) != null)
			return 3.0d;
		if (value is VariableCollection or Script or IGuiControl)
			return 2.0d;
		if (value is TString or string)
			return 1.0d;
		return 0.0d;
	}

	private static int ToScriptInt(double value)
	{
		var adjusted = value + 0.0001d;
		var result   = (int)adjusted;
		if (adjusted < 0.0d && adjusted != result)
			result--;
		return result;
	}

	private static double GetRandomValue(double first, double second)
	{
		var rangeStart = first;
		var rangeEnd   = second;
		if (first == second) return first;
		if (first > second)
		{
			rangeStart = second;
			rangeEnd   = first;
		}

		if (rangeStart + 1.0d < rangeEnd)
		{
			var adjustedEnd = rangeEnd + 1.0d;
			var truncated   = Math.Floor(adjustedEnd);
			if (rangeEnd == truncated)
				rangeEnd -= 1.0d;
		}

		return Random.Shared.NextDouble() * (rangeEnd - rangeStart) + rangeStart;
	}

	private static double GetAngle(double x, double y)
	{
		if (x == 0.0d)
			return y > 0.0d ? 4.71238898038469d : Math.PI / 2.0d;

		var angle = Math.Atan(-y / x);
		if (x < 0.0d)
			angle += Math.PI;
		if (angle < 0.0d)
			angle += Math.PI * 2.0d;
		return angle;
	}

	private static double GetDirection(double x, double y)
	{
		return Math.Abs(x) > Math.Abs(y) ? x >= 0.0d ? 3.0d : 1.0d : y >= 0.0d ? 2.0d : 0.0d;
	}

	private static string GetScriptSubstring(string value, int start, int length)
	{
		if (start < 0)
			start = 0;
		if (start > value.Length)
			start = value.Length;
		if (length < 0 || start + length > value.Length)
			length = value.Length - start;
		if (length < 0)
			length = 0;
		return value.Substring(start, length);
	}

	public void Reset()
	{
		_rootTempVariables.Clear();
		_executionState.Value = null;
	}

	private sealed class ExecutionState
	{
		public Stack<ScriptVariable>  TempFrames       { get; }      = new();
		public Stack<ScriptVariable>  LocalFrames      { get; }      = new();
		public Stack<HashSet<string>> TempAliasFrames  { get; }      = new();
		public Stack<string>          FunctionFrames   { get; }      = new();
		public Stack<IStackEntry>     WithScope        { get; set; } = new();
		public ScriptVariable?        ReceiverOverride { get; set; }
		public int                    IndexPos         { get; set; }
		public bool                   UseTemp          { get; set; }
		public string                 ActiveEvent      { get; set; } = string.Empty;
	}
}
