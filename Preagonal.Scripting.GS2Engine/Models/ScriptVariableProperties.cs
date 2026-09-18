namespace Preagonal.Scripting.GS2Engine.Models;

using System;
using System.Linq;
using Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptVariableProperties : ScriptProperties<ScriptVariable>
{
	public ScriptVariableProperties() : base(null)
	{
		AddProperties(this, new() { { "joinedclasses", "The names of the classes joined to this object.", variable => variable.JoinedClasses, (variable, value) => variable.JoinedClasses = value }, });

		AddFunctions(
			this,
			new()
			{
				{
					"clearvars", "Clears the variables stored on this object.", (variable, _) =>
					{
						variable.Clear();
						return 0;
					}
				},
				{
					"isinclass", "Returns whether this object has joined the named class.", (variable, args) => args.Length > 0 && variable.JoinedClassNames.Contains(args[0].GetValue()?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase),
					[new("className", typeof(string))]
				},
				{ "degtorad", "Converts degrees to radians.", (_, args) => args.Length > 0 ? args[0].GetValue<double>() * Math.PI / 180d : 0d, [new("degrees", typeof(double))] },
				{ "radtodeg", "Converts radians to degrees.", (_, args) => args.Length > 0 ? args[0].GetValue<double>() * 180d / Math.PI : 0d, [new("radians", typeof(double))] },
				{
					"cancelevents", "Cancels scheduled events with the given name on this object.", (variable, machine, args) =>
					{
						if (args.Length > 0)
							(variable as Script ?? variable.OwnerScript ?? machine.CurrentScript).CancelEvents(variable, args[0].GetValue()?.ToString() ?? string.Empty);
						return 0;
					},
					[new("eventName", typeof(string))]
				},
				{
					"scheduleevent", "Schedules an event on this object.", (variable, machine, args) =>
					{
						if (args.Length < 2) return false;

						var eventName = args[1].GetValue()?.ToString() ?? string.Empty;
						var script    = variable as Script ?? variable.OwnerScript ?? machine.CurrentScript;
						return script.ScheduleEvent(variable, args[0].GetValue<double>(), eventName, args[2..]);
					},
					[new("delay", typeof(double)), new("eventName", typeof(string)), new("arguments", typeof(object[]), true)]
				},
				{
					"hasfunction", "Returns whether this object exposes the named function.", (variable, machine, args) => args.Length > 0 && machine.HasFunction(variable, args[0].GetValue()?.ToString() ?? string.Empty),
					[new("functionName", typeof(string))]
				},
				{ "trigger", "Triggers an event on this object.", Trigger, [new("eventName", typeof(string))] },
				{
					"catchevent", "Catches an event emitted by another object.", (variable, machine, args) =>
					{
						if (args.Length < 2 || ResolveEventTarget(machine, args[0]) is not { } target || string.IsNullOrWhiteSpace(target.Name)) return false;

						var eventName    = args[1].GetValue()?.ToString() ?? string.Empty;
						var handlerName  = args.Length > 2 ? args[2].GetValue()?.ToString() ?? string.Empty : string.Empty;
						var targetScript = target.OwnerScript ?? target as Script;
						if (targetScript == null) return false;

						targetScript.CatchEvent(target, eventName, variable as Script ?? variable.OwnerScript ?? machine.CurrentScript, handlerName);
						return true;
					},
					[new("object", typeof(object)), new("eventName", typeof(string)), new("handlerName", typeof(string), true)]
				},
				{
					"ignoreevent", "Stops catching an event emitted by another object.", (variable, machine, args) =>
					{
						if (args.Length < 2 || ResolveEventTarget(machine, args[0]) is not { } target) return false;
						(target.OwnerScript ?? target as Script)?.IgnoreEvent(target.Name, args[1].GetValue()?.ToString() ?? "", variable as Script ?? variable.OwnerScript ?? machine.CurrentScript);
						return true;
					},
					[new("object", typeof(object)), new("eventName", typeof(string))]
				},
				{
					"join", "Joins a class script to this object.", (variable, args) =>
					{
						if (args.Length > 0)
						{
							var className = args[0].GetValue()?.ToString() ?? string.Empty;
							variable.Join(className);
							if (variable is Script script)
								script.ScriptManager.RequestClassScript(className);
						}

						return 0;
					},
					[new("className", typeof(string))]
				},
				{
					"leave", "Leaves a class script to this object.", (variable, args) =>
					{
						if (args.Length > 0)
						{
							var className = args[0].GetValue()?.ToString() ?? string.Empty;
							variable.Leave(className);
						}

						return 0;
					},
					[new("className", typeof(string))]
				},
			}
		);

		Compile();
	}

	private static ScriptVariable? ResolveEventTarget(ScriptMachine machine, IStackEntry entry)
	{
		if (entry.GetValue() is ScriptVariable target) return target;
		var name = entry.GetValue()?.ToString()?.ToLowerInvariant() ?? "";
		return machine.CurrentScript.ScriptManager.GlobalVariables.TryGetVariable(name, out var found) ? found?.GetValue() as ScriptVariable : null;
	}

	private static object Trigger(ScriptVariable variable, ScriptMachine machine, params IStackEntry[] args)
	{
		if (args.Length == 0) return 0;

		var eventName = args[0].GetValue()?.ToString() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(eventName)) return 0;
		if (!eventName.StartsWith("on", System.StringComparison.OrdinalIgnoreCase))
			eventName = $"on{eventName}";

		var targetScript = variable.OwnerScript ?? variable as Script ?? machine.CurrentScript;
		var targetEvent  = variable is Script || string.IsNullOrWhiteSpace(variable.Name) ? eventName : $"{variable.Name}.{eventName}";
		return targetScript.CallEntries(targetEvent, args[1..], variable).ConfigureAwait(false).GetAwaiter().GetResult().GetValue() ?? 0;
	}
}