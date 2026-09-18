using System;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public sealed record FunctionParameterDefinition(string Name, Type Type, bool Optional = false);