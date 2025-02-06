using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Exposes methods for interacting with a SignalR hub connection
/// but actual connecting and disconnecting is managed by the parent adapter.
/// </summary>
public abstract class HubAdapterNode(IHubAdapter host)
	: SpawnableBase<HubAdapter>, IHubAdapterNode
{
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

	#region Cancellable Methods
	/// <inheritdoc />
	public Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
		ArgumentNullException.ThrowIfNull(args);
		Contract.EndContractBlock();

		// If the token is cancellable, then use the local method.
		// Otherwise just use the underlying CanellationToken.
		return cancellationToken.CanBeCanceled
			? SendCoreAsync()
			: host.SendCoreAsync(methodName, args, _cts.Token);

		async Task SendCoreAsync()
		{
			using var cts = AddCtsInstance(cancellationToken);
			try
			{
				await host
					.SendCoreAsync(methodName, args, cts.Token)
					.ConfigureAwait(false);
			}
			finally
			{
				RemoveCtsInstance(cts);
			}
		}
	}

	/// <inheritdoc />
	public Task<object?> InvokeCoreAsync(
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
			? InvokeCoreAsync()
			: host.InvokeCoreAsync(methodName, returnType, args, _cts.Token);

		async Task<object?> InvokeCoreAsync()
		{
			using var cts = AddCtsInstance(cancellationToken);
			try
			{
				return await host
					.InvokeCoreAsync(methodName, returnType, args, cancellationToken)
					.ConfigureAwait(false);
			}
			finally
			{
				RemoveCtsInstance(cts);
			}
		}
	}

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
	public async IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
		ArgumentNullException.ThrowIfNull(args);
		Contract.EndContractBlock();

		if (!cancellationToken.CanBeCanceled)
		{
			// Just a passthrough.
			await foreach (var item in host
				.StreamAsyncCore<TResult>(methodName, args, _cts.Token)
				.ConfigureAwait(false))
			{
				yield return item;
			}
		}
		else
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

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task<object?>> handler, object state)
		=> Subscribe(methodName, Host.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state)
		=> Subscribe(methodName, Host.On(methodName, parameterTypes, handler, state));

	/// <inheritdoc />
	public void Remove(string methodName)
		=> Host.Remove(methodName);

	/// <inheritdoc />
	public void Dispose()
	{
		// Step 1) Check if we're already disposed.
		var p = Interlocked.Exchange(ref _host, null);
		if (p is null) return;

		// Step 2) Cleanup the _onDispose reference.
		var d = Interlocked.Exchange(ref _onDispose, null);
		Debug.Assert(d is not null);

		// Step 3) Cleanup all the spawned adapters.
		DisposeSpawned();

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
		d.Invoke(this);
	}
}