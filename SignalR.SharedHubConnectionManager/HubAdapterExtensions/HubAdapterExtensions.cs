
namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Extension methods for <see cref="IHubAdapter"/>.
/// </summary>
public static partial class HubAdapterExtensions
{
	/// <summary>
	/// Returns an <see cref="IHubAdapter"/> wrapper around the specified <see cref="HubConnection"/>.
	/// </summary>
	public static HubConnectionAdapter AsAdapter(this HubConnection hubConnection)
		=> new (hubConnection);
}
