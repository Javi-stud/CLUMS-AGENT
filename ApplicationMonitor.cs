
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LabMonitorAgent;

public class ApplicationMonitor
{
    private delegate bool EnumWindowsProc(
        IntPtr hWnd,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(
        IntPtr hWnd
    );

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(
        IntPtr hWnd
    );

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr hWnd,
        out uint processId
    );

    private static readonly HashSet<string> ExcludedApplications =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "RtkUWP",
            "ApplicationFrameHost",
            "TextInputHost",
            "ShellExperienceHost",
            "ShellHost",
            "RuntimeBroker",
            "backgroundTaskHost",
            "StartMenuExperienceHost",
            "SearchHost",
            "WidgetService",
            "WidgetBoard",
            "sihost",
            "dwm",
            "winlogon",
            "csrss",
            "fontdrvhost",
            "taskhostw"
        };

    private readonly EventQueue queue;
    private readonly ILogger<ApplicationMonitor> logger;

    private const int CheckIntervalSeconds = 5;

    private readonly int currentSessionId =
        Process.GetCurrentProcess().SessionId;

    private readonly HashSet<string> runningApplications =
        new(StringComparer.OrdinalIgnoreCase);

    public ApplicationMonitor(
        EventQueue queue,
        ILogger<ApplicationMonitor> logger)
    {
        this.queue = queue;
        this.logger = logger;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Application monitoring started."
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckApplicationsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Application monitoring check failed"
                );
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(CheckIntervalSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CheckApplicationsAsync()
    {
        HashSet<string> currentApplications =
            new(StringComparer.OrdinalIgnoreCase);

        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            // Ignore windows without a title.
            // We check the length only; we never read the title.
            if (GetWindowTextLength(hWnd) == 0)
                return true;

            GetWindowThreadProcessId(
                hWnd,
                out uint processId
            );

            try
            {
                using Process process =
                    Process.GetProcessById((int)processId);

                if (process.SessionId != currentSessionId)
                    return true;

                if (process.Id == Environment.ProcessId)
                    return true;

                string appName = process.ProcessName;

                if (string.IsNullOrWhiteSpace(appName))
                    return true;

                if (ExcludedApplications.Contains(appName))
                    return true;

                // HashSet prevents duplicate entries
                // when an application has multiple windows.
                currentApplications.Add(appName);
            }
            catch
            {
                // Process may have exited or become inaccessible.
            }

            return true;
        }, IntPtr.Zero);

        // Detect newly opened applications.
        foreach (string appName in currentApplications)
        {
            if (!runningApplications.Contains(appName))
            {
                await queue.EnqueueAsync(
                    AgentEvent.Create(
                        AgentEventTypes.AppStarted,
                        appName: appName
                    )
                );

                logger.LogInformation(
                    "Application started: {appName}",
                    appName
                );
            }
        }

        // Detect applications that are no longer open.
        foreach (string appName in runningApplications)
        {
            if (!currentApplications.Contains(appName))
            {
                await queue.EnqueueAsync(
                    AgentEvent.Create(
                        AgentEventTypes.AppStopped,
                        appName: appName
                    )
                );

                logger.LogInformation(
                    "Application stopped: {appName}",
                    appName
                );
            }
        }

        // Save the current application snapshot.
        runningApplications.Clear();

        foreach (string appName in currentApplications)
        {
            runningApplications.Add(appName);
        }
    }
}
