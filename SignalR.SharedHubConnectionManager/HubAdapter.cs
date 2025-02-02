using System.Collections.Concurrent;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Exposes methods for interacting with a SignalR hub connection
/// but actual connecting and disconnecting is managed by the parent adapter.
/// </summary>
public class HubAdapter : IHubAdapter
{
	private IHubAdapter? _parent;
	private readonly CancellationTokenSource _cts = new();

	private readonly ConcurrentDictionary<string, HashSet<Subscription>> _subscriptions = [];

	// Looks will allow for cleanup of the subscriptions.
	private void OnRemoved(string methodName, HashSet<Subscription> subscriptions)
	{

	}

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
		HashSet<Subscription>? parent = null;
		Subscription? s = null;

		s = new Subscription(() =>
		{
			Debug.Assert(parent is not null);
			Debug.Assert(s is not null);

			lock (parent) parent.Remove(s);

			OnRemoved(methodName, parent);
		}, sub);

		parent = _subscriptions.AddOrUpdate(
			methodName,
			_ => [s],
			(_, subs) =>
			{
				lock (subs) subs.Add(s);
				return subs;
			});

		return s;
	}

	internal HubAdapter(IHubAdapter parent)
	{
		_parent = parent ?? throw new ArgumentNullException(nameof(parent));
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

	public ValueTask DisposeAsync()
	{
		var c = Interlocked.Exchange(ref _parent, null);
		if (c is null) return ValueTask.CompletedTask;

		_cts.Cancel();

		foreach (var sub in _subscriptions.Values.SelectMany(static s => s))
		{
			// Just dispose the underlying subscription.
			// Don't allow exceptions during dispose.
			try { sub.Sub.Dispose(); }
			catch { }
		}

		_subscriptions.Clear();

		return ValueTask.CompletedTask;
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