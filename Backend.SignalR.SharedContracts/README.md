# Backend.SignalR.SharedContracts

## Overview

This project contains the shared contracts for SignalR communication. It is a Class Library that defines the interfaces for SignalR hubs and clients, as well as the message models used for real-time updates.

Its main responsibility is to provide a common set of definitions that both the SignalR server (`Backend.SignalR`) and the SignalR clients (including the Orleans Silo and external game clients) can reference to ensure type-safe communication.

---

## Why This Project Exists

This project exists to centralize the definitions of the real-time communication protocol. By separating these contracts, we ensure that:
- Any component that needs to send or receive real-time updates (like Orleans Grains via the Silo) can reference the interfaces without needing a dependency on the full SignalR server implementation.
- Game clients (like Godot) can potentially use these contracts to ensure they are calling the correct methods on the hubs.
- It provides a single source of truth for the structure of real-time messages.

---

## Responsibilities

- Defines hub interfaces: `IRealtimeUpdatesHub`.
- Defines client interfaces: `IRealtimeUpdatesClient`.
- Defines specialized interfaces for backend-to-backend communication: `IRealtimeUpdatesOrleans`.
- Defines message data structures, such as `PlayerListMessage`.

---

## What This Project Does Not Do

- This project does not contain any hub logic or SignalR server implementation (this is in `Backend.SignalR`).
- This project does not handle Orleans grain logic (these are in `Backend.Orleans.GrainClasses`).
- This project does not define Orleans-specific grain interfaces (these are in `Backend.Orleans.SharedContracts`).

---

## Project Type

| Item             | Description                       |
|------------------|-----------------------------------|
| Project type     | Class Library / SignalR Contracts |
| Target framework | `net8.0`                          |
| Language         | C#                                |
| Main entry point | N/A (Library)                     |
| Output           | Class Library (.dll)              |

---

## Dependencies

### Project Dependencies

- None

### External Dependencies

- `Microsoft.AspNetCore.SignalR.Client` (9.0.9)

---

## Configuration

This project does not require any specific configuration as it only contains interfaces and data models.

---

## Running the Project Locally

This project is a class library and cannot be run directly. It is referenced by other projects in the solution.
