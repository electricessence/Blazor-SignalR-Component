namespace Open.SignalR.SharedHubConnection;

public static partial class HubAdapterExtensions
{
	/// <inheritdoc cref="HubConnectionExtensions.SendAsync(HubConnection, string, object?, object?, CancellationToken)"/>/>
	public static Task SendAsync(this IHubActions hubConnection, string methodName, object?[] args, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.SendCoreAsync(methodName, args, cancellationToken);
	}

	/// <inheritdoc cref="HubConnectionExtensions.SendAsync(HubConnection, string, object?, object?, CancellationToken)"/>/>
	public static Task SendAsync(this IHubActions hubConnection, string methodName, params object?[] args)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.SendCoreAsync(methodName, args, default);
	}

	/// <inheritdoc cref="HubConnectionExtensions.SendAsync(HubConnection, string, object?, object?, CancellationToken)"/>/>
	public static Task SendAsync(this IHubActions hubConnection, string methodName, CancellationToken cancellationToken, params object?[] args)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.SendCoreAsync(methodName, args, cancellationToken);
	}
}
