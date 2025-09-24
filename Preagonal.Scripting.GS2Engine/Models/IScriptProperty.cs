using System;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IScriptProperty
{
	public ScriptPropertyType ScriptPropertyType { get; }
	public string             PropertyName       { get; }
	public Type               MainType           { get; }
	public Type               ReturnType         { get; }
	public IScriptProperties? Properties         { get; }
	public bool               HasWriteMethod     { get; }
	public bool               HasReadMethod      { get; }
	public bool               IsFunction         { get; }
	object?                   Read(object instance);
	void                      Write(object instance, object? value);
	object?                   Call(object instance, params IStackEntry[] arguments);
	void                      SetCallback(CallbackDelegate callback);
}