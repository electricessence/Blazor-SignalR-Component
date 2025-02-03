namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// An interface for spawning objects.
/// </summary>
public interface ISpawn<T>
	where T : notnull
{
	/// <summary>
	/// Spawns a new instance of <typeparamref name="T"/>.
	/// </summary>
	T Spawn();
}
