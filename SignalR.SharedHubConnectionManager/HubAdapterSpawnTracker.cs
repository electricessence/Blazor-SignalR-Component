namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A tracker for spawned <see cref="HubAdapter"/> instances.
/// Helps
/// </summary>
internal readonly struct HubAdapterSpawnTracker(IHubAdapter parent) : IDisposable
{
	private readonly Lock _spawnLock = new();
	private readonly HashSet<HubAdapter> _spawn = [];

	private void Remove(HubAdapter adapter)
	{
		lock (_spawnLock) _spawn.Remove(adapter);
	}

	public HubAdapter Spawn()
	{
		var adapter = new HubAdapter(parent, Remove);
		lock (_spawnLock)
		{
			_spawn.Add(adapter);
		}

		return adapter;
	}

	public void Dispose()
	{
		HubAdapter[] spawn;
		lock (_spawnLock)
		{
			spawn = _spawn.ToArray();
			_spawn.Clear();
		}

		foreach (var s in spawn)
		{
			try { s.Dispose(); }
			catch { }
		}
	}
}
