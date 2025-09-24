namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public delegate TRet? PropertyFunctionDelegate<in T, out TRet>(T o, params IStackEntry[] o2);