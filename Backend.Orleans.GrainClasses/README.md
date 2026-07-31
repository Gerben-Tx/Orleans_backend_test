# Backend.Orleans.GrainClasses

## Overview

This project contains the implementation of Orleans Grains for the backend. It is a Class Library that defines the core game logic and state management using the Microsoft Orleans framework.

Its main responsibility is to provide the distributed and scalable implementation of the game's entities, such as players and world chunks, and to coordinate their interactions.

---

## Why This Project Exists

This project exists to centralize the implementation of the game's business logic within the Orleans distributed virtual actor model. By separating the grain implementations into their own project, we ensure that:
- The game logic is decoupled from the hosting environment (Silo).
- The grains can be easily tested and reused.
- The project clearly defines the "how" of the game's actors, while the `SharedContracts` project defines the "what".

---

## Responsibilities

- Implements `PlayerGrain` for managing individual player state, position, and chunk transitions.
- Implements `WorldChunkGrain` for managing areas of the game world and tracking players within them.
- Implements `PlayerRegistry` for maintaining a global registry of active players by name.
- Handles real-time update notifications to clients via `IRealtimeUpdatesOrleans`.
- Manages persistent state for players and registries using Orleans `IPersistentState`.

---

## What This Project Does Not Do

- This project does not host the Orleans Silo (this is handled by `Backend.Orleans.Silo`).
- This project does not define the grain interfaces (these are in `Backend.Orleans.SharedContracts`).
- This project does not handle raw SignalR connection management (delegated to the SignalR hub).
- This project does not directly interact with a database; it uses Orleans state abstractions.

---

## Project Type

| Item             | Description                    |
|------------------|--------------------------------|
| Project type     | Class Library / Orleans Grains |
| Target framework | `net8.0`                       |
| Language         | C#                             |
| Main entry point | N/A (Library)                  |
| Output           | Class Library (.dll)           |

---

## Dependencies

### Project Dependencies

- `Backend.Orleans.SharedContracts`
- `Backend.SignalR.SharedContracts`

### External Dependencies

- `Microsoft.Orleans.Runtime` (9.2.1)
- `Microsoft.Orleans.Sdk` (9.2.1)
- `Microsoft.Orleans.Streaming` (9.2.1)

---

## Configuration

This project relies on Orleans Silo configuration, which is typically found in the `Backend.Orleans.Silo` project.

### Required Settings

| Setting      | Description                            | Example                       |
|--------------|----------------------------------------|-------------------------------|
| `tableStore` | Storage provider for grain persistence | Defined in Silo configuration |

---

## Running the Project Locally

This project is a class library and cannot be run directly. It is hosted by the `Backend.Orleans.Silo` project. To test or run the grains, start the Silo project.
