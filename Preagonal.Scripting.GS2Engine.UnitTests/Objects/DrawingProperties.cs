using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests.Objects;

public class DrawingProperties : ScriptProperties<Drawing>
{
	public DrawingProperties() : base(typeof(ScriptVariable))
	{
		AddProperties(
			this,
			new()
			{
				{ "rotation", "", drawing => drawing.Rotation, (drawing, rotation) => drawing.Rotation = rotation },
			}
		);

		/*
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
		*/

		Compile();
	}
}