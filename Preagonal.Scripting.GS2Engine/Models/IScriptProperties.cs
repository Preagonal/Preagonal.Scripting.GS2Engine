using System;
using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IScriptProperties : ICollection<IScriptProperty>
{
	public new void Add(IScriptProperty scriptProperty);
	bool            TryGetProperty(string propertyName, out IScriptProperty property);
	void            Compile();
	bool            Compiled   { get; }
	Type?           ParentType { get; }
}