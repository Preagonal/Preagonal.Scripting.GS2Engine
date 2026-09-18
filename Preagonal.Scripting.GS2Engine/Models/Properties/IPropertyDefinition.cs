using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public interface IPropertyDefinition<in T>
{
	string              PropertyName { get; }
	string              Description  { get; }
	Type                ReturnType   { get; }
	PropertyType        PropertyType { get; }
	Func<T, object?>?   Read         { get; }
	Action<T, object?>? Write        { get; }
}