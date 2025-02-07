namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A wrapper around <see cref="HubConnection"/> that implements <see cref="IHubAdapter"/>.
/// </summary>
public class HubConnectionAdapter : HubAdapterBase
{
	/// <summary>
	/// The <see cref="HubConnection"/> instance.
	/// </summary>
	protected HubConnection Connection { get; }

	readonly Lock _startLock = new();
	Task? _startAsync;
	Task? _reconnectingAsync;

	/// <summary>
	/// Initializes a new instance of <see cref="HubConnectionAdapter"/>.
	/// </summary>
	public HubConnectionAdapter(HubConnection hubConnection)
	{
		Connection = hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));

		hubConnection.Closed += OnClosed;
		hubConnection.Reconnecting += OnReconnecting;
		hubConnection.Reconnected += OnReconnected;
	}

	private Task OnClosed(Exception? _)
	{
		lock (_startLock)
			_startAsync = null;

		return Task.CompletedTask;
	}

	private Task OnReconnecting(Exception? _)
	{
		var startAsync = _startAsync;
		if (startAsync is not null)
			return Task.CompletedTask;

		lock (_startLock)
		{
			startAsync = _startAsync;
			if (startAsync is not null)
				return Task.CompletedTask;

			// If we got here, we have not yet set the reconnecting task.
			// Create an unstarted task that if picked up will eventually start when finally connected.
			_startAsync = _reconnectingAsync = new Task(static () => { });
		}

		return Task.CompletedTask;
	}

	private Task OnReconnected(string? _)
	{
		Task? reconnectingAsync;
		lock (_startLock)
		{
			reconnectingAsync = _reconnectingAsync;
			_reconnectingAsync = null;
			_startAsync = Task.CompletedTask;
		}

		reconnectingAsync?.Start();
		return Task.CompletedTask;
	}

	/// <summary>
	/// Ensures the connection is started.
	/// </summary>
	protected Task EnsureStart(CancellationToken cancellationToken)
	{
		AssertIsAlive();
		var startAsync = _startAsync;
		if (startAsync is not null)
			return startAsync;

		lock (_startLock)
		{
			startAsync = _startAsync;
			if (startAsync is not null)
				return startAsync;

			// Start the connection and return the task.
			_startAsync = startAsync = Connection
				.StartAsync(cancellationToken);
		}

		// If any errors or cancellations occur, clear the start task.
		startAsync.ContinueWith(t =>
		{
			if (_startAsync != t) return;
			lock (_startLock)
			{
				if (_startAsync != t) return;
				_startAsync = null;
			}
		}, TaskContinuationOptions.NotOnRanToCompletion
		 | TaskContinuationOptions.ExecuteSynchronously);

		return startAsync;
	}

	/// <inheritdoc cref="IHubConnectAdapter.StartAsync(CancellationToken)" />
	public override Task StartAsync(CancellationToken cancellationToken = default)
		=> EnsureStart(cancellationToken);

	/// <inheritdoc />
	protected override event Action<IHubAdapter>? ConnectedCore;

	/// <summary>
	/// Disposes of any spawned instances and then the underyling connection.
	/// </summary>
	protected override async ValueTask OnDisposeAsync()
	{
		// Clear any connected events.
		ConnectedCore = null;

		Connection.Closed -= OnClosed;
		Connection.Reconnecting -= OnReconnecting;
		Connection.Reconnected -= OnReconnected;

		await base.OnDisposeAsync();
		await Connection.DisposeAsync();
	}

	#region Cancellable Methods
	/// <inheritdoc />
	public override async Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
		=> await Connection.SendCoreAsync(methodName, args, cancellationToken);

	/// <inheritdoc />
	public override Task<object?> InvokeCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.InvokeCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public override Task<ChannelReader<object?>> StreamAsChannelCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.StreamAsChannelCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public override IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.StreamAsyncCore<TResult>(methodName, args, cancellationToken);
	#endregion

	/// <inheritdoc />
	public override IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task<object?>> handler, object state)
		=> Connection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public override IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task> handler, object state)
		=> Connection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public override void Remove(string methodName)
		=> Connection.Remove(methodName);
}