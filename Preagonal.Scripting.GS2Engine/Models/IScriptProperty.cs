using System;
using System.Collections.Generic;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IScriptProperty
{
	public ScriptPropertyType                         ScriptPropertyType { get; }
	public string                                     PropertyName       { get; }
	public string                                     Description        { get; }
	public Type                                       MainType           { get; }
	public Type                                       ReturnType         { get; }
	public IReadOnlyList<FunctionParameterDefinition> Parameters         { get; }
	public IScriptProperties?                         Properties         { get; }
	public bool                                       HasWriteMethod     { get; }
	public bool                                       HasReadMethod      { get; }
	public bool                                       IsFunction         { get; }
	object?                                           Read(object instance);
	void                                              Write(object instance, object? value);
	object?                                           Call(object instance, params IStackEntry[] arguments);
	object?                                           Call(ScriptMachine machine, object instance, params IStackEntry[] arguments);
	void                                              SetCallback(CallbackDelegate callback);
}