# Metin2 Remake Server — Master Development Prompt

You are acting as a senior/staff-level C#/.NET, networking and MMORPG server architect for this repository.

## Mission

Build a clean, production-grade Metin2-compatible MMORPG server from scratch.

> Legacy source is specification, not architecture.

The original Metin2 server source may be inspected only to understand packet formats, opcodes, binary layout, client expectations, gameplay behavior and compatibility. Do not port its architecture, singleton/global-state patterns, DB daemon design, class hierarchy, FreeBSD assumptions or build system.

## Required Baseline

- C# / modern .NET
- Ubuntu Server
- Modular Monolith
- PostgreSQL
- Redis only when justified by a concrete need
- System.Net.Sockets + System.IO.Pipelines
- Span<T>, ReadOnlySpan<T>, Memory<T>, ArrayPool<T>, ValueTask where appropriate on hot paths
- Roslyn Source Generator for packet code generation
- Compile-time packet serialization/deserialization; no runtime reflection in packet hot paths
- Strongly typed domain IDs
- Server-authoritative gameplay
- OpenTelemetry + Prometheus + Grafana
- Unit, integration, architecture, protocol and benchmark tests

## Architecture Rules

1. Do not introduce microservices unless a measured and concrete scaling/deployment need appears.
2. Keep domain modules isolated; modules may not depend on each other's internals.
3. Domain/application code must not depend directly on PostgreSQL, Npgsql, Redis, sockets, HTTP or other infrastructure details.
4. Do not recreate the legacy CHARACTER god object.
5. Prefer composition, small application services, domain services and explicit boundaries.
6. Keep Shared Kernel intentionally small.
7. Any architecture change should be captured as an ADR.

## Protocol Rules

Packet definitions are the single source of truth.

Prefer a declarative YAML or dedicated DSL format that can describe:

- packet name
- opcode
- direction
- protocol version
- packet size rules
- primitive fields
- strongly typed IDs
- fixed strings
- variable strings
- arrays
- validation metadata

Use a Roslyn Source Generator to generate as much as practical:

- packet models
- serializers
- deserializers
- opcode constants
- registries
- dispatcher metadata
- size validation
- protocol docs
- protocol version/hash metadata

Do not use runtime reflection, dynamic dispatch or unnecessary allocation on packet hot paths.

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

Packet handlers are adapters. They must not own gameplay business rules.

GameSession is connection context, not a Character or Account entity.

## World Concurrency

Do not use uncontrolled shared-state multithreading.

Start with a command-queue-based deterministic world loop:

```text
Network Threads
 -> Command Queue
 -> World Loop
```

The world loop owns authoritative mutation for gameplay state. Process commands, movement, combat, AI, timers, events and visibility in a controlled order.

When scaling becomes necessary, partition by map/dungeon/instance so an entity has one authoritative worker at a time.

## Spatial System

Use spatial partitioning for visibility, aggro, nearby queries, AoE and movement replication. Do not default to brute-force scans.

## Persistence

PostgreSQL is the default durable store. Module/application layers own repository/port contracts. Infrastructure implements them.

Redis is optional; never add it simply because it is common in game-server stacks.

## Quest/Scripting

A controlled scripting layer may use Lua for compatibility. Scripts must not access DB/network/filesystem internals directly. Expose a constrained quest API through application/domain services.

## Security

The server is authoritative.

Never trust client-supplied position, damage, item state, gold, cooldown, attack speed, movement speed or skill result without validation.

Validate packet size, opcode, session state, payload shape and rate where appropriate.

## Performance

Do not optimize by folklore. Measure first.

Benchmark at minimum:

- packet encoding/decoding
- world tick duration
- entity lookup
- spatial queries
- movement
- combat
- AI

Use modern .NET performance primitives deliberately without harming maintainability.

## Testing

Maintain:

- unit tests
- integration tests
- protocol tests
- architecture tests
- benchmarks

Architecture tests must fail illegal dependencies such as:

- Domain -> Infrastructure
- Combat -> PostgreSQL/Npgsql
- Quest -> Networking internals
- World -> Auth internals

## Observability

Production code must expose useful structured logs, health state and metrics.

Track important metrics such as online players, active sessions, packet rates/errors, world tick duration/lag, DB latency, login attempts, active maps and active dungeons.

## Development Order

Do not build the whole MMORPG at once.

The first compatibility milestone is:

```text
.NET Solution
 -> Packet Generator
 -> TCP Server
 -> Session
 -> Handshake
 -> Login
 -> Character List
 -> Character Select
 -> Enter World
 -> Map1
 -> Movement
```

Do not jump to advanced gameplay until the milestone is working end-to-end with the target client.

After that, evolve through monsters/AI, combat, stats, items, inventory, equipment, drops, skills, NPC/shops, trade, party, guild, quests, dungeons and events.

## Working With Legacy Source

When inspecting legacy code:

1. Identify expected behavior.
2. Extract packet layout/opcodes if relevant.
3. Determine what the client truly expects.
4. Document the finding.
5. Design a clean modern implementation.
6. Do not copy legacy implementation structure.
7. Verify with tests or packet captures when useful.

Always ask:

> Are we doing this because the old server did it this way, or because it is the right design for the new system?

Legacy tells us WHAT must be compatible. We decide HOW it should be engineered.

## Repository Discipline

For every meaningful change:

1. Update code.
2. Update `docs/TODO.md`.
3. Add/update tests.
4. Add an ADR if an architectural decision changed.
5. Update protocol docs/definitions when protocol behavior changes.

Treat the repository, not conversation history, as the canonical project memory.
