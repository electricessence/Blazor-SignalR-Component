using BlazorWeb.Client.Pages;
using BlazorWeb.Components;
using BlazorWeb.Hubs;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorWeb.Client._Imports).Assembly);

app.MapHub<StreamHub>("/hubs/stream");

await app.StartAsync();

var rnd = new Random();
var streamHubContext = app.Services.GetRequiredService<IHubContext<StreamHub>>();
using var cts = new CancellationTokenSource();
var chaosProducer = Task
    .Run(async () =>
    {
        while(!cts.Token.IsCancellationRequested)
		{
			await Task.Delay(500);
			string message = rnd.Next(0, 1000).ToString();
			await streamHubContext
                .Clients.Group("chaos")
				.SendAsync("OnNext", message);
		}
	}, cts.Token)
    .ContinueWith(_ => { /* Ignore exeptions */});

await app.WaitForShutdownAsync();
cts.Cancel();
await chaosProducer;