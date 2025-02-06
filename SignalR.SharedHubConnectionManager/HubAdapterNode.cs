using System.Diagnostics.Contracts;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Exposes methods for interacting with a SignalR hub connection
/// but actual connecting and disconnecting is managed by the parent adapter.
/// </summary>
public class HubAdapterNode(
	IHubAdapter host, Action<HubAdapterNode> onDispose)
	: SpawnableBase<HubAdapterNode>, IHubAdapterNode
{
	#region Host Aapter
	private IHubAdapter? _host = host ?? throw new ArgumentNullException(nameof(host));

	private IHubAdapter Host
	{
		get
		{
			var c = _host;
			ObjectDisposedException.ThrowIf(c is null, this);
			return c;
		}
	}
	#endregion

	private Action<HubAdapterNode>? _onDispose
		= onDispose ?? throw new ArgumentNullException(nameof(onDispose));

	/// <inheritdoc />
	public Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
		// No cancellation managment needed here. Fire and forget.
		=> host.SendCoreAsync(methodName, args, cancellationToken);

	/// <inheritdoc />
	public Task<object?> InvokeCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
		=> host.InvokeCoreAsync(methodName, returnType, args, cancellationToken);

	#region CancellationToken Management
	private readonly Lock _ctsSync = new();
	private HashSet<CancellationTokenSource>? _ctsInstances = [];
	private readonly CancellationTokenSource _cts = new();

	private CancellationTokenSource AddCtsInstance(CancellationToken incommingToken)
	{
		var instances = _ctsInstances;
		ObjectDisposedException.ThrowIf(instances is null, this);

		lock (_ctsSync)
		{
			instances = _ctsInstances;
			ObjectDisposedException.ThrowIf(instances is null, this);

			var cts = CancellationTokenSource.CreateLinkedTokenSource(incommingToken, _cts.Token);
			instances.Add(cts);
			return cts;
		}
	}

	private void RemoveCtsInstance(CancellationTokenSource cts)
	{
		if (_ctsInstances is null)
			return;

		lock (_ctsSync)
		{
			_ctsInstances?.Remove(cts);
		}
	}
	#endregion

	#region Potentially Long-Running Cancellable Methods
	/// <inheritdoc />
	public Task<ChannelReader<object?>> StreamAsChannelCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
		ArgumentNullException.ThrowIfNull(returnType);
		ArgumentNullException.ThrowIfNull(args);
		Contract.EndContractBlock();

		// If the token is cancellable, then use the local method.
		// Otherwise just use the underlying CanellationToken.
		return cancellationToken.CanBeCanceled
			? StreamAsChannelCoreAsync()
			: host.StreamAsChannelCoreAsync(methodName, returnType, args, _cts.Token);

		async Task<ChannelReader<object?>> StreamAsChannelCoreAsync()
		{
			using var cts = AddCtsInstance(cancellationToken);
			ChannelReader<object?>? reader = null;
			try
			{
				reader = await host
					.StreamAsChannelCoreAsync(methodName, returnType, args, cts.Token)
					.ConfigureAwait(false);
			}
			catch
			{
				RemoveCtsInstance(cts);
				throw;
			}

			// Await completion and remove the cts instance.
			_ = reader.Completion
				.ContinueWith(_ => RemoveCtsInstance(cts), CancellationToken.None);

			return reader;
		}
	}

	/// <inheritdoc />
	public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
		ArgumentNullException.ThrowIfNull(args);
		Contract.EndContractBlock();

		// If the token is cancellable, then use the local method.
		// Otherwise just use the underlying CanellationToken.
		return cancellationToken.CanBeCanceled
			? StreamAsyncCore()
			: host.StreamAsyncCore<TResult>(methodName, args, _cts.Token);

		async IAsyncEnumerable<TResult> StreamAsyncCore()
		{
			using var cts = AddCtsInstance(cancellationToken);
			try
			{
				await foreach (var e in host
					.StreamAsyncCore<TResult>(methodName, args, cts.Token)
					.ConfigureAwait(false))
				{
					yield return e;
				}
			}
			finally
			{
				RemoveCtsInstance(cts);
			}
		}
	}
	#endregion

	#region Subscription Managment
	// We'll need to synchronize the dictionary to avoid leaks so a ConcurrentDictionary won't help here.
	private readonly Lock _subSync = new();
	private readonly Dictionary<string, HashSet<Subscription>> _subscriptions = [];

	private sealed class Subscription(Action onDispose, IDisposable sub) : IDisposable
	{
		private Action? _onDispose = onDispose;

		internal readonly IDisposable Sub = sub;

		public void Dispose()
		{
			// Step 1) Avoid double removal.
			var d = Interlocked.Exchange(ref _onDispose, null);

			// Step 2) Dispose the subscription.
			Sub.Dispose();

			// Step 3) Remove the subscription from the parent.
			d?.Invoke();
		}
	}

	/// <summary>
	/// Produces an <see cref="IDisposable"/> that will remove the subscription when disposed
	/// or will unsubscribe if this instance is disposed.
	/// </summary>
	private Subscription Subscribe(string methodName, IDisposable sub)
	{
		HashSet<Subscription>? registry = null;
		Subscription? s = null;

		// Setup the subscription.
		s = new Subscription(() =>
		{
			// Setup the removal action.
			Debug.Assert(registry is not null);
			Debug.Assert(s is not null);

			// Removal may require a cleanup of the registry so we need to lock everything.
			lock (_subSync)
			{
				registry.Remove(s);
				if (registry.Count == 0)
					_subscriptions.Remove(methodName);
			}
		}, sub);

		// Add the subscription to the registry.
		lock (_subSync)
		{
			// Keep the lock to avoid stray removals.
			if (_subscriptions.TryGetValue(methodName, out registry))
			{
				registry.Add(s);
			}
			else
			{
				_subscriptions[methodName] = registry = [s];
			}
		}

		// Return the subscription.
		return s;
	}
	#endregion

	#region Subscribable Methods
	/// <inheritdoc />
	public IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task<object?>> handler, object state)
		=> Subscribe(methodName, Host.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state)
		=> Subscribe(methodName, Host.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public void Remove(string methodName)
	{
		if (!_subscriptions.TryGetValue(methodName, out var subs))
			return;

		lock (_subSync)
		{
			if (!_subscriptions.TryGetValue(methodName, out subs))
				return;

			_subscriptions.Remove(methodName);
		}

		foreach (var sub in subs)
		{
			sub.Sub.Dispose();
		}
	}
	#endregion

	/// <inheritdoc />
	protected async override ValueTask OnDisposeAsync()
	{
		// Step 1) Cleanup host.
		lock (_subSync) {
			_host = null;
		}

		// Step 2) Cleanup the _onDispose reference.
		Debug.Assert(_onDispose is not null);
		var d = _onDispose;
		_onDispose = null;

		// Step 3) Pre-cleanup any spawned instances.
		await base.OnDisposeAsync().ConfigureAwait(false);

		// Step 4) Cancel any running async methods.
		using var cts = _cts;
		_cts.Cancel();

		// Step 5) Dispose any remaining CTS instances.
		HashSet<CancellationTokenSource> ctsInstances;
		lock (_ctsSync)
		{
			Debug.Assert(_ctsInstances is not null);
			ctsInstances = _ctsInstances;
			_ctsInstances = null;
		}

		foreach (var ctsi in ctsInstances!)
		{
			ctsi.Dispose();
		}

		// Step 6) Cleanup any remaining local subscriptions.
		foreach (var sub in _subscriptions.Values.SelectMany(static s => s))
		{
			// Just dispose the underlying subscription.
			// Don't allow exceptions during dispose.
			try { sub.Sub.Dispose(); }
			catch { }
		}

		_subscriptions.Clear();

		// Step 7) Signal to the host that we're done.
		try
		{
			d.Invoke(this);
		}
		catch (Exception ex)
		{
			// We can't throw execptions during disposal,
			// but since it's provided delegate, we need to be sure to discover it.
			Debug.WriteLine(ex);
			Debugger.Break();
		}
	}

	IHubAdapterNode ISpawn<IHubAdapterNode>.Spawn() => Spawn();

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken = default)
		=> Host.StartAsync(cancellationToken);
}