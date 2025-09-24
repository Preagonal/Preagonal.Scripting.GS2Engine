using System.Collections.Generic;

namespace Preagonal.Scripting.GS2Engine.Models.Properties;

public class PropertyDefinitions<TInstance> : List<IPropertyDefinition<TInstance>> where TInstance : class
{
	public void Add<TRet>(
		string propertyName,
		string description,
		PropertyReadDelegate<TRet, TInstance>? readTyped = null,
		PropertyWriteDelegate<TInstance, TRet>? writeTyped = null,
		PropertyType propertyType = PropertyType.Default
	) =>
		Add(new PropertyDefinition<TInstance, TRet>(propertyName, description, readTyped, writeTyped, propertyType));
}