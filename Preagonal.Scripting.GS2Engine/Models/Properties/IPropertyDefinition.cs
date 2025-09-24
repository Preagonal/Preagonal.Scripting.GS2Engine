using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public interface IPropertyDefinition<in T>
{
	string       PropertyName { get; }
	string       Description  { get; }
	Type         ReturnType   { get; }
	PropertyType PropertyType { get; }
	object?      Read(T instance);
	void         Write(T instance, object? value);
}