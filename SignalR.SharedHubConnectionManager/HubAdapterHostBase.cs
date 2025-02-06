using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// Acts as a wrapper around a <see cref="IHubAdapter"/> that manages disposal.
/// </summary>
public abstract class HubAdapterHostBase(IHubAdapter host) : IHubAdapter
{
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



	public ValueTask DisposeAsync()
	{
		OnDispose();

		return host.DisposeAsync();
	}

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

	/// <inheritdoc />
	public abstract IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task<object?>> handler, object state);

	/// <inheritdoc />
	public abstract IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state);

	/// <inheritdoc />
	public abstract void Remove(string methodName);
}
