namespace SignalR.SharedHubConnectionManager;

public interface IHubConnectionManager
{
	/// <summary>
	/// Gets or creates a shared session (proxy) for the specified hub URL.
	/// </summary>
	Task<IHubAdapter> GetOrCreateHubSessionAsync(string hubUrl);
}