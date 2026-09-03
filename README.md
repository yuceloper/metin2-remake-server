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

The server can run the source-verified ClientVS22 28249 Auth or Game listener; the independent plaintext probe examples below remain historical development checks. Ports are deliberately explicit; the examples below use local development ports and do **not** claim canonical Metin2 port numbers.

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

## Local PostgreSQL

A development PostgreSQL service is available through Docker Compose. The defaults are explicitly development-only and can be overridden with environment variables.

```bash
docker compose up -d postgres
```

Default local values:

```text
host:     127.0.0.1
port:     5432
database: metin2
username: metin2
password: metin2-dev-only
```

Override them with:

```text
METIN2_POSTGRES_DB
METIN2_POSTGRES_USER
METIN2_POSTGRES_PASSWORD
METIN2_POSTGRES_PORT
```

Database changes are explicit embedded SQL migrations under `src/Metin2.Infrastructure.Persistence.Postgres/Migrations`. `PostgresMigrator` records successfully applied versions in `schema_migrations` and executes each new migration transactionally.

Authentication password hashes use a versioned PBKDF2-HMAC-SHA256 format. The current production default is 600,000 iterations with a unique random salt. The algorithm and work factor are encoded with each stored hash so password storage can be upgraded without changing the Auth application contract.

## ClientVS22 Local Trial Stack

The full source-verified ClientVS22 28249 Auth + Game path can be started with PostgreSQL:

```bash
docker compose up --build auth game
```

Development defaults:

```text
Auth address: 127.0.0.1:11002
Game address: 127.0.0.1:13000
Username:     test
Password:     test1234
Character:    testHero
```

Override credentials before starting:

```bash
METIN2_DEV_USERNAME=myplayer METIN2_DEV_PASSWORD='change-this-password' \
docker compose up --build auth game
```

For a client on another computer, set `METIN2_ADVERTISED_ADDRESS` to the server's reachable
IPv4 address. The Compose defaults are development-only; do not expose the known default
database or game credentials to an untrusted network.

Both server processes apply embedded migrations during startup. A PostgreSQL advisory lock
serializes concurrent migration attempts. Only the Auth Compose service opts into development
account seeding; standalone/production runs do not seed unless
`METIN2_SEED_DEVELOPMENT_ACCOUNT=true` is explicitly set together with
`METIN2_DEV_USERNAME` and `METIN2_DEV_PASSWORD`.

## Repository Structure

```text
src/
  Metin2.Server/
  Metin2.Protocol/
  Metin2.Protocol.Generator/
  Metin2.Shared/
  Metin2.Infrastructure.Networking/
  Metin2.Infrastructure.Persistence.Postgres/
  Modules/
    Metin2.Modules.Auth/

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
