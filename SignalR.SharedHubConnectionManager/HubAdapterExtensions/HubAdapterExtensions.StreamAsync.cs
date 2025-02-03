namespace SignalR.SharedHubConnectionManager;

public static partial class HubAdapterExtensions
{
	/// <inheritdoc cref="HubConnectionExtensions.StreamAsChannelCoreAsync{TResult}(HubConnection, string, object?[], CancellationToken)"/>/>
	public static IAsyncEnumerable<TResult> StreamAsync<TResult>(this IHubAdapter hubConnection, string methodName, object?[] args, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.StreamAsyncCore<TResult>(methodName, args, cancellationToken);
	}

	/// <inheritdoc cref="HubConnectionExtensions.StreamAsChannelCoreAsync{TResult}(HubConnection, string, object?[], CancellationToken)"/>/>
	public static IAsyncEnumerable<TResult> StreamAsync<TResult>(this IHubAdapter hubConnection, string methodName, params object?[] args)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.StreamAsyncCore<TResult>(methodName, args);
	}

	/// <inheritdoc cref="HubConnectionExtensions.StreamAsChannelCoreAsync{TResult}(HubConnection, string, object?[], CancellationToken)"/>/>
	public static IAsyncEnumerable<TResult> StreamAsync<TResult>(this IHubAdapter hubConnection, string methodName, CancellationToken cancellationToken, params object?[] args)
	{
		ArgumentNullException.ThrowIfNull(hubConnection);
		return hubConnection.StreamAsyncCore<TResult>(methodName, args, cancellationToken);
	}
}
