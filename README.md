# Metin2 Remake Server

A clean-room, modern Metin2-compatible MMORPG server platform written in C#/.NET for Ubuntu Server.

## Core Principle

> Legacy source is specification, not architecture.

The original Metin2 server source is used only to understand protocol layouts, opcodes, client expectations, gameplay behavior and compatibility requirements. Legacy architecture, global state, singleton-heavy design, DB daemon structure and build system are not carried forward.

## Runtime Baseline

- .NET 10 LTS
- Ubuntu-first deployment
- Docker-compatible production packaging

## Architecture

- C# / modern .NET
- Modular Monolith
- Ubuntu Server / Docker
- PostgreSQL
- Redis only when justified
- System.Net.Sockets + System.IO.Pipelines
- Span<T>, Memory<T>, ArrayPool<T>, ValueTask on hot paths
- Roslyn Source Generator based packet generation
- Compile-time serialization/deserialization without runtime reflection
- Strongly typed domain IDs
- Server-authoritative gameplay
- In-process domain events
- OpenTelemetry + Prometheus + Grafana
- Unit, integration, protocol, benchmark and architecture tests

## Initial Milestone

Original Metin2 Client -> TCP -> Handshake -> Login -> Character List -> Character Select -> Enter World -> Map1 -> Movement

## Handshake Development Checkpoint

The current server can run a reference-backed legacy handshake listener in either Auth or Game mode. Ports are deliberately explicit; the examples below use local development ports and do **not** claim canonical Metin2 port numbers.

Run an Auth handshake listener:

```bash
dotnet run --project src/Metin2.Server -- serve --mode auth --bind 127.0.0.1 --port 15000
```

Verify it with the independent protocol probe:

```bash
dotnet run --project tools/Metin2.HandshakeProbe -- --host 127.0.0.1 --port 15000 --expect auth
```

Run a Game handshake listener:

```bash
dotnet run --project src/Metin2.Server -- serve --mode game --bind 127.0.0.1 --port 16000
```

Verify the Game/Login transition:

```bash
dotnet run --project tools/Metin2.HandshakeProbe -- --host 127.0.0.1 --port 16000 --expect login
```

The probe validates the current reference-confirmed wire sequence:

```text
FD 01
FF <handshake payload>
...
FD 0A   # Auth
```

or:

```text
FD 01
FF <handshake payload>
...
FD 02   # Game/Login
```

This is a development compatibility probe, not proof that a stock Metin2 client is compatible yet.

## Repository Structure

```text
src/
  Metin2.Server/
  Metin2.Protocol/
  Metin2.Protocol.Generator/
  Metin2.Shared/
  Metin2.Infrastructure.Networking/

tools/
  Metin2.HandshakeProbe/

tests/
protocol/
docs/
  adr/
```

## Project Memory

- [Architecture](docs/ARCHITECTURE.md)
- [Development TODO](docs/TODO.md)
- [Master Development Prompt](docs/MASTER_PROMPT.md)

Any significant implementation change should update code, TODO status, and an ADR when the architecture changes.
