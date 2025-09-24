using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Models;

public interface IScriptProperties : ICollection<IScriptProperty>
{
	public new void Add(IScriptProperty scriptProperty);
	void            Compile();
	bool            Compiled { get; }
}