
using System.Runtime.InteropServices;

namespace LabMonitorAgent;

public class WindowsSessionMonitor
{

    [DllImport("kernel32.dll")]
private static extern uint WTSGetActiveConsoleSessionId();

    // Windows session states
    private enum WTS_CONNECTSTATE_CLASS
    {
        Active,
        Connected,
        ConnectQuery,
        Shadow,
        Disconnected,
        Idle,
        Listen,
        Reset,
        Down,
        Init
    }

    // Information returned by Windows about a session
    
[StructLayout(LayoutKind.Sequential)]
private struct WTS_SESSION_INFO
{
    public int SessionID;
    public IntPtr pWinStationName;
    public WTS_CONNECTSTATE_CLASS State;
}


    // Windows API: retrieves the computer's sessions
    [DllImport("Wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int Reserved,
        int Version,
        out IntPtr ppSessionInfo,
        out int pCount
    );

    // Windows API: releases memory allocated by WTSEnumerateSessions
    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);


public void GetSessions()
{
    IntPtr sessionInfo = IntPtr.Zero;
    int count = 0;

    bool success = WTSEnumerateSessions(
        IntPtr.Zero,
        0,
        1,
        out sessionInfo,
        out count
    );

    if (!success)
    {
        throw new System.ComponentModel.Win32Exception(
            Marshal.GetLastWin32Error()
        );
    }

    try
    {
        int dataSize = Marshal.SizeOf<WTS_SESSION_INFO>();
        IntPtr current = sessionInfo;

        for (int i = 0; i < count; i++)
        {
            WTS_SESSION_INFO session =
                Marshal.PtrToStructure<WTS_SESSION_INFO>(current);

            string? username = GetSessionUsername(session.SessionID);

Console.WriteLine(
    $"Session ID: {session.SessionID}, " +
    $"Station: {Marshal.PtrToStringUni(session.pWinStationName)}, " +
    $"State: {session.State}, " +
    $"Username: {username ?? "No user"}"
);

            current = IntPtr.Add(current, dataSize);
        }
    }
    finally
{
    WTSFreeMemory(sessionInfo);
}
}

// Windows API: retrieves a username from a Windows session
[DllImport("Wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
private static extern bool WTSQuerySessionInformation(
    IntPtr hServer,
    int sessionId,
    WTS_INFO_CLASS wtsInfoClass,
    out IntPtr ppBuffer,
    out int pBytesReturned
);


private enum WTS_INFO_CLASS
{
    WTSUserName = 5
}


public string? GetSessionUsername(int sessionId)
{
    IntPtr buffer = IntPtr.Zero;
    int bytesReturned = 0;

    bool success = WTSQuerySessionInformation(
        IntPtr.Zero,
        sessionId,
        WTS_INFO_CLASS.WTSUserName,
        out buffer,
        out bytesReturned
    );

    if (!success)
    {
        throw new System.ComponentModel.Win32Exception(
            Marshal.GetLastWin32Error()
        );
    }

    try
    {
        return Marshal.PtrToStringUni(buffer);
    }
    finally
    {
        if (buffer != IntPtr.Zero)
        {
            WTSFreeMemory(buffer);
        }
    }
}


// Retrieves a username from a Windows session

public string? GetActiveUsername()
{
    uint sessionId = WTSGetActiveConsoleSessionId();

    // No session is currently attached to the physical console.
    if (sessionId == 0xFFFFFFFF)
    {
        return null;
    }

    return GetSessionUsername((int)sessionId);
}


}






