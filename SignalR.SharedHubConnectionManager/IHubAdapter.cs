﻿namespace SignalR.SharedHubConnectionManager;

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

/// <summary>
/// An extended <see cref="IHubAdapter"/> that can ensure a connection has started and can listen for when a connection is established/reconnected.
/// </summary>
public interface IHubConnectAdapter : IHubAdapter
{
	/// <remarks>
	/// For a shared connection, this simply ensures a connection is open.
	/// A connection is not automatically started when created and requires at least one call to this method to start.
	/// 'On' and 'Remove' methods can be called before starting the connection.
	/// </remarks>
	/// <inheritdoc cref="HubConnection.StartAsync(CancellationToken)"/>
	Task StartAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Event is fired when the connection is established either after starting or after a reconnection.
	/// </summary>
	/// <remarks>Event is fired for new subscribers if the connection is already connected.</remarks>
	event Action<IHubAdapter> Connected;

	/// <summary>
	/// The actual task that starts the connection.
	/// </summary>
	/// <remarks>Will be null if the connection has yet to start or has been closed.</remarks>
	public Task? StartTask { get; }
}

/// <inheritdoc />
public interface IHubAdapterNode : IHubConnectAdapter, ISpawn<IHubAdapterNode>, IAsyncDisposable;

public static partial class HubAdapterExtensions
{
	/// <summary>
	/// Sets up a handler to be invoked upon verfiying a connection and then ensure the connection has been started.
	/// </summary>
	public static Task Start(this IHubConnectAdapter adapter, Action<IHubAdapter> onConnected, CancellationToken cancellationToken = default)
	{
		adapter.Connected += onConnected;
		return adapter.StartAsync(cancellationToken);
	}
}