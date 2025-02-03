using System.Collections.Concurrent;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Exposes methods for interacting with a SignalR hub connection
/// but actual connecting and disconnecting is managed by the parent adapter.
/// </summary>
public class HubAdapter(IHubAdapter parent, Action<HubAdapter> onDispose) : IHubAdapterNode
{
	#region Spawn Tracking
	private readonly HubAdapterSpawnTracker _spawnTracker;

	/// <summary>
	/// Spawns a new instance of a <see cref="HubAdapter"/>.
	/// </summary>
	public HubAdapter Spawn() => _spawnTracker.Spawn();

	/// <inheritdoc />
	IHubAdapterNode ISpawn<IHubAdapterNode>.Spawn() => _spawnTracker.Spawn();
	#endregion

	private IHubAdapter? _parent = parent ?? throw new ArgumentNullException(nameof(parent));
	private Action<HubAdapter>? _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
	private readonly CancellationTokenSource _cts = new();

	// We'll need to synchronize the dictionary to avoid leaks so a ConcurrentDictionary won't help here.
	private readonly Lock _sync = new();
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
			lock (_sync)
			{
				registry.Remove(s);
				if (registry.Count == 0)
					_subscriptions.Remove(methodName);
			}
		}, sub);

		// Add the subscription to the registry.
		lock (_sync)
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

	private IHubAdapter Parent
	{
		get
		{
			var c = _parent;
			ObjectDisposedException.ThrowIf(c is null, this);
			return c;
		}
	}

	public void Dispose()
	{
		var p = Interlocked.Exchange(ref _parent, null);
		if (p is null) return;
		var d = Interlocked.Exchange(ref _onDispose, null);
		Debug.Assert(d is not null);
		_cts.Cancel();

		// First cleanup all the spawned adapters.
		_spawnTracker.Dispose();

		// Cleanup all the local subscriptions.
		foreach (var sub in _subscriptions.Values.SelectMany(static s => s))
		{
			// Just dispose the underlying subscription.
			// Don't allow exceptions during dispose.
			try { sub.Sub.Dispose(); }
			catch { }
		}

		_subscriptions.Clear();
		d.Invoke(this);
	}

	/// <inheritdoc />
	public Task SendCoreAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
		=> Parent.SendCoreAsync(methodName, args, cancellationToken);

	/// <inheritdoc />
	public Task<ChannelReader<object?>> StreamAsChannelCoreAsync(string methodName, Type returnType, object?[] args, CancellationToken cancellationToken = default)
		=> Parent.StreamAsChannelCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(string methodName, object?[] args, CancellationToken cancellationToken = default)
		=> Parent.StreamAsyncCore<TResult>(methodName, args, cancellationToken);

	/// <inheritdoc />
	public Task<object?> InvokeCoreAsync(string methodName, Type returnType, object?[] args, CancellationToken cancellationToken = default)
		=> Parent.InvokeCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task<object?>> handler, object state)
		=> Subscribe(methodName, Parent.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state)
		=> Subscribe(methodName, Parent.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public void Remove(string methodName)
		=> Parent.Remove(methodName);
}