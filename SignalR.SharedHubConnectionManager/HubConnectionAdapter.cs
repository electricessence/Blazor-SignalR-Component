
namespace SignalR.SharedHubConnectionManager;

/// <summary>
/// A wrapper around <see cref="HubConnection"/> that implements <see cref="IHubAdapter"/>.
/// </summary>
public class HubConnectionAdapter(HubConnection hubConnection) : IHubAdapter
{
	private readonly HubConnection _hubConnection
		= hubConnection ?? throw new ArgumentNullException(nameof(hubConnection));

	/// <inheritdoc />
	public ValueTask DisposeAsync()
		=> _hubConnection.DisposeAsync();

	/// <inheritdoc />
	public Task<object?> InvokeCoreAsync(string methodName, Type returnType, object?[] args, CancellationToken cancellationToken = default)
		=> _hubConnection.InvokeCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task<object?>> handler, object state)
		=> _hubConnection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public IDisposable On(string methodName, Type[] parameterTypes, Func<object?[], object, Task> handler, object state)
		=> _hubConnection.On(methodName, parameterTypes, handler, state);

	/// <inheritdoc />
	public void Remove(string methodName)
		=> _hubConnection.Remove(methodName);

	/// <inheritdoc />
	public Task SendCoreAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
		=> _hubConnection.SendCoreAsync(methodName, args, cancellationToken);

	/// <inheritdoc />
	public Task<ChannelReader<object?>> StreamAsChannelCoreAsync(string methodName, Type returnType, object?[] args, CancellationToken cancellationToken = default)
		=> _hubConnection.StreamAsChannelCoreAsync(methodName, returnType, args, cancellationToken);

	/// <inheritdoc />
	public IAsyncEnumerable<TResult> StreamAsyncCore<TResult>(string methodName, object?[] args, CancellationToken cancellationToken = default)
		=> _hubConnection.StreamAsyncCore<TResult>(methodName, args, cancellationToken);
}