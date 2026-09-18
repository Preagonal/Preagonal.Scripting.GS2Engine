using System;
using System.Collections.Generic;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public interface IFunctionDefinition<in T>
{
	string                                     PropertyName { get; }
	string                                     Description  { get; }
	Type                                       ReturnType   { get; }
	IReadOnlyList<FunctionParameterDefinition> Parameters   { get; }
	object?                                    Call(T instance, ScriptMachine? machine, params IStackEntry[] arguments);
}