using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public delegate TRet? PropertyFunctionDelegate<in T, out TRet>(T instance, params IStackEntry[] arguments);

public delegate TRet? ContextualPropertyFunctionDelegate<in T, out TRet>(T instance, ScriptMachine machine, params IStackEntry[] arguments);