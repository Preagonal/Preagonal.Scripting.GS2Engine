using System.Linq;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptObjProperties : ScriptProperties<Script>
{
	public ScriptObjProperties() : base(typeof(ScriptVariable))
	{
		AddProperties(
			this,
			new()
			{
				{ "hp", "", _ => 0.00d },
			}
		);

		AddFunctions(
			this,
			new()
			{
				{
					"settimer",
					"",
					(control, o2) =>
					{
						var value = o2.FirstOrDefault()?.GetValue();
						switch (value)
						{
							case double timeout:
								control.SetTimer(timeout);
								break;
						}

						return 0;
					}
				},
			}
		);

		Compile();
	}
}