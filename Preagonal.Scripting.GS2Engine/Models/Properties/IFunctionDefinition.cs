using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public interface IFunctionDefinition<in T>
{
	string       PropertyName { get; }
	string       Description  { get; }
	Type         ReturnType   { get; }
	object?      Call(T instance, params IStackEntry[] arguments);
}