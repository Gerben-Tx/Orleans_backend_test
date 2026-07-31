# Backend.SignalR

## Overview

This project is the SignalR hub server for the backend. It is an ASP.NET Core Web Application that facilitates real-time, two-way communication between the Orleans backend and the game clients (e.g., Godot).

Its main responsibility is to act as a communication bridge, pushing updates from Orleans grains to connected clients and potentially routing client requests back to the Orleans cluster.

---

## Why This Project Exists

This project exists to provide a dedicated real-time communication layer. By separating SignalR into its own project, we ensure that:
- The Orleans Silo is not burdened with managing thousands of persistent TCP connections from web/game clients.
- We can leverage standard ASP.NET Core features for authentication, scaling, and middleware.
- The communication protocol (SignalR) is decoupled from the business logic (Orleans Grains).
- It provides a unified gateway for all real-time events in the system.

---

## Responsibilities

- Hosts the SignalR Hubs: `RealtimeUpdatesHubClient` and `RealtimeUpdatesHubOrleans`.
- Maps the real-time update endpoints (`/realtimeUpdatesHubClient` and `/realtimeUpdatesHubOrleans`).
- Acts as an Orleans Client to interact with the grain cluster.
- Configures Orleans stream providers for consuming events from the Silo.
- Handles group management for world chunks to efficiently broadcast updates to interested clients.

---

## What This Project Does Not Do

- This project does not contain the game logic or grain implementations (these are in `Backend.Orleans.GrainClasses`).
- This project does not host the Orleans Silo (this is handled by `Backend.Orleans.Silo`).
- This project does not directly manage persistent state (handled by Orleans).
- This project does not define the SignalR interfaces (these are in `Backend.SignalR.SharedContracts`).

---

## Project Type

| Item             | Description                  |
|------------------|------------------------------|
| Project type     | ASP.NET Core Web Application |
| Target framework | `net8.0`                     |
| Language         | C#                           |
| Main entry point | `Program.cs`                 |
| Output           | Web Application / executable |

---

## Dependencies

### Project Dependencies

- `Backend.Orleans.SharedContracts`
- `Backend.SignalR.SharedContracts`

### External Dependencies

- `Microsoft.Orleans.Client` (9.2.1)
- `Microsoft.Orleans.Streaming.AzureStorage` (9.2.1)
- Standard ASP.NET Core SignalR libraries.

---

## Configuration

The project is configured in `Program.cs` and `appsettings.json`.

### Required Settings

| Setting              | Description                             | Default/Example              |
|----------------------|-----------------------------------------|------------------------------|
| `Urls`               | The address the server listens on       | `http://localhost:5202`      |
| `Orleans Clustering` | Connection to the Orleans Silo          | `UseLocalhostClustering()`   |
| `QueueServiceClient` | Connection to Azure Queue for streaming | `UseDevelopmentStorage=true` |

---

## Running the Project Locally

1. Ensure the **Azurite** emulator is running if using Azure Queue Storage for streams.
2. Start the `Backend.SignalR` project:
   ```bash
   dotnet run --project Backend.SignalR
   ```
3. The hubs will be available at:
   - `http://localhost:5202/realtimeUpdatesHubClient`
   - `http://localhost:5202/realtimeUpdatesHubOrleans`
