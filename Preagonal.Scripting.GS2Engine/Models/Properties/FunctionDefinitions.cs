using System;
using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public class FunctionDefinitions<TInstance> : List<IFunctionDefinition<TInstance>> where TInstance : class
{
	public void Add<TRet>(string propertyName, string description, PropertyFunctionDelegate<TInstance, TRet>? callTyped = null, IReadOnlyList<FunctionParameterDefinition>? parameters = null, Type? returnType = null) =>
		Add(new FunctionDefinition<TInstance, TRet>(propertyName, description, callTyped, parameters, returnType));

	public void Add<TRet>(string propertyName, string description, ContextualPropertyFunctionDelegate<TInstance, TRet> callTyped, IReadOnlyList<FunctionParameterDefinition>? parameters = null, Type? returnType = null) =>
		Add(new ContextualFunctionDefinition<TInstance, TRet>(propertyName, description, callTyped, parameters, returnType));
}