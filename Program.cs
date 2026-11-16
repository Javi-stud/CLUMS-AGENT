using LabMonitorAgent;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ComputerIdentity>();
builder.Services.AddSingleton<NetworkInfo>();
builder.Services.AddSingleton<ComputerInfo>();
builder.Services.AddSingleton<AgentLogWriter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
