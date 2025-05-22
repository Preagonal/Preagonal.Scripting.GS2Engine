using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.GS2.ByteCode;
using Preagonal.Scripting.GS2Engine.Models;
using static System.IO.File;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class Script : VariableCollection
{
	public delegate IStackEntry Command(ScriptMachine machine, IStackEntry[]? args);

	public static readonly VariableCollection                                GlobalVariables = new();
	public static readonly ConcurrentDictionary<string, VariableCollection?> GlobalObjects   = new();
	public static readonly ConcurrentDictionary<string, Command>             GlobalFunctions = new();
	public static readonly ConcurrentDictionary<string, Script>              GlobalScripts   = new();

	private readonly List<TString>                      _strings  = [];
	public readonly  Dictionary<string, FunctionParams> Functions = new();
	private          ScriptCom[]                        _bytecode = [];
	public readonly  VariableCollection?                RefObject = null;
	public           bool                               ExecutionEnabled  { get; private set; } = true;
	public           TString                            Name              { get; set; }
	public           TString                            File              { get; set; }
	public           ScriptType                         Type              { get; }
	private          int                                Gs1Flags          { get; set; }
	public           ScriptMachine                      Machine           { get; }
	public           DateTime?                          Timer             { get; private set; }
	public           Dictionary<string, Command>        ExternalFunctions { get; }              = new();
	public           ScriptCom[]                        Bytecode          => _bytecode;



	public Script(ScriptType type)
	{
		Name      = string.Empty;
		File      = string.Empty;
		RefObject = this;
		Machine   = new(this);
		Type      = type;
	}

	public Script(
		TString bytecodeFile,
		VariableCollection? refObject = null,
		ScriptType? type = null
	)
	{
		Name      = Path.GetFileNameWithoutExtension(bytecodeFile);
		File      = bytecodeFile;
		RefObject = refObject;
		Machine   = new(this);
		Type      = type ?? ScriptType.Weapon;

		SetStream(ReadAllBytes(bytecodeFile));

		Init();
	}

	public Script(
		TString name,
		byte[] bytecode,
		VariableCollection? refObject = null,
		ScriptType? type = null
	)
	{
		Name      = name;
		File      = "";
		RefObject = refObject;
		Machine   = new(this);
		Type      = type ?? ScriptType.Weapon;

		SetStream(bytecode);

		Init();
	}

	~Script()
	{
		if (GlobalScripts.ContainsKey(GetHashCode().ToString()))
			GlobalScripts.TryRemove(GetHashCode().ToString(), out _);
	}

	public void UpdateFromFile(string scriptFile)
	{
		Name = Path.GetFileNameWithoutExtension(scriptFile);
		File = scriptFile;
		SetStream(ReadAllBytes(scriptFile));

		Init();
	}

	public void UpdateFromByteCode(TString name, byte[] byteCode)
	{
		Name = name;
		File = "";
		SetStream(byteCode);

		Init();
	}

	public void HaltExecution() => ExecutionEnabled = false;

	public void EnableExecution() => ExecutionEnabled = true;

	private void Init()
	{
		if (!GlobalScripts.ContainsKey(GetHashCode().ToString()))
			GlobalScripts.AddOrUpdate(GetHashCode().ToString(), this, (s, script) => Script.GlobalScripts[s] = script);

		foreach (var obj in GlobalFunctions)
			ExternalFunctions.Add(obj.Key, obj.Value);

		ExternalFunctions.Add(
			"settimer",
			delegate(ScriptMachine machine, IStackEntry[]? args)
			{
				if (args?.Length > 0 && ExecutionEnabled)
					SetTimer((double)(machine.GetEntry(args[0]).GetValue() ?? 0));
				return 0.ToStackEntry();
			}
		);

		EnableExecution();
	}

	private void Reset()
	{
		Machine.Reset();
		Functions.Clear();
		ExternalFunctions.Clear();
		_bytecode = [];
		_strings.Clear();
		Clear();
		HaltExecution();
	}

	private void SetStream(TString bytecodeParam)
	{
		var oIndex = 0;
		bytecodeParam.setRead(0);

		CheckHeader(bytecodeParam);

		Reset();

		while (bytecodeParam.bytesLeft() > 0)
		{
			Tools.DebugLine($"Bytes left: {bytecodeParam.bytesLeft()}");

			if (bytecodeParam.bytesLeft() == 1)
				if (bytecodeParam.readChar() == '\n')
					break;

			var segmentType = (BytecodeSegment)bytecodeParam.readInt();

			if (segmentType is < BytecodeSegment.Gs1EventFlags or > BytecodeSegment.Bytecode)
			{
				Tools.Debug($"Segment: Unknown ({segmentType})\n");
				break;
			}

			Tools.Debug($"Segment: {segmentType.BytecodeSegmentToString()}\n");

			var segmentLength = bytecodeParam.readInt();

			var segmentSection = bytecodeParam.readChars(segmentLength);

			switch (segmentType)
			{
				case BytecodeSegment.Gs1EventFlags:
				{
					var flags = 0;
					if (3 < segmentSection.length())
						flags = segmentSection.readInt();
					Gs1Flags = flags;
					break;
				}

				case BytecodeSegment.FunctionNames:
				{
					if (0 < segmentSection.length())
						while (segmentSection.bytesLeft() > 0)
						{
							TString functionName = new();
							var     pos          = segmentSection.readInt();

							while (true)
							{
								var ch = segmentSection.readChar();
								if (ch == '\0') break;
								functionName.writeChar(ch);
							}

							var isPublic = functionName.starts("public.");
							if (isPublic) functionName.removeStart(7);
							AddFunction(functionName, pos, isPublic);

							Tools.Debug($"Function[{pos}]: {functionName}\n");
						}

					break;
				}

				case BytecodeSegment.Strings:
				{
					if (0 < segmentSection.length())
						while (segmentSection.bytesLeft() > 0)
						{
							TString stringName = new();
							while (true)
							{
								var ch = segmentSection.readChar();
								if (ch == '\0') break;
								stringName.writeChar(ch);
							}

							_strings.Add(stringName);
							Tools.Debug($"String: {stringName}\n");
						}

					break;
				}

				case BytecodeSegment.Bytecode:
				{
					ScriptCom op = new();
					while (segmentSection.bytesLeft() > 0)
					{
						var bytecodeByte = segmentSection.readChar();
						if (bytecodeByte is >= 0xF0 and <= 0xF6)
							Tools.Debug($" (Opcode: 0x{bytecodeByte}) ");

						switch (bytecodeByte)
						{
							case 0xF0:
							{
								var varIndex = segmentSection.readChar();

								op.VariableName = _strings[varIndex];

								Tools.Debug($" - variable[{varIndex}]({op.VariableName}) (byte)\n");
								break;
							}
							case 0xF1:
							{
								var varIndex = segmentSection.readShort();

								op.VariableName = _strings[varIndex];

								Tools.Debug($" - string({op.VariableName}) (word)\n");
								break;
							}
							case 0xF2:
							{
								var varIndex = segmentSection.readInt();

								op.VariableName = _strings[varIndex];

								Tools.Debug($" - string({op.VariableName}) (dword)\n");
								break;
							}
							case 0xF3:
							{
								var varIndex = (sbyte)segmentSection.readChar();
								op.Value = varIndex;
								Tools.Debug($" - double({op.Value}) (byte)\n");
								break;
							}
							case 0xF4:
							{
								var varIndex = segmentSection.readShort();
								op.Value = varIndex;
								Tools.Debug($" - double({op.Value}) (word)\n");
								break;
							}
							case 0xF5:
							{
								var varIndex = segmentSection.readInt();
								op.Value = varIndex;
								Tools.Debug($" - double({op.Value}) (dword)\n");
								break;
							}
							case 0xF6:
							{
								TString doubleString = new();
								while (true)
								{
									var ch = segmentSection.readChar();
									if (ch == '\0') break;
									doubleString.writeChar(ch);
								}

								doubleString = doubleString.ToString().Replace("--", "");
								op.Value     = double.Parse(doubleString.ToString(), CultureInfo.InvariantCulture);
								Tools.Debug($" - double({op.Value}) (string)\n");
								break;
							}

							default:
							{
								if (oIndex >= _bytecode.Length) Array.Resize(ref _bytecode, oIndex + 0x100);
								//BytecodeLength = oIndex + 0x100;
								op        = _bytecode[oIndex] = new();
								op.OpCode = (Opcode)bytecodeByte;
								++oIndex;
								break;
							}
						}
					}

					Tools.DebugLine("Bytecode done");
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		Array.Resize(ref _bytecode, oIndex);

		OnScriptUpdated();
	}

	private static void CheckHeader(TString bytecodeParam)
	{
		if (bytecodeParam.bytesLeft() < 1) return;
		var isPacket = bytecodeParam.readChar();
		if (isPacket != 0xAC)
		{
			bytecodeParam.setRead(0);
			var segmentType = (BytecodeSegment)bytecodeParam.readInt();

			if (segmentType is < BytecodeSegment.Gs1EventFlags or > BytecodeSegment.Bytecode)
			{
				bytecodeParam.setRead(0);
			}
			else
			{
				return;
			}
		}

		Tools.DebugLine("GServer packet header included");
		var infoSectionLength = (ushort)bytecodeParam.readGShort();
		Tools.DebugLine($"Length of information section: {infoSectionLength}");

		var infoSection = bytecodeParam.readChars(infoSectionLength);

		var data = infoSection.ToString().Split(',');

		var target = data[0];
		var name   = data[1];

		Tools.DebugLine($"Code target: {target}");
		Tools.DebugLine($"Target name: {name}");

		int.TryParse(data[2], out var saveScriptToFileInt);
		var saveScriptToFile = saveScriptToFileInt == 1 ? "Yes" : "No";

		Tools.DebugLine($"Save script to file: {saveScriptToFile}");

		/*
		infoSection = data[3];
		int keys  = 0;
		while (infoSection.bytesLeft() > 0)
		{
			uint key = infoSection.readGInt5().toUInt();
			//scriptKeys[keys] = CString(key);
			Tools.DebugLine($"Key({keys}): {key}");
			keys++;
		}
		*/
	}

	private void AddFunction(TString functionName, int pos, bool isPublic) =>
		Functions.Add(functionName.ToString().ToLower(), new() { BytecodePosition = pos, IsPublic = isPublic });

	private static void OnScriptUpdated()
	{
		//fixBadByteCode();
		//checkOnlyFunctions();
		OptimizeByteCode();
	}

	private static void OptimizeByteCode()
	{
	}

	private async Task<IStackEntry> Execute(string functionName, Stack<IStackEntry>? parameters = null)
	{
		try
		{
			return await Machine.Execute(functionName, parameters).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Tools.DebugLine(e.Message);
			return 0.ToStackEntry();
		}
	}

	/// <summary>
	///     Function -> Call Event for Object
	/// </summary>
	public async Task<IStackEntry> Call(string eventName, params object[]? args)
	{
		try
		{

			if (args == null) return await Execute(eventName).ConfigureAwait(false);

			var callStack = new Stack<IStackEntry>();
			foreach (var variable in args.Reverse())
			{
				switch (variable)
				{
					case string s:
						callStack.Push(s.ToStackEntry());
						break;
					case int i:
						callStack.Push(i.ToStackEntry());
						break;
					case double d:
						callStack.Push(d.ToStackEntry());
						break;
					case float f:
						callStack.Push(f.ToStackEntry());
						break;
					case decimal dc:
						callStack.Push(dc.ToStackEntry());
						break;
					case string[] sa:
						callStack.Push(sa.ToStackEntry());
						break;
					case int[] ia:
						callStack.Push(ia.ToStackEntry());
						break;
					case bool bo:
						callStack.Push(bo.ToStackEntry());
						break;
					case VariableCollection p:
						callStack.Push(p.ToStackEntry());
						break;
				}
			}

			return await Execute(eventName, callStack).ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Console.WriteLine(e.Message);
		}

		return 0.ToStackEntry();
	}

	public async Task<IStackEntry> TriggerEvent(string eventName)
	{
		switch (eventName.ToLower())
		{
			case "ontimeout":
			{
				break;
			}
			default:
				return await Execute(eventName).ConfigureAwait(false);
		}

		return 0.ToStackEntry();
	}

	private void SetTimer(double value)
	{
		Timer = DateTime.UtcNow.AddSeconds(value);
		/*try
		{
			if (!ThreadPool.QueueUserWorkItem(
				    delegate
				    {
					    DelayedMethodCall(
						    value,
						    () => OnTriggerEvent("onTimeout").ConfigureAwait(false).GetAwaiter().GetResult()
					    );
				    }
			    ))
			{
				Console.WriteLine("Timer function not queued");
			}
		}
		catch (Exception e)
		{
			Console.WriteLine($"{e.Message}: {e}");
		}*/
	}

	private static void DelayedMethodCall(double seconds, Action methodToCall)
	{
		Thread.Sleep((int)(seconds * 1500));  // Convert seconds to milliseconds
		methodToCall();
	}

	public async Task OnTriggerEvent(string eventName)
	{
		Timer = null;
		await Execute(eventName).ConfigureAwait(false);
	}

	public void AddObjectReference(string objectType, VariableCollection obj)
	{
		if (GlobalObjects.ContainsKey(objectType))
			GlobalObjects[objectType] = obj;
		else
			GlobalObjects.AddOrUpdate(objectType, obj, (s, collection) => GlobalObjects[s] = collection);
	}
}