namespace Open.SignalR.SharedClient;

/// <summary>
/// Represents all the <see cref="HubConnection"/> actions other than starting, stopping and disposal.
/// </summary>
public interface IHubAdapter : IHubSubscriber, IHubActions, IDisposable
{
	/// <summary>
	/// Invokes the <paramref name="handler"/> when the connection is available or re-established.
	/// </summary>
	/// <remarks>Ensures a connnection has made before invoking the action.</remarks>
	IDisposable OnConnected(Func<Task> handler);
}

public static partial class HubAdapterExtensions
{
	/// <inheritdoc cref="IHubAdapter.OnConnected(Func{Task})"/>
	public static IDisposable OnConnected(this IHubAdapter hubAdapter, Action handler)
		=> hubAdapter.OnConnected(() =>
		{
			handler();
			return Task.CompletedTask;
		});

	/// <inheritdoc cref="IHubAdapter.OnConnected(Func{Task})"/>
	public static IDisposable OnConnected(this IHubAdapter hubAdapter, Func<IHubAdapter, Task> action)
		=> hubAdapter.OnConnected(() => action(hubAdapter));

	/// <inheritdoc cref="IHubAdapter.OnConnected(Func{Task})"/>
	public static IDisposable OnConnected(this IHubAdapter hubAdapter, Action<IHubAdapter> action)
		=> hubAdapter.OnConnected(() => action(hubAdapter));

	/// <summary>
	/// Invokes the <paramref name="handler"/> when the connection is available.
	/// Only invokes the action once and then disposes the created listener.
	/// </summary>
	/// <inheritdoc cref="IHubAdapter.OnConnected(Func{Task})"/>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1229:Use async/await when necessary", Justification = "Not needed.")]
	public static IDisposable OnceConnected(this IHubAdapter hubAdapter, Func<Task> handler)
	{
		Task? task = null;
		Lock sync = new();
		IDisposable? d = null;
		d = hubAdapter.OnConnected(() =>
		{
			Debug.Assert(d is not null);
			if (task is not null) return task;
			try
			{
				lock (sync)
				{
					if (task is not null) return task;
					task = handler();
				}

				return task;
			}
			finally
			{
				d.Dispose();
			}
		});

		return d;
	}

	/// <inheritdoc cref="OnceConnected(IHubAdapter, Func{Task})"/>
	public static IDisposable OnceConnected(this IHubAdapter hubAdapter, Action handler)
		=> hubAdapter.OnceConnected(() =>
		{
			handler();
			return Task.CompletedTask;
		});

	/// <inheritdoc cref="OnceConnected(IHubAdapter, Func{Task})"/>
	public static IDisposable OnceConnected(this IHubAdapter hubAdapter, Func<IHubAdapter, Task> action)
		=> hubAdapter.OnceConnected(() => action(hubAdapter));

	/// <inheritdoc cref="OnceConnected(IHubAdapter, Func{Task})"/>
	public static IDisposable OnceConnected(this IHubAdapter hubAdapter, Action<IHubAdapter> action)
		=> hubAdapter.OnceConnected(() => action(hubAdapter));
}
