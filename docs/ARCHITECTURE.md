# Metin2 Remake Server — Architecture & Engineering Principles

## Vision

Build a clean, production-grade Metin2-compatible MMORPG server from scratch using modern C#/.NET engineering practices.

> Legacy source is specification, not architecture.

The original source may be used only to understand protocol behavior, binary layouts, opcodes, client expectations, gameplay rules and compatibility. Do not copy its architectural patterns, class hierarchy, global state, DB-daemon model, FreeBSD assumptions or legacy build system.

## Technology Baseline

- C# / modern .NET
- Ubuntu Server
- Docker
- PostgreSQL
- Redis only when a measured use-case justifies it
- System.Net.Sockets
- System.IO.Pipelines
- Span<T>, ReadOnlySpan<T>, Memory<T>, ArrayPool<T>, ValueTask on hot paths
- OpenTelemetry, Prometheus, Grafana
- Structured logging

## Architectural Style

Use a modular monolith. Start with one deployable process and strongly isolated domain modules. Do not introduce microservices without a demonstrated need.

Target modules include Auth, Characters, World, Combat, Skills, Items, Inventory, Equipment, Monsters, NPC, Shops, Trading, Parties, Guilds, Quests, Dungeons and Events.

Modules must not depend on each other's internals. Domain code must not depend directly on PostgreSQL, Npgsql, Redis, sockets, HTTP, EF Core or other infrastructure details.

## Dependency Direction

```text
Server
  -> Modules
  -> Shared Kernel

Infrastructure implements ports owned by modules/application layers.
```

## Strong Domain Types

Avoid primitive obsession for identifiers.

```csharp
public readonly record struct CharacterId(uint Value);
public readonly record struct ItemId(uint Value);
public readonly record struct GuildId(uint Value);
public readonly record struct MapId(uint Value);
public readonly record struct EntityId(ulong Value);
```

## Protocol as a Single Source of Truth

Packet definitions are declarative and versioned. Prefer YAML or a small dedicated DSL.

A Roslyn Source Generator should generate packet models, serializers, deserializers, opcode registries, validation metadata, dispatcher metadata and protocol documentation.

Runtime reflection is forbidden in packet hot paths.

## Network Pipeline

```text
TCP Socket
  -> PipeReader
  -> Frame Decoder
  -> Packet Decoder
  -> Generated Dispatcher
  -> Packet Handler
  -> Application Command
  -> Domain
```

Packet handlers translate protocol DTOs into application/domain commands. Gameplay rules do not live in packet handlers.

## Sessions

GameSession is connection context only. It is not an Account, Character, or domain entity.

Typical session state may include ConnectionId, AuthenticationState, AccountId, CharacterId, CurrentMap, ProtocolVersion and RemoteEndpoint.

## World Concurrency Model

Avoid uncontrolled shared mutable multithreading. Start with a command-queue-driven deterministic world loop.

```text
Network Threads
  -> Incoming Command Queue
  -> World Loop
```

The loop processes commands, movement, combat, AI, timers, events and visibility in a controlled order.

Scale later with map/dungeon partitioning when justified:

```text
Map1 -> Worker 1
Map2 -> Worker 2
Dungeon A -> Worker 3
Dungeon B -> Worker 4
```

A world entity should have a single authoritative worker at a time.

## Spatial System

Use a spatial grid or equivalent partitioning structure for visibility, mob aggro, nearby-entity queries, movement replication and AoE calculations. Do not use brute-force scans as the default design.

## Domain Design

Do not recreate the legacy CHARACTER god object. Keep responsibilities explicit and compositional. Combat, inventory, equipment, progression, skills and quests should remain separate concerns.

## Domain Events

Use lightweight in-process domain events where they reduce coupling. Do not add Kafka, RabbitMQ or distributed messaging unless the architecture truly becomes distributed.

## Persistence

PostgreSQL is the default durable store. Domain/application layers define repository/port contracts; infrastructure implements them.

Redis is optional and must not become a default dependency.

## Quest System

Prefer a controlled scripting boundary, potentially Lua for compatibility. Scripts may not directly access database, networking or filesystem internals. Expose a constrained quest API through application/domain services.

## Security

The server is authoritative. Never trust client-supplied position, damage, gold, item state, cooldown, movement speed, attack speed or skill result without validation.

Protocol-level defenses should include packet-size limits, opcode validation, session-state validation, malformed-payload rejection and appropriate rate limiting.

## Performance

Optimize measured hot paths, not speculative ones. Benchmark packet encode/decode, world tick, spatial queries, entity lookup, movement, combat and AI.

Use modern .NET primitives deliberately, but do not sacrifice maintainability for meaningless micro-optimizations.

## Testing

Maintain:

- Unit tests
- Integration tests
- Protocol tests
- Architecture tests
- Benchmark tests

Architecture tests should reject dependencies such as Domain -> Infrastructure, Combat -> PostgreSQL, Quest -> Networking internals, or World -> Auth internals.

## Observability

Production systems must expose structured logs, metrics and health information. Track online players, active sessions, packet rate/errors, world tick duration/lag, DB latency, login attempts, active maps and active dungeons.

## Initial Milestone

```text
Original Metin2 Client
 -> TCP Connection
 -> Handshake
 -> Login
 -> Character List
 -> Character Select
 -> Enter World
 -> Map1
 -> Movement
```

Do not expand into advanced gameplay before this compatibility path works end-to-end.

## Decision Rule

For every legacy behavior ask:

> Are we doing this because the old server did it this way, or because it is the right design for the new system?

Legacy code tells us what behavior the client expects. The new architecture determines how we implement it.
