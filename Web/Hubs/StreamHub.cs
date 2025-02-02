using Microsoft.AspNetCore.SignalR;

namespace BlazorWeb.Hubs;

/// <summary>
/// SignalR hub for subscribing to streams.
/// </summary>
public class StreamHub : Hub
{
	/// <summary>
	/// Subscribe to a topic.
	/// </summary>
	public Task SubTo(string topic)
		=> Groups.AddToGroupAsync(Context.ConnectionId, topic);

	/// <summary>
	/// Unsubscribe from a topic.
	/// </summary>
	/// <param name="topic"></param>
	/// <returns></returns>
	public Task UnsubFrom(string topic)
		=> Groups.RemoveFromGroupAsync(Context.ConnectionId, topic);
}
