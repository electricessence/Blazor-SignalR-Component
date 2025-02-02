using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SignalR.SharedHubConnectionManager;

public class HubConnectionManager : IHubConnectionManager, IAsyncDisposable
{
	// Holder that contains the connection and the active session count.
	private class HubConnectionHolder
	{
		public HubConnection Connection { get; }
		public int RefCount;

		public HubConnectionHolder(HubConnection connection)
		{
			Connection = connection;
			connection.StreamAsChannelAsync
			// Start at 0; each new session will increment.
			RefCount = 0;
		}
	}

	// Dictionary keyed by hub URL. Lazy ensures only one connection is created per hub URL.
	private readonly ConcurrentDictionary<string, Lazy<Task<HubConnectionHolder>>> _holders = new();

	/// <summary>
	/// Gets or creates a session (proxy) for the specified hub URL.
	/// </summary>
	public async Task<IHubSession> GetOrCreateHubSessionAsync(string hubUrl)
	{
		// Retrieve or create the lazy holder for the given hub URL.
		var lazyHolder = _holders.GetOrAdd(hubUrl, url =>
			new Lazy<Task<HubConnectionHolder>>(async () =>
			{
				var connection = await CreateAndStartHubConnectionAsync(url);
				return new HubConnectionHolder(connection);
			})
		);

		var holder = await lazyHolder.Value;
		// Increment the reference count for each new session.
		Interlocked.Increment(ref holder.RefCount);
		return new HubSession(this, hubUrl, holder);
	}

	/// <summary>
	/// Creates and starts a new HubConnection.
	/// </summary>
	private async Task<HubConnection> CreateAndStartHubConnectionAsync(string hubUrl)
	{
		var connection = new HubConnectionBuilder()
			.WithUrl(hubUrl)
			.WithAutomaticReconnect() // Enable automatic reconnect if needed.
			.Build();

		// Optionally, register connection events here.
		connection.Reconnecting += error =>
		{
			// Log or handle reconnection logic.
			return Task.CompletedTask;
		};
		connection.Reconnected += connectionId =>
		{
			// Handle successful reconnection.
			return Task.CompletedTask;
		};
		connection.Closed += async error =>
		{
			// Optionally handle cleanup or retry logic on connection closed.
			await Task.CompletedTask;
		};

		await connection.StartAsync();
		return connection;
	}

	/// <summary>
	/// Releases a session. If no sessions remain for the hub, the connection is disposed.
	/// </summary>
	internal async Task ReleaseSessionAsync(string hubUrl, HubConnectionHolder holder)
	{
		// Decrement the reference count.
		var newCount = Interlocked.Decrement(ref holder.RefCount);
		if (newCount <= 0)
		{
			// Remove the holder from the dictionary.
			_holders.TryRemove(hubUrl, out _);
			// Dispose the underlying connection.
			await holder.Connection.DisposeAsync();
		}
	}

	/// <summary>
	/// Dispose all connections when the manager is disposed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		foreach (var lazyHolder in _holders.Values)
		{
			if (lazyHolder.IsValueCreated)
			{
				var holder = await lazyHolder.Value;
				await holder.Connection.DisposeAsync();
			}
		}
		_holders.Clear();
	}
}
