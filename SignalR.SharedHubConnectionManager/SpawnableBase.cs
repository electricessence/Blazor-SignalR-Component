namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A base class for a host/parent object that can spawn other objects via factory and can clean them up when disposed.
/// </summary>
public abstract class SpawnableBase<T>() : ISpawn<T>
	where T : notnull, IAsyncDisposable
{
	private readonly SpawnTracker<T> _spawnTracker = new();

	/// <summary>
	/// A <paramref name="factory"/> must be provided for spawning instances of <typeparamref name="T"/>.
	/// </summary>
	protected void InitFactory(Func<Action<T>, T> factory)
		=> _spawnTracker.InitFactory(factory);

	/// <summary>
	/// Spawns a new instance of <typeparamref name="T"/>.
	/// </summary>
	public T Spawn() => _spawnTracker.Spawn();

	/// <summary>
	/// Disposes of all spawned instances.
	/// </summary>
	public ValueTask DisposeSpawned()
		=> _spawnTracker.DisposeAsync();

	/// <summary>
	/// The local method for disposal.
	/// </summary>
	protected virtual ValueTask OnDisposeAsync()
		=> DisposeSpawned();

	int _disposed = 0;

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
	protected void AssertIsAlive()
		=> ObjectDisposedException.ThrowIf(_disposed is 1, this);

	/// <summary>
	/// Disposed of all spawned instances.
	/// </summary>
	/// <remarks>Is thread safe and can be called multiple times.</remarks>
	public ValueTask DisposeAsync()
		=> _disposed is 1 || Interlocked.CompareExchange(ref _disposed, 1, 0) == 1
			? default
			: OnDisposeAsync();
}
