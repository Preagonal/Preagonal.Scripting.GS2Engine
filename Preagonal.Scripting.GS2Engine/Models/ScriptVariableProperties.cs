using System;
using System.Linq;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

public class ScriptVariableProperties : ScriptProperties<ScriptVariable>
{
	public ScriptVariableProperties() : base(null)
	{
		AddProperties(this, new() { { "joinedclasses", "The names of the classes joined to this object.", variable => variable.JoinedClasses, (variable, value) => variable.JoinedClasses = value }, });

		AddFunctions(
			this,
			new()
			{
				{ "getdynamicvarnames", "Lists dynamic field names, excluding built-in properties.", (variable, _) => GetVariableNames(variable, false, true) },
				{ "getstaticvarnames", "Lists built-in property names rather than dynamic fields.", (variable, _) => GetVariableNames(variable, true, false) },
				{ "getvarnames", "Lists the names of this object's fields, including registered properties and dynamic variables.", (variable, _) => GetVariableNames(variable, true, true) },
				{
					"clearvars", "Removes this object's dynamic fields.", (variable, _) =>
					{
						variable.Clear();
						return 0;
					}
				},
				{
					"isinclass", "Checks whether this object has joined the specified class.",
					(variable, args) => args.Length > 0 && variable.JoinedClassNames.Contains(args[0].GetValue()?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase), [new("className", typeof(string))]
				},
				{ "degtorad", "Converts degrees to radians.", (_, args) => args.Length > 0 ? args[0].GetValue<double>() * Math.PI / 180d : 0d, [new("degrees", typeof(double))] },
				{ "radtodeg", "Converts radians to degrees.", (_, args) => args.Length > 0 ? args[0].GetValue<double>() * 180d / Math.PI : 0d, [new("radians", typeof(double))] },
				{
					"cancelevents", "Cancels scheduled events on this object that have the supplied event name.", (variable, machine, args) =>
					{
						if (args.Length > 0)
							(variable as Script ?? variable.OwnerScript ?? machine.CurrentScript).CancelEvents(variable, args[0].GetValue()?.ToString() ?? string.Empty);
						return 0;
					},
					[new("eventName", typeof(string))]
				},
				{
					"scheduleevent", "Schedules onEventname after a delay in seconds, followed by the event name and its arguments.", (variable, machine, args) =>
					{
						if (args.Length < 2) return false;

						var eventName = args[1].GetValue()?.ToString() ?? string.Empty;
						var script    = variable as Script ?? variable.OwnerScript ?? machine.CurrentScript;
						return script.ScheduleEvent(variable, args[0].GetValue<double>(), eventName, args[2..]);
					},
					[new("delay", typeof(double)), new("eventName", typeof(string)), new("arguments", typeof(object[]), true)]
				},
				{
					"hasfunction", "Checks whether the named function is available to the calling script on this object.", (variable, machine, args) => args.Length > 0 && machine.HasFunction(variable, args[0].GetValue()?.ToString() ?? string.Empty),
					[new("functionName", typeof(string))]
				},
				{ "trigger", "Queues onEventname with the supplied arguments without interrupting the running script.", Trigger, [new("eventName", typeof(string)), new("arguments", typeof(object[]), true)] },
				{
					"catchevent", "Registers a handler for an object's event; the handler receives the emitting object as its first argument.", (variable, machine, args) =>
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
					"ignoreevent", "Stops receiving the named event from the specified object.", (variable, machine, args) =>
					{
						if (args.Length < 2 || ResolveEventTarget(machine, args[0]) is not { } target) return false;
						(target.OwnerScript ?? target as Script)?.IgnoreEvent(target.Name, args[1].GetValue()?.ToString() ?? "", variable as Script ?? variable.OwnerScript ?? machine.CurrentScript);
						return true;
					},
					[new("object", typeof(object)), new("eventName", typeof(string))]
				},
				{
					"join", "Joins a class to gain its functions and event handlers.", (variable, args) =>
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
					"leave", "Detaches a class previously joined to this object.", (variable, args) =>
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

	private static string[] GetVariableNames(ScriptVariable variable, bool includeProperties, bool includeDynamic)
	{
		var names = includeProperties ? variable.Properties.Where(property => !property.IsFunction && property.HasReadMethod).Select(property => property.PropertyName) : Enumerable.Empty<string>();
		if (includeDynamic)
			names = names.Concat(variable.GetSnapshot().Select(pair => pair.Key));
		return names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
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
		if (!eventName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
			eventName = $"on{eventName}";

		var targetScript = variable as Script ?? variable.OwnerScript ?? machine.CurrentScript;
		var targetEvent  = variable is Script || string.IsNullOrWhiteSpace(variable.Name) ? eventName : $"{variable.Name}.{eventName}";
		targetScript.QueueEvent(variable, targetEvent, args[1..], variable, machine.ScriptExecutionContext);
		return 0;
	}
}