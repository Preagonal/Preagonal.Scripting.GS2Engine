using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Preagonal.Scripting.GS2Engine.Extensions;

namespace Preagonal.Scripting.GS2Engine.Models;

public class VariableCollection
{
	public delegate object? VariableCollectionGetCallback();

	private readonly Dictionary<string, IStackEntry> _collection = new();
	private readonly Lock                            _syncRoot   = new();

	public VariableCollection()
	{
	}

	public VariableCollection(IDictionary<string, IStackEntry>? collection) => AddOrUpdate(collection);

	public IStackEntry this[TString key]
	{
		get => GetVariable(key);
		set => SetVariable(key, value);
	}

	public IStackEntry GetVariable(TString variable) => TryGetVariable(variable, out var entry) ? entry! : SetVariable(variable, "".ToStackEntry());

	public void Clear()
	{
		lock (_syncRoot)
			_collection.Clear();
	}

	public bool RemoveVariable(TString variable)
	{
		lock (_syncRoot)
			return _collection.Remove(variable.ToString());
	}

	public IStackEntry AddOrUpdate(TString variable, IStackEntry value, bool skipCallback = false)
	{
		lock (_syncRoot)
		{
			var key = variable.ToString();
			if (_collection.ContainsKey(key))
			{
				if (value is LinkedStackEntry)
					_collection[key] = value;
				else
					_collection[key].SetValue(value.GetValue(), skipCallback);
			}
			else
			{
				_collection.Add(key, value);
			}

			return _collection[key];
		}
	}

	public IStackEntry SetVariable(TString variable, IStackEntry value) => AddOrUpdate(variable, value);

	public bool ContainsVariable(TString variable)
	{
		lock (_syncRoot)
			return _collection.ContainsKey(variable.ToString());
	}

	public bool TryGetVariable(TString variable, out IStackEntry? entry)
	{
		lock (_syncRoot)
			return _collection.TryGetValue(variable.ToString(), out entry);
	}

	public void AddOrUpdate(IDictionary<string, IStackEntry>? collection)
	{
		if (collection == null) return;
		foreach (var variable in collection.ToArray())
			AddOrUpdate(variable.Key, variable.Value);
	}

	public IDictionary<string, IStackEntry> GetDictionary() => _collection;

	public IReadOnlyCollection<KeyValuePair<string, IStackEntry>> GetSnapshot()
	{
		lock (_syncRoot)
			return _collection.Count == 0 ? [] : _collection.ToArray();
	}

	public void AddOrUpdate(VariableCollection? collection)
	{
		if (collection != null)
			foreach (var variable in collection.GetSnapshot())
				AddOrUpdate(variable.Key, variable.Value);
	}
}