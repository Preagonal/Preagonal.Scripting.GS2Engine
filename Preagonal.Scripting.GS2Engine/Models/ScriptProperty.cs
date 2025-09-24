using System;
using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public delegate void CallbackDelegate(object? value);

public class ScriptProperty<TInstance> : IScriptProperty where TInstance : class
{

	public ScriptProperty(IPropertyDefinition<TInstance> definition, IScriptProperties properties)
	{
		PropertyName       = definition.PropertyName;
		Properties         = properties;
		ReturnType         = definition.ReturnType;
		WriteTyped         = definition.Write;
		ReadTyped          = definition.Read;
		ScriptPropertyType = ScriptPropertyType.Variable;
		CallTyped          = null;
	}

	public ScriptProperty(IFunctionDefinition<TInstance> definition, IScriptProperties properties)
	{
		PropertyName       = definition.PropertyName;
		Properties         = properties;
		ReturnType         = definition.ReturnType;
		WriteTyped         = null;
		ReadTyped          = null;
		ScriptPropertyType = ScriptPropertyType.Function;
		CallTyped          = definition.Call;
	}

	public  ScriptPropertyType                       ScriptPropertyType { get; }
	public  string                                   PropertyName       { get; }
	public  Type                                     MainType           { get; } = typeof(TInstance);
	public  Type                                     ReturnType         { get; }
	private Func<TInstance, object?>?                ReadTyped          { get; }
	private Action<TInstance, object?>?              WriteTyped         { get; }
	private Func<TInstance, IStackEntry[], object?>? CallTyped          { get; }
	private CallbackDelegate?                        Callback           { get; set; }
	public  IScriptProperties?                       Properties         { get; }

	public  bool                                     HasWriteMethod => WriteTyped != null;
	public  bool                                     HasReadMethod => ReadTyped != null;
	public  bool                                     IsFunction => ScriptPropertyType == ScriptPropertyType.Function;

	object? IScriptProperty.Read(object instance) => ReadTyped?.Invoke((TInstance)instance);

	void IScriptProperty.Write(object instance, object? value)
	{
		WriteTyped?.Invoke((TInstance)instance, value);
		Callback?.Invoke(value);
	}

	object? IScriptProperty.Call(object instance, params IStackEntry[] value) =>
		CallTyped?.Invoke((TInstance)instance, value);

	public void SetCallback(CallbackDelegate callback) => Callback = callback;
}