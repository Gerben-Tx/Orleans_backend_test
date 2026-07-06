# Backend.Orleans.SharedContracts

## Overview

This project contains the shared contracts for the Orleans-based backend. It is a Class Library that defines the interfaces for Orleans Grains and the serializable data structures used for communication within the Orleans cluster.

Its main responsibility is to provide a common set of definitions that both the implementation (`GrainClasses`) and the hosts/clients (`Silo`, `SignalR`) can reference.

---

## Why This Project Exists

This project exists to decouple the grain interfaces from their implementations. By separating the contracts into their own project, we ensure that:
- Any project that needs to interact with grains only needs to reference the interfaces, not the entire implementation logic.
- We avoid circular dependencies between the grain implementations and the projects that use them.
- It provides a centralized location for all serializable models used in grain state and communication.

---

## Responsibilities

- Defines grain interfaces: `IPlayerGrain`, `IWorldChunkGrain`, and `IPlayerRegistry`.
- Defines serializable data structures, such as `SerializableVector2`, used in grain state and method parameters.
- Provides the necessary attributes and markers for Orleans code generation (`[GenerateSerializer]`, `[Id]`).

---

## What This Project Does Not Do

- This project does not contain any business logic or grain implementations (these are in `Backend.Orleans.GrainClasses`).
- This project does not handle grain persistence or hosting (handled by `Backend.Orleans.Silo`).
- This project does not define SignalR-specific contracts (these are in `Backend.SignalR.SharedContracts`).

---

## Project Type

| Item | Description |
|---|---|
| Project type | Class Library / Orleans Contracts |
| Target framework | `net8.0` |
| Language | C# |
| Main entry point | N/A (Library) |
| Output | Class Library (.dll) |

---

## Dependencies

### Project Dependencies

- None

### External Dependencies

- `Microsoft.Orleans.Sdk` (9.2.1)

---

## Configuration

This project does not require any specific configuration as it only contains interfaces and data models.

---

## Running the Project Locally

This project is a class library and cannot be run directly. It is referenced by other projects in the solution.
