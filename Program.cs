using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting.WindowsServices;
using LabMonitorAgent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CLUMS Lab Monitor Agent";
});
if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.Replace(
        ServiceDescriptor.Singleton<IHostLifetime, ClumsWindowsServiceLifetime>()
    );
}

var backendUrl = builder.Configuration["Backend:BaseUrl"];
var agentApiKey = builder.Configuration["Backend:AgentApiKey"];

if (string.IsNullOrWhiteSpace(backendUrl))
    throw new InvalidOperationException("Backend URL is missing.");

if (string.IsNullOrWhiteSpace(agentApiKey))
    throw new InvalidOperationException("Agent API key is missing.");

builder.Services.AddHttpClient("CLUMS", client =>
{
    client.BaseAddress = new Uri(backendUrl);
    client.DefaultRequestHeaders.Add("X-Agent-Key", agentApiKey);
});

builder.Services.AddSingleton<ComputerIdentity>();
builder.Services.AddSingleton<NetworkInfo>();
builder.Services.AddSingleton<WindowsSessionMonitor>();
builder.Services.AddSingleton<AgentEventQueue>();
builder.Services.AddSingleton<AgentEventSender>();
builder.Services.AddSingleton<ComputerInfo>();
builder.Services.AddSingleton<AgentLogWriter>();
builder.Services.AddSingleton<BackendMonitor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();