using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using System.ServiceProcess;
using System.Collections.Concurrent;

namespace LabMonitorAgent;

public class ClumsWindowsServiceLifetime : WindowsServiceLifetime
{
    private readonly WindowsSessionMonitor sessionMonitor;
private readonly AgentEventQueue eventQueue;

private readonly ConcurrentDictionary<int, string> sessionUsers = new();
    public ClumsWindowsServiceLifetime(
    IHostEnvironment environment,
    IHostApplicationLifetime applicationLifetime,
    ILoggerFactory loggerFactory,
    IOptions<HostOptions> optionsAccessor,
    IOptions<WindowsServiceLifetimeOptions> windowsServiceOptionsAccessor,
    WindowsSessionMonitor sessionMonitor,
    AgentEventQueue eventQueue
) : base(
        environment,
        applicationLifetime,
        loggerFactory,
        optionsAccessor,
        windowsServiceOptionsAccessor
   )
{
    this.sessionMonitor = sessionMonitor;
    this.eventQueue = eventQueue;

    CanHandleSessionChangeEvent = true;
}
    protected override void OnSessionChange(
    SessionChangeDescription changeDescription
)
{
    base.OnSessionChange(changeDescription);

    int sessionId = changeDescription.SessionId;

    if (changeDescription.Reason == SessionChangeReason.SessionLogon)
    {
        string? username = sessionMonitor.GetSessionUsername(sessionId);

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        sessionUsers[sessionId] = username;

        eventQueue.Enqueue(new AgentEvent
        {
            Type = "STUDENT_LOGIN",
            OccurredAt = DateTime.UtcNow,
            ComputerUsername = username
        });

        return;
    }

    if (changeDescription.Reason == SessionChangeReason.SessionLogoff)
    {
        if (sessionUsers.TryRemove(sessionId, out string? username))
        {
            eventQueue.Enqueue(new AgentEvent
            {
                Type = "STUDENT_LOGOUT",
                OccurredAt = DateTime.UtcNow,
                ComputerUsername = username
            });
        }
    }
}
}