namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A base class for implementing a spawning <see cref="IHubAdapter"/>.
/// </summary>
public abstract class HubAdapterBase
	: SpawnableBase<HubAdapterNode>, IHubAdapter, IAsyncDisposable
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
}