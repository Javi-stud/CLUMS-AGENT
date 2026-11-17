using LabMonitorAgent;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Server address and key come from appsettings.json (address) and user-secrets / environment (key).
builder.Services.AddOptions<BackendOptions>()
    .Bind(builder.Configuration.GetSection(BackendOptions.SectionName))
    .Validate(
        o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out Uri? u) &&
             (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps),
        "Backend:BaseUrl must be an http(s) address, e.g. http://192.168.1.10:8000")
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.AgentApiKey),
        "Backend:AgentApiKey is not set. It must equal AGENT_API_KEY in the server's .env " +
        "(dev: dotnet user-secrets set \"Backend:AgentApiKey\" \"<key>\"; deployed: environment variable Backend__AgentApiKey)")
    .ValidateOnStart();

// One long-lived HttpClient, refreshed every 5 minutes so a changed server IP is picked up.
builder.Services.AddSingleton(sp =>
{
    BackendOptions options = sp.GetRequiredService<IOptions<BackendOptions>>().Value;

    HttpClient http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
    {
        BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(Math.Max(1, options.RequestTimeoutSeconds))
    };
    http.DefaultRequestHeaders.Add("X-Agent-Key", options.AgentApiKey);
    return http;
});

builder.Services.AddSingleton<ComputerIdentity>();
builder.Services.AddSingleton<NetworkInfo>();
builder.Services.AddSingleton<ComputerInfo>();
builder.Services.AddSingleton<AgentLogWriter>();
builder.Services.AddSingleton<EventQueue>();
builder.Services.AddSingleton<BackendClient>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();