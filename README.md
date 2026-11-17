# CLUMS Agent: setup (stage 1)

What the agent does now: registers with the CLUMS server, sends a heartbeat every few seconds,
and sends `AGENT_STARTED` / `AGENT_STOPPED` events. Events are kept in a file until the server
confirms them, so nothing is lost if the server or network is down.

Not done yet: student login/logout, running apps, shutdown/restart detection, running as a
Windows service, and self-update (an "Update" command from the dashboard is only logged).

## Run it (on your PC, for testing)

1. Start the backend (see the CLUMS backend README) and note `AGENT_API_KEY` from its `.env`.
2. In this folder:
   ```
   dotnet user-secrets set "Backend:AgentApiKey" "<the AGENT_API_KEY value>"
   dotnet run
   ```
   If the backend isn't on `http://localhost:8000`, change `Backend:BaseUrl` in `appsettings.json`
   (or set it the same way as the key).
3. The computer appears on the dashboard as **Idle**. Stop the agent with Ctrl+C and it shows
   **Offline** with a "Normal shutdown" record; start it again and it comes back.

In the backend's `.env`, set `LATEST_AGENT_VERSION=1.0.0` (this agent's version, from `<Version>`
in the `.csproj`). Otherwise the dashboard will say this agent is behind the current release.

## Settings

| Setting | Where | Meaning |
|---|---|---|
| `Backend:BaseUrl` | `appsettings.json` | Server address, e.g. `http://192.168.1.10:8000` |
| `Backend:AgentApiKey` | user-secrets (dev) or environment variable `Backend__AgentApiKey` (lab PCs) | Must equal `AGENT_API_KEY` on the server. **Never commit it.** |
| `Backend:RequestTimeoutSeconds` | `appsettings.json` | How long to wait for the server (default 10) |

The agent won't start without a key, and tells you how to set one. The heartbeat interval isn't set here:
the server sends it (change it on the dashboard's Agents screen).

Use `https://` for the server if the lab network isn't closed: the key is sent with every request.

## Files on the PC (`C:\ProgramData\LabMonitorAgent\`)

| File | Purpose |
|---|---|
| `computer-id.txt` | This PC's permanent ID. **If you clone a disk image, delete this file on each copy** so every PC makes its own |
| `event-queue.jsonl` | Events not yet confirmed by the server. Safe to delete only if you accept losing them |
| `Logs\agent-log.txt` | Local record of start/stop |

## Behaviour worth knowing

- **Server down:** the agent keeps running, queues events (up to 10,000, oldest dropped after that), and sends them when the server is back. A message is logged once a minute, not every few seconds.
- **Wrong key:** it logs a clear error and keeps the events queued.
- **A bad event:** if the server rejects one event, the agent tries the others one by one and drops only the bad one.
- **Network change:** if the IP or MAC changes, the agent registers again so the dashboard stays correct. Virtual adapters (VMware, VirtualBox, Hyper-V, VPN) are ignored; wired is preferred over Wi-Fi.
- **Shutdown:** on stop it tries for 3 seconds to deliver `AGENT_STOPPED`; if it can't, the event is delivered on the next start.
