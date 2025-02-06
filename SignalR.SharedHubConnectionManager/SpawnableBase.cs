namespace SignalR.SharedHubConnectionManager;

public abstract class SpawnableBase<T>() : ISpawn<T>
	where T : notnull, IDisposable
{
	private readonly SpawnTracker<T> _spawnTracker = new();

	protected void InitFactory(Func<Action<T>, T> factory)
		=> _spawnTracker.InitFactory(factory);

	/// <summary>
	/// Spawns a new instance of <typeparamref name="T"/>.
	/// </summary>
	public T Spawn() => _spawnTracker.Spawn();

	/// <summary>
	/// Disposes of all spawned instances.
	/// </summary>
	public void DisposeSpawned() => _spawnTracker.Dispose();
}
