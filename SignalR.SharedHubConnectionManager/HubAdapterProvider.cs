﻿using System.Collections.Concurrent;

namespace Open.SignalR.SharedHubConnection;

/// <summary>
/// Manages persistent <see cref="HubConnection"/> instances
/// and produces <see cref="IHubAdapter"/> instances for hub paths.
/// </summary>
public class HubAdapterProvider : IHubAdapterProvider, IAsyncDisposable
{
	private ConcurrentDictionary<string, HubConnection>? _registry = new();

	/// <summary>
	/// Disposes of this and all managed <see cref="HubConnection"/> instances.
	/// </summary>
	public ValueTask DisposeAsync()
	{
		var conn = _registry;
		if(conn is null) return default;
		conn = Interlocked.CompareExchange(ref _registry, null, conn);
		return conn is null ? default : DisposeAsync(conn.Values);
	}

	private static async ValueTask DisposeAsync(IEnumerable<IAsyncDisposable> asyncDisposables)
	{
		foreach (var c in asyncDisposables)
		{
			try
			{ await c.DisposeAsync(); }
			catch
			{ /* Ignore */ }
		}
	}

	/// <inheritdoc />
	public IHubAdapter GetAdapterFor(string hub)
	{
		ArgumentNullException.ThrowIfNull(hub);
		ArgumentException.ThrowIfNullOrWhiteSpace(hub);
		var reg = _registry;
		ObjectDisposedException.ThrowIf(reg is null, nameof(HubAdapterProvider));

		var hubConnection = reg.GetOrAdd(hub, k =>
		{
			var builder = new HubConnectionBuilder();
			return builder
				.WithUrl(k)
				.WithAutomaticReconnect()
				.Build();
		});

		return new HubAdapter(hubConnection);
	}
}
