using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptManager : IScriptManager
{
	public        IScriptTranslationProvider? TranslationProvider { get; set; }
	private const string                      GlobalScriptPrefix = "__script:";

	protected readonly ILogger<ScriptManager>                          _logger;
	public static      ConcurrentDictionary<string, IScriptProperties> GlobalProperties { get; } = [];
	public             ScriptVariable                                  GlobalVariables  { get; } = new();
	private readonly   Dictionary<string, ScriptObjectCreator>         _objectCreators     = new(StringComparer.OrdinalIgnoreCase);
	private readonly   Lock                                            _globalScriptsSync  = new();
	private            Script[]                                        _globalScripts      = [];
	private            bool                                            _globalScriptsDirty = true;
	private            Action<string>?                                 _classScriptRequestHandler;
	private readonly   ConcurrentQueue<QueuedScriptEvent>              _events = new();
	private            int                                             _dispatchingEvents;
	private            long                                            _anonymousObjectId;
	private            long                                            _frameTimeTicks;

	public DateTime CurrentTime => Volatile.Read(ref _frameTimeTicks) is > 0 and var ticks ? new(ticks, DateTimeKind.Utc) : DateTime.UtcNow;

	public void BeginFrame(DateTime now) => Volatile.Write(ref _frameTimeTicks, now.ToUniversalTime().Ticks);

	public ScriptManager(ILogger<ScriptManager> logger)
	{
		_logger = logger;
		_       = TString.PropertiesInstance;
		_       = ScriptArrayProperties.Instance;
		_       = VersionProperties.Instance;
		_       = ScriptUniverse.PropertiesInstance;
		RegisterDefaultObjectCreators();
	}

	public void RegisterGlobalObject(string name, ScriptVariable collection)
	{
		GlobalVariables.AddOrUpdate(name.ToLowerInvariant(), collection.ToStackEntry());

		if (collection is GuiControl guiControl)
			InstallEventCatchers(guiControl);
	}

	public void QueueEvent(
		ScriptVariable source,
		Script target,
		string eventName,
		IStackEntry[] arguments,
		ScriptVariable? receiver = null,
		ScriptExecutionContext? context = null
	) =>
		_events.Enqueue(
			new(
				source,
				target,
				target.EventGeneration,
				eventName,
				arguments.Select(entry => (IStackEntry)entry.GetValue().ToStackEntry()).ToArray(),
				receiver,
				context
			)
		);

	public async Task DispatchPendingEvents(Action<Script, ScriptExecutionContext?, Action>? execute = null)
	{
		if (Interlocked.Exchange(ref _dispatchingEvents, 1) != 0) return;
		try
		{
			// Events raised by callbacks wait for the next update rather than recursing inline.
			var count = _events.Count;
			for (var i = 0; i < count && _events.TryDequeue(out var queued); i++)
			{
				if (queued.Source is GuiControl { IsDisposed: true } || !queued.Target.ExecutionEnabled || queued.Generation != queued.Target.EventGeneration) continue;
				try
				{
					if (execute == null)
						await DispatchEvent(queued).ConfigureAwait(false);
					else
						execute(queued.Target, queued.Context, () => DispatchEvent(queued).ConfigureAwait(false).GetAwaiter().GetResult());
				}
				catch (Exception exception)
				{
					Tools.DebugLine($"Error calling {queued.Target.Name}.{queued.Name}: {exception}");
				}
			}
		}
		finally
		{
			Volatile.Write(ref _dispatchingEvents, 0);
		}
	}

	private static async Task DispatchEvent(QueuedScriptEvent queued)
	{
		var previousContext = queued.Target.Machine.ScriptExecutionContext;
		try
		{
			queued.Target.Machine.ScriptExecutionContext = queued.Context;
			await queued.Target.CallEntries(queued.Name, queued.Arguments, queued.Receiver).ConfigureAwait(false);
		}
		finally
		{
			queued.Target.Machine.ScriptExecutionContext = previousContext;
		}
	}

	public void RegisterGlobalScript(Script script)
	{
		lock (_globalScriptsSync)
		{
			GlobalVariables.AddOrUpdate(GetGlobalScriptKey(script), script.ToStackEntry());

			var scriptName = GetGlobalScriptNameKey(script);
			if (!string.IsNullOrEmpty(scriptName))
				GlobalVariables.AddOrUpdate(scriptName, script.ToStackEntry());

			_globalScriptsDirty = true;
		}

		if (script.Type != ScriptType.Class)
			foreach (var guiControl in GetGlobalGuiControls())
				guiControl.InstallEventCatchers(script);
	}

	public void RegisterGlobalVariable(string name, object? variable) => GlobalVariables.AddOrUpdate(name.ToLowerInvariant(), variable.ToStackEntry());

	public void RegisterObjectCreator(string typeName, ScriptObjectCreator creator) => _objectCreators[typeName] = creator;

	public void SetClassScriptRequestHandler(Action<string>? handler) => _classScriptRequestHandler = handler;

	public virtual void RequestClassScript(string className)
	{
		if (string.IsNullOrWhiteSpace(className)) return;

		_classScriptRequestHandler?.Invoke(className);
	}

	public bool TryCreateObject(string typeName, string objectName, Script script, out ScriptVariable? createdObject)
	{
		var requestedName = objectName;
		if (string.IsNullOrEmpty(objectName) || objectName.Equals("unknown_object", StringComparison.OrdinalIgnoreCase))
			objectName = $"__anonymous:{Interlocked.Increment(ref _anonymousObjectId)}";
		var normalizedObjectName = objectName.ToLowerInvariant();
		if (!string.IsNullOrEmpty(normalizedObjectName) && GlobalVariables.TryGetVariable(normalizedObjectName, out var existingEntry) && existingEntry?.GetValue<ScriptVariable>() is { } existingObject)
		{
			// A forward reference creates a plain placeholder. A later `new` must
			// replace it with the requested concrete object.
			if (existingObject.GetType() != typeof(ScriptVariable))
			{
				createdObject = existingObject;
				return true;
			}
		}

		if (_objectCreators.TryGetValue(typeName, out var creator))
		{
			createdObject = creator(objectName, script);
			RegisterCreatedObject(objectName, script, createdObject, requestedName);
			return true;
		}

		if (TryCreateProfile(typeName, objectName, out createdObject))
		{
			RegisterCreatedObject(objectName, script, createdObject, requestedName);
			return true;
		}

		if (GlobalVariables.TryGetVariable(typeName.ToLowerInvariant(), out var templateEntry) && templateEntry?.GetValue() is ScriptVariable template && template.GetType() == typeof(ScriptVariable))
		{
			createdObject = new(objectName);
			foreach (var (name, value) in template.GetSnapshot())
				createdObject.AddOrUpdate(name, CopyTemplateValue(value.GetValue()).ToStackEntry());
			RegisterCreatedObject(objectName, script, createdObject, requestedName);
			return true;
		}

		createdObject = null;
		return false;
	}

	private static object? CopyTemplateValue(object? value)
	{
		// Array values are copied; references to other script objects retain their identity.
		if (value is not IList array) return value;
		var copy = new List<object?>(array.Count);
		foreach (var item in array)
			copy.Add(CopyTemplateValue(item is IStackEntry entry ? entry.GetValue() : item));
		return copy;
	}

	public void UnregisterGlobalObject(string name, ScriptVariable collection)
	{
		foreach (var (key, entry) in GlobalVariables.GetSnapshot())
		{
			if (!ReferenceEquals(entry.GetValue(), collection)) continue;
			entry.SetValue(0d);
			GlobalVariables.RemoveVariable(key);
		}
	}

	public void UnregisterGlobalScript(Script script)
	{
		script.DiscardPendingEvents();
		foreach (var globalScript in GetGlobalScripts())
			globalScript.RemoveEventCatchersFrom(script);
		foreach (var guiControl in GetGlobalGuiControls())
			guiControl.RemoveEventCatchersFrom(script);

		foreach (var (name, entry) in GlobalVariables.GetSnapshot())
		{
			var value = entry.GetValue();
			if (ReferenceEquals(value, script) || value is ScriptVariable { OwnerScript: { } owner } && ReferenceEquals(owner, script))
			{
				GlobalVariables.RemoveVariable(name);
			}
		}

		lock (_globalScriptsSync)
			_globalScriptsDirty = true;
	}

	public IReadOnlyCollection<Script> GetGlobalScripts()
	{
		lock (_globalScriptsSync)
		{
			if (!_globalScriptsDirty) return _globalScripts;

			_globalScripts      = GlobalVariables.GetSnapshot().Where(pair => pair.Key.StartsWith(GlobalScriptPrefix, StringComparison.Ordinal)).Select(pair => pair.Value.GetValue()).OfType<Script>().ToArray();
			_globalScriptsDirty = false;
			return _globalScripts;
		}
	}

	private static string GetGlobalScriptKey(Script script)     => $"{GlobalScriptPrefix}{script.GetHashCode()}";
	private static string GetGlobalScriptNameKey(Script script) => script.Name?.ToLowerInvariant() ?? string.Empty;

	private void RegisterCreatedObject(string objectName, Script script, ScriptVariable? createdObject, string requestedName)
	{
		if (createdObject == null) return;

		createdObject.OwnerScript ??= script;
		if (!string.IsNullOrWhiteSpace(objectName))
			RegisterGlobalObject(objectName, createdObject);
		if (requestedName.Equals("unknown_object", StringComparison.OrdinalIgnoreCase))
			RegisterGlobalObject(requestedName, createdObject);
	}

	private void InstallEventCatchers(GuiControl guiControl)
	{
		foreach (var script in GetGlobalScripts())
			if (script.Type != ScriptType.Class)
				guiControl.InstallEventCatchers(script);
	}

	private IReadOnlyCollection<GuiControl> GetGlobalGuiControls() => GlobalVariables.GetSnapshot().Select(pair => pair.Value.GetValue()).OfType<GuiControl>().ToList();

	private bool TryCreateProfile(string typeName, string objectName, out ScriptVariable? createdObject)
	{
		if (!typeName.EndsWith("Profile", StringComparison.OrdinalIgnoreCase))
		{
			createdObject = null;
			return false;
		}

		var profile = CreateGuiControlProfile(objectName, copyDefaultProfile: true);
		if (GlobalVariables.TryGetVariable(typeName.ToLowerInvariant(), out var templateEntry) && templateEntry?.GetValue<GuiControlProfile>() is { } template)
		{
			profile.CopyFrom(template);
		}

		createdObject = profile;
		return true;
	}

	private void RegisterDefaultObjectCreators()
	{
		RegisterObjectCreator("GuiControl", (id, script) => new GuiControl(id, script));
		RegisterObjectCreator("GuiControlProfile", (id, _) => CreateGuiControlProfile(id, copyDefaultProfile: true));
		RegisterObjectCreator("TStaticVar", (id, _) => new(id));
	}

	private GuiControlProfile CreateGuiControlProfile(string objectName, bool copyDefaultProfile)
	{
		var profile = new GuiControlProfile(objectName);
		if (!copyDefaultProfile ||
		    objectName.Equals("GuiDefaultProfile", StringComparison.OrdinalIgnoreCase) ||
		    !GlobalVariables.TryGetVariable("guidefaultprofile", out var defaultEntry) ||
		    defaultEntry?.GetValue<GuiControlProfile>() is not { } defaultProfile)
		{
			return profile;
		}

		profile.CopyFrom(defaultProfile);
		return profile;
	}
}