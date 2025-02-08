namespace Open.SignalR.SharedClient;

/// <summary>
/// Provides <see cref="IHubAdapter"/> instances for hub paths.
/// </summary>
public interface IHubAdapterProvider
{
	/// <summary>
	/// Gets a <see cref="IHubAdapter"/> instance for the specified hub path.
	/// </summary>
	IHubAdapter GetAdapterFor(string hub);
}
