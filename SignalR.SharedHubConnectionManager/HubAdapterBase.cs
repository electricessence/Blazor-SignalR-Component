using System.Collections.Concurrent;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A base class for implementing a spawning <see cref="IHubAdapter"/>.
/// </summary>
public abstract class HubAdapterBase
	: SpawnableBase<HubAdapterNode>, IHubConnectAdapter, IAsyncDisposable
{
	/// <summary>
	/// The constructor for <see cref="HubAdapterBase"/>.
	/// Requires initialization of the factory for spawning <see cref="HubAdapterNode"/> instances.
	/// </summary>
	protected HubAdapterBase()
		=> InitFactory(onDispose => new HubAdapterNode(this, onDispose));

	/// <inheritdoc />
	public abstract Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public abstract Task<object?> InvokeCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public abstract Task<ChannelReader<object?>> StreamAsChannelCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public abstract IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default);

	/// <inheritdoc />
	public abstract IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task<object?>> handler, object state);

	/// <inheritdoc />
	public abstract IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task> handler, object state);

	/// <inheritdoc />
	public abstract void Remove(string methodName);

	/// <inheritdoc cref="IHubConnectAdapter.StartAsync(CancellationToken)" />
	public abstract Task StartAsync(CancellationToken cancellationToken = default);

	/// <inheritdoc cref="IHubConnectAdapter.StartTask" />
	public Task? StartTask { get; protected set; }

	private readonly ConcurrentDictionary<Action<IHubAdapter>, Action<IHubAdapter>> _connectedEventHandlers = new();

	/// <inheritdoc />
	protected override ValueTask OnDisposeAsync()
	{
		_connectedEventHandlers.Clear();
		return base.OnDisposeAsync();
	}

	/// <summary>
	/// The core event for when the connection is established.
	/// </summary>
	protected abstract event Action<IHubAdapter>? ConnectedCore;

	/// <inheritdoc cref="IHubConnectAdapter.Connected" />
	public event Action<IHubAdapter>? Connected
	{
		add
		{
			AssertIsAlive();
			ArgumentNullException.ThrowIfNull(value, nameof(value));

			void LocalHandler(IHubAdapter adapter)
			{
				// This ensures the event is fired only once after adding.
				if (!_connectedEventHandlers.TryRemove(value, out var handler))
					return;

				ConnectedCore -= handler;
				ConnectedCore += value;
				value(adapter);
			}

			// Double adding? Return.
			if (!_connectedEventHandlers.TryAdd(value, LocalHandler))
				return;

			ConnectedCore += LocalHandler;

			var startTask = StartTask;
			if (startTask is null)
				return;

			// In flight, or completed, it doesn't matter.
			// The LocalHanlder will manage calling the value only once.
			startTask.ContinueWith(
				_ => LocalHandler(this),
				TaskContinuationOptions.OnlyOnRanToCompletion
				| TaskContinuationOptions.ExecuteSynchronously);
		}

		remove
		{
			ArgumentNullException.ThrowIfNull(value, nameof(value));
			if (_connectedEventHandlers.TryRemove(value, out var handler))
				ConnectedCore -= handler;
			ConnectedCore -= value;
		}
	}
}