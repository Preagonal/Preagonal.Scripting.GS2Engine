using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.GS2.Script;

public class ScriptManager : IScriptManager
{
	private const string GlobalScriptPrefix = "__script:";

	protected readonly ILogger<ScriptManager>                          _logger;
	public static      ConcurrentDictionary<string, IScriptProperties> GlobalProperties { get; } = [];
	public             ScriptVariable                                  GlobalVariables  { get; } = new();
	private readonly   Dictionary<string, ScriptObjectCreator>         _objectCreators     = new(StringComparer.OrdinalIgnoreCase);
	private readonly   Lock                                            _globalScriptsSync  = new();
	private            Script[]                                        _globalScripts      = [];
	private            bool                                            _globalScriptsDirty = true;
	private            Action<string>?                                 _classScriptRequestHandler;

	public ScriptManager(ILogger<ScriptManager> logger)
	{
		_logger = logger;
		_       = TString.PropertiesInstance;
		_       = ScriptArrayProperties.Instance;
		_       = VersionProperties.Instance;
		RegisterDefaultObjectCreators();
	}

	public void RegisterGlobalObject(string name, ScriptVariable collection)
	{
		GlobalVariables.AddOrUpdate(name.ToLowerInvariant(), collection.ToStackEntry());

		if (collection is GuiControl guiControl)
			InstallEventCatchers(guiControl);
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
			RegisterCreatedObject(objectName, script, createdObject);
			return true;
		}

		if (TryCreateProfile(typeName, objectName, out createdObject))
		{
			RegisterCreatedObject(objectName, script, createdObject);
			return true;
		}

		if (GlobalVariables.TryGetVariable(typeName.ToLowerInvariant(), out var templateEntry) && templateEntry?.GetValue() is ScriptVariable template && template.GetType() == typeof(ScriptVariable))
		{
			createdObject = new ScriptVariable(objectName);
			foreach (var (name, value) in template.GetSnapshot())
				createdObject.AddOrUpdate(name, CopyTemplateValue(value.GetValue()).ToStackEntry());
			RegisterCreatedObject(objectName, script, createdObject);
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
		var normalizedName = name.ToLowerInvariant();
		if (string.IsNullOrEmpty(normalizedName) || !GlobalVariables.TryGetVariable(normalizedName, out var existingEntry) || !ReferenceEquals(existingEntry?.GetValue<ScriptVariable>(), collection))
		{
			return;
		}

		GlobalVariables.RemoveVariable(normalizedName);
	}

	public void UnregisterGlobalScript(Script script)
	{
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
	private static string GetGlobalScriptNameKey(Script script) => script.Name?.ToString().ToLowerInvariant() ?? string.Empty;

	private void RegisterCreatedObject(string objectName, Script script, ScriptVariable? createdObject)
	{
		if (createdObject == null) return;

		createdObject.OwnerScript ??= script;
		if (!string.IsNullOrWhiteSpace(objectName))
			RegisterGlobalObject(objectName, createdObject);
	}

	private void InstallEventCatchers(GuiControl guiControl)
	{
		foreach (var script in GetGlobalScripts())
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
		RegisterObjectCreator("TStaticVar", (id, _) => new ScriptVariable(id));
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