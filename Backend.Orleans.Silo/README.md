# Backend.Orleans.Silo

## Overview

This project is the host for the Orleans cluster (the "Silo"). It is a Console Application responsible for bootstrapping the Orleans framework, configuring its various services (persistence, streaming, clustering), and hosting the game's Grains.

Its main responsibility is to provide the execution environment for the distributed actors defined in the `GrainClasses` project.

---

## Why This Project Exists

This project exists to separate the hosting and configuration of the Orleans cluster from the actual business logic. By having a dedicated Silo project, we can:
- Centralize the configuration of infrastructure concerns (storage, networking, logging).
- Provide a clear entry point for starting the backend services.
- Easily scale the system by running multiple instances of this project in a cluster.
- Host management tools like the Orleans Dashboard.

---

## Responsibilities

- Configures and starts the Orleans Silo using `LocalhostClustering` (for development).
- Configures grain persistence using Azure Table Storage (`tableStore`) and Blob Storage (`blobStore`, `PubSubStore`).
- Configures Orleans Streams using Azure Queue Storage (`StreamProvider`).
- Hosts the Orleans Dashboard for monitoring grain activity and cluster health.
- Configures logging for both Orleans and application-specific namespaces.
- Provides a SignalR client proxy (`IRealtimeUpdatesOrleans`) to bridge Orleans grains with the SignalR hub.

---

## What This Project Does Not Do

- This project does not contain grain implementations (these are in `Backend.Orleans.GrainClasses`).
- This project does not define grain interfaces (these are in `Backend.Orleans.SharedContracts`).
- This project does not host the SignalR Hub server (this is handled by `Backend.SignalR`).
- This project does not handle client-facing game logic directly; it acts as the backbone for grain execution.

---

## Project Type

| Item | Description |
|---|---|
| Project type | Console Application / Orleans Silo |
| Target framework | `net8.0` |
| Language | C# |
| Main entry point | `Program.cs` |
| Output | Executable (.exe) |

---

## Dependencies

### Project Dependencies

- `Backend.Orleans.GrainClasses`

### External Dependencies

- `Microsoft.Orleans.Server` (9.2.1)
- `Microsoft.Orleans.Persistence.AzureStorage` (9.2.1)
- `Microsoft.Orleans.Streaming.AzureStorage` (9.2.1)
- `Microsoft.Extensions.Hosting` (9.0.8)
- `OrleansDashboard` (8.2.0)

---

## Configuration

The project is configured in `Program.cs` and currently uses local development storage.

### Required Settings

| Setting | Description | Default/Example |
|---|---|---|
| `TableServiceClient` | Connection to Azure Table Storage | `UseDevelopmentStorage=true` |
| `BlobServiceClient` | Connection to Azure Blob Storage | `UseDevelopmentStorage=true` |
| `QueueServiceClient` | Connection to Azure Queue Storage | `UseDevelopmentStorage=true` |
| `SignalR Hub URL` | URL of the RealtimeUpdatesHub | `http://localhost:5202/realtimeUpdatesHubOrleans` |

---

## Running the Project Locally

1. Ensure the **Azurite** emulator (or a real Azure Storage account) is running to provide Table, Blob, and Queue services.
2. Ensure the `Backend.SignalR` project is running if real-time updates are needed.
3. Start the `Backend.Orleans.Silo` project from your IDE or via command line:
   ```bash
   dotnet run --project Backend.Orleans.Silo
   ```
4. Access the Orleans Dashboard at `http://localhost:8080` (default port).
