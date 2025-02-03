namespace BlazorWeb;

public interface ISpawn<T>
	where T : notnull
{
	T Spawn();
}
