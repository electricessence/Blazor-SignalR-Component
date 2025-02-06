
namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A wrapper around <see cref="HubConnection"/> that implements <see cref="IHubAdapter"/>.
/// </summary>
public class HubConnectionAdapter(HubConnection hubConnection) : IHubAdapter
{
	/// <summary>
	/// The <see cref="HubConnection"/> instance.
	/// </summary>
	public HubConnection Connection { get; }
		= hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));

	/// <inheritdoc />
	public ValueTask DisposeAsync()
		=> Connection.DisposeAsync();

	/// <inheritdoc />
	public Task SendCoreAsync(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.SendCoreAsync(methodName, args, cancellationToken);

	/// <inheritdoc />
	public Task<object?> InvokeCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.InvokeCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public Task<ChannelReader<object?>> StreamAsChannelCoreAsync(
		string methodName, Type returnType, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.StreamAsChannelCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(
		string methodName, object?[] args,
		CancellationToken cancellationToken = default)
		=> Connection.StreamAsyncCore<TResult>(methodName, args, cancellationToken);

	/// <inheritdoc />
	public IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task<object?>> handler, object state)
		=> Connection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public IDisposable On(
		string methodName, Type[] parameterTypes,
		Func<object?[], object, Task> handler, object state)
		=> Connection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public void Remove(string methodName)
		=> Connection.Remove(methodName);
}