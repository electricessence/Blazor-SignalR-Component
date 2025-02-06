using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A tracker for spawned <see cref="HubAdapter"/> instances.
/// A tracker for spawned <see cref="HubAdapter"/> instances.
/// </summary>
internal class SpawnTracker<T> : IDisposable
	where T : IDisposable
{
	private static readonly ConcurrentBag<HashSet<T>> _hashSetPool = [];

	private readonly Lock _spawnLock = new();
	private HashSet<T>? _spawn = _hashSetPool.TryTake(out var s) ? s : [];
	private Func<Action<T>, T>? _factory;

	public bool WasDisposed => _spawn is null;

	internal void InitFactory(Func<Action<T>, T> factory)
		=> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

	public void Remove(T adapter)
	{
		// Disposing/disposed?
		if(_spawn is null)
			return;

		lock (_spawnLock)
		{
			var s = _spawn;
			if (s is null)
				return;

			s.Remove(adapter);
		}
	}

	public T Spawn()
	{
		ObjectDisposedException.ThrowIf(_spawn is null, typeof(SpawnTracker<T>));

		// Create out of lock (optmistic).
		Debug.Assert(_factory is not null);
		var adapter = _factory(Remove);

		// Finish DCL pattern.
		lock (_spawnLock)
		{
			ObjectDisposedException.ThrowIf(_spawn is null, typeof(SpawnTracker<T>));
			_spawn.Add(adapter);
		}

		return adapter;
	}

	public void Dispose()
	{
		var s = _spawn;
		if (s is null)
			return;

		lock (_spawnLock)
		{
			// Extract and nullify the spawn list.
			s = _spawn;
			if (s is null)
				return;
			_spawn = null;
		}

		// By using this pattern we avoid creating a new list of HashSet<T> to dispose.

		foreach (var d in s)
		{
			try { d.Dispose(); }
			catch { }
		}

		s.Clear();

		// Not an exact science here as there might be multiple threads adding to the pool, but this is good enough.
		if(_hashSetPool.Count < 10)
			_hashSetPool.Add(s);
	}
}
