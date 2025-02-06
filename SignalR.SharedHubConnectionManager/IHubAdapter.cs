namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Represents all the <see cref="HubConnection"/> actions other than starting, stopping and disposal.
/// </summary>
public interface IHubAdapter
{
	// Only the necessary methods for communicating with the hub are exposed.

	/// <inheritdoc cref="HubConnection.SendCoreAsync(string, object?[], CancellationToken)"/>
	Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc cref="HubConnection.InvokeCoreAsync(string, Type, object?[], CancellationToken)"/>
	Task<object?> InvokeCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc cref="HubConnection.StreamAsChannelCoreAsync(string, Type, object?[], CancellationToken)"/>
	Task<ChannelReader<object?>> StreamAsChannelCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc cref="HubConnection.StreamAsyncCore{TResult}(string, object?[], CancellationToken)"/>
	IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc cref="HubConnection.On(string, Type[], Func{object?[], object, Task{object?}}, object)"/>
	IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task<object?>> handler, object state);

	/// <inheritdoc cref="HubConnection.On(string, Type[], Func{object?[], object, Task}, object)"/>
	IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task> handler, object state);

	/// <inheritdoc cref="HubConnection.Remove(string)"/>
	void Remove(string methodName);
}

/// <inheritdoc />
public interface IHubAdapterNode : IHubAdapter, ISpawn<IHubAdapterNode>, IAsyncDisposable;