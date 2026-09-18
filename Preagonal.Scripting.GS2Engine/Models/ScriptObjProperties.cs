using System.Linq;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptObjProperties : ScriptProperties<Script>
{
	public ScriptObjProperties() : base(typeof(ScriptVariable))
	{
		_ = ScriptVariable.PropertiesInstance;

		AddProperties(this, new() { { "hp", "The object's hit-point value.", _ => 0.00d }, });

		AddFunctions(
			this,
			new()
			{
				{
					"settimer", "Schedules the object's onTimeout event after the specified delay.", (control, machine, o2) =>
					{
						var value = o2.FirstOrDefault()?.GetValue();
						switch (value)
						{
							case double timeout:
								control.SetTimer(machine.CurrentReceiver, timeout);
								break;
						}

						return 0;
					},
					[new("delay", typeof(double))]
				},
			}
		);

		Compile();
	}
}