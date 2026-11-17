
using System.Runtime.InteropServices;

namespace LabMonitorAgent;

public class WindowsSessionMonitor(
    ILogger<WindowsSessionMonitor> logger,
    EventQueue queue
)
{
    private const int PollIntervalSeconds = 3;

    // Stores the Windows sessions we have already detected.
    private readonly Dictionary<int, string> knownSessions = new();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Windows session monitoring started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Dictionary<int, string> currentSessions =
                    GetLoggedInSessions();

                // Detect new sessions and changed usernames.
                foreach (var session in currentSessions)
                {
                    int sessionId = session.Key;
                    string username = session.Value;

                    if (knownSessions.TryGetValue(sessionId, out string? oldUsername))
                    {
                        if (oldUsername != username)
                        {
                            await QueueLogoutAsync(oldUsername);
                            await QueueLoginAsync(username);
                            knownSessions[sessionId] = username;
                        }
                    }
                    else
                    {
                        await QueueLoginAsync(username);
                        knownSessions[sessionId] = username;
                    }
                }

                // Detect sessions that have disappeared.
                foreach (var session in knownSessions.ToArray())
                {
                    if (!currentSessions.ContainsKey(session.Key))
                    {
                        await QueueLogoutAsync(session.Value);
                        knownSessions.Remove(session.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking Windows sessions.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(PollIntervalSeconds),
                cancellationToken
            );
        }
    }

    private async Task QueueLoginAsync(string username)
    {
        await queue.EnqueueAsync(
            AgentEvent.Create(
                AgentEventTypes.StudentLogin,
                computerUsername: username
            )
        );

        logger.LogInformation(
            "Windows student session detected: {username}",
            username
        );
    }

    private async Task QueueLogoutAsync(string username)
    {
        await queue.EnqueueAsync(
            AgentEvent.Create(
                AgentEventTypes.StudentLogout,
                computerUsername: username
            )
        );

        logger.LogInformation(
            "Windows student session ended: {username}",
            username
        );
    }

    private static Dictionary<int, string> GetLoggedInSessions()
    {
        Dictionary<int, string> sessions = new();

        if (!WTSEnumerateSessions(
                IntPtr.Zero,
                0,
                1,
                out IntPtr sessionInfo,
                out int count))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error()
            );
        }

        try
        {
            int structSize = Marshal.SizeOf<WTS_SESSION_INFO>();

            for (int i = 0; i < count; i++)
            {
                IntPtr current = IntPtr.Add(
                    sessionInfo,
                    i * structSize
                );

                WTS_SESSION_INFO info =
                    Marshal.PtrToStructure<WTS_SESSION_INFO>(current);

                // Session 0 is reserved for Windows services.
                if (info.SessionId == 0)
                    continue;

                // Keep active, connected, idle, and disconnected sessions.
                // A disconnected session is not necessarily logged out.
                if (info.State != WTS_CONNECTSTATE_CLASS.WTSActive &&
                    info.State != WTS_CONNECTSTATE_CLASS.WTSConnected &&
                    info.State != WTS_CONNECTSTATE_CLASS.WTSDisconnected &&
                    info.State != WTS_CONNECTSTATE_CLASS.WTSIdle)
                    continue;

                string username = GetSessionUsername(info.SessionId);

                if (!string.IsNullOrWhiteSpace(username))
                {
                    sessions[info.SessionId] = username;
                }
            }
        }
        finally
        {
            WTSFreeMemory(sessionInfo);
        }

        return sessions;
    }

    private static string GetSessionUsername(int sessionId)
    {
        if (!WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                WTS_INFO_CLASS.WTSUserName,
                out IntPtr buffer,
                out _))
        {
            return "";
        }

        try
        {
            return Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [DllImport("Wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int reserved,
        int version,
        out IntPtr sessionInfo,
        out int count
    );

    [DllImport(
    "Wtsapi32.dll",
    EntryPoint = "WTSQuerySessionInformationW",
    CharSet = CharSet.Unicode,
    SetLastError = true
)]
[return: MarshalAs(UnmanagedType.Bool)]
private static extern bool WTSQuerySessionInformation(
    IntPtr hServer,
    int sessionId,
    WTS_INFO_CLASS infoClass,
    out IntPtr buffer,
    out int bytesReturned
);

    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    private enum WTS_INFO_CLASS
    {
        WTSUserName = 5
    }

    private enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive = 0,
        WTSConnected = 1,
        WTSConnectQuery = 2,
        WTSShadow = 3,
        WTSDisconnected = 4,
        WTSIdle = 5,
        WTSListen = 6,
        WTSReset = 7,
        WTSDown = 8,
        WTSInit = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr WinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }
}
