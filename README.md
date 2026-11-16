# Computer Laboratory User Monitoring System (CLUMS)

## Project Overview

The **Computer Laboratory User Monitoring System (CLUMS)** is a centralized computer monitoring system designed to automatically record and monitor user activities across computers in a computer laboratory.

The system uses **C# (.NET) monitoring agents** installed on each laboratory computer to collect user and system activity records. These agents communicate with a centralized **Python backend** through REST APIs to transmit monitoring events and heartbeat messages.

The Python backend processes incoming data, manages monitoring records, handles authentication, and communicates with the database.

Administrators can access a centralized web dashboard developed using HTML and CSS to monitor computer availability, view student sessions, review application usage, and track disconnection records.

## System Architecture

CLUMS follows a client-server architecture consisting of three primary components:

1. **Frontend (HTML and CSS)** – Provides the administrator dashboard.
2. **Backend (Python)** – Processes requests, manages records, handles authentication, and provides REST APIs.
3. **Monitoring Agents (C# .NET)** – Installed on laboratory computers to collect and transmit monitoring events.

### System Components

**1. Frontend – HTML and CSS**

- Provides the administrator dashboard.
- Displays laboratory computer statuses.
- Displays student login and logout records.
- Displays application usage history.
- Provides disconnection monitoring.
- Displays reports and analytics.
- Communicates with the Python backend through REST APIs.

**2. Backend – Python**

- Provides REST API endpoints.
- Receives monitoring events from C# agents.
- Processes and validates incoming records.
- Manages computer registration.
- Manages student sessions.
- Processes application usage records.
- Handles heartbeat monitoring.
- Detects missing agent communications.
- Manages administrator authentication.
- Communicates with the database.
- Provides monitoring information to the frontend.

**3. Monitoring Agents – C# (.NET)**

Installed on each laboratory computer.

Responsibilities:

- Detect student login activities.
- Detect user logout activities.
- Monitor system shutdowns and restarts.
- Monitor running applications.
- Record application start and end times.
- Collect computer information.
- Send periodic heartbeat messages.
- Transmit monitoring events to the Python backend.
- Detect and synchronize pending events after reconnection.

## Technology Stack

| Component | Technology |
|---|---|
| Frontend | HTML, CSS |
| Backend | Python |
| Backend API | Python REST API |
| Monitoring Agents | C# (.NET) |
| Operating System Monitoring | Windows Event Logs |
| Application Detection | Windows API / Process Monitoring |
| Database | MySQL / SQLite |
| API Communication | REST API |
| Authentication | JWT |
| Communication Format | JSON |

## Expected Outcome

Upon completion, CLUMS will provide a centralized platform that automatically monitors laboratory computers and records student usage activities.

The system will use C# agents to collect computer activities, a Python backend to process and manage monitoring data, and an HTML/CSS dashboard to present monitoring information to administrators.

## Project Status

**Development Stage:** Planning and Initial Development

**Current Technology Stack:**

- Frontend: HTML and CSS
- Backend: Python
- Monitoring Agents: C# (.NET)
- Database: MySQL / SQLite
