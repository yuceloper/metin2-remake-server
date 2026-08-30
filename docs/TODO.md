# Metin2 Remake Server — Development TODO

## Phase 0 — Repository Foundation

- [x] Create repository
- [x] Add README
- [x] Add architecture principles
- [x] Add master development prompt
- [x] Create `Metin2.sln`
- [x] Add `Directory.Build.props`
- [x] Enable nullable reference types
- [x] Enable warnings as errors
- [x] Add `.editorconfig`
- [x] Add analyzers
- [x] Create `src`, `tests`, `protocol`, `docs/adr` structure
- [x] Add CI pipeline
- [x] Verify Debug/Release builds
- [x] Verify Ubuntu build
- [x] Add Docker build

## Phase 1 — Shared Kernel

- [x] CharacterId
- [x] AccountId
- [x] ItemId
- [x] GuildId
- [x] MapId
- [x] EntityId
- [x] MonsterId
- [x] Minimal result/error abstractions
- [x] Domain event primitives

## Phase 2 — Protocol Definition Format

- [x] Decide YAML vs custom DSL
- [x] Define opcode metadata
- [x] Define packet direction
- [x] Primitive field types
- [x] Fixed-length strings
- [x] Variable-length strings
- [x] Byte arrays
- [x] Strong ID support
- [x] Array support
- [x] Protocol version metadata
- [x] Packet-size metadata
- [x] Validation rules
- [x] Packet sequence metadata
- [x] Define payload-vs-framing boundary

## Phase 3 — Roslyn Packet Generator

- [x] Create `Metin2.Protocol.Generator`
- [x] Parse packet definitions
- [x] Generator diagnostics
- [x] Generate packet models
- [x] Generate opcode constants
- [ ] Complete serializers for every schema field kind
- [ ] Complete deserializers for every schema field kind
- [x] Generate packet registry
- [x] Direction validation
- [x] Fixed payload-size calculation for scalar/string/primitive-array packets
- [x] Strong ID serialization for fixed scalar codecs
- [x] Generate fixed-layout primitive codecs
- [x] Generate legacy fixed ASCII null-terminated string codecs
- [x] Generate fixed primitive-array codecs
- [x] Expose fixed `PayloadSize` semantics
- [ ] Generate fixed raw-byte codecs
- [ ] Generate variable-length string/bytes/array codecs
- [x] Generator tests
- [x] Runtime packet IO tests for fixed ASCII behavior
- [ ] Snapshot/golden tests
- [x] Verify no runtime reflection
- [ ] Allocation benchmarks

## Phase 4 — Protocol Documentation

- [ ] Generate packet tables
- [ ] Generate opcode documentation
- [ ] Generate C2S/S2C lists
- [ ] Generate field layouts
- [ ] Generate packet-size information
- [ ] Generate Markdown docs
- [ ] Protocol version/hash

## Phase 5 — Networking Foundation

- [x] TCP listener
- [x] Socket lifecycle
- [x] Connection abstraction
- [x] GameSession
- [x] System.IO.Pipelines receive pipeline
- [x] Send pipeline
- [x] Legacy one-byte header framing profile
- [x] Optional trailing sequence-byte framing
- [x] Profile-driven legacy sequence validation core
- [x] Frame decoder/encoder
- [x] Packet decoder/encoder
- [x] Graceful disconnect
- [x] Cancellation handling in receive loop
- [ ] Connection timeout
- [ ] Maximum packet size
- [x] Invalid packet handling in receive loop
- [ ] Base rate limiting
- [ ] ArrayPool/buffer pooling
- [ ] Network benchmarks

## Phase 6 — Generated Packet Dispatcher

- [x] Packet handler interface
- [x] Generated opcode lookup
- [ ] Handler registration
- [x] Unknown opcode handling
- [x] Session-state validation
- [x] Direction validation
- [x] Handler exception isolation
- [ ] Correlated logging
- [ ] Packet metrics

## Phase 7 — Legacy Protocol Research

- [ ] Extract packet headers from legacy reference source (ongoing)
- [x] Document handshake flow from reference implementation
- [x] Document phase packet and reference wire values
- [x] Document auth login request reference layout
- [x] Document auth login success/failure reference layouts
- [x] Document game TokenLogin reference layout and Login phase
- [x] Document legacy fixed-string behavior
- [x] Document legacy header/payload/sequence framing behavior
- [x] Document classic sequence progression/validation algorithm
- [x] Document classic TokenLogin plaintext/security activation boundary
- [ ] Select and verify target client sequence table/profile
- [x] Add reference-confirmed Handshake YAML
- [x] Add reference-confirmed Phase YAML
- [x] Add reference-confirmed LoginRequest YAML
- [x] Add reference-confirmed LoginSuccess YAML
- [x] Add reference-confirmed LoginFailed YAML
- [x] Add reference-confirmed TokenLogin YAML
- [ ] Extract character selection protocol
- [ ] Extract game-enter protocol
- [ ] Extract movement packets
- [ ] Extract spawn/despawn packets
- [ ] Extract chat packets
- [ ] Extract combat packets
- [ ] Extract item packets
- [ ] Extract skill packets
- [ ] Verify struct packing and endianness against original source
- [ ] Compare handshake/login with real client packet captures
- [ ] Verify target-client encryption mode and first post-TokenLogin encrypted traffic

## Phase 8 — Handshake

- [x] Reference-confirmed legacy handshake packet definition
- [ ] Compatibility-verified legacy handshake packet definition
- [x] Handshake handler
- [x] Connection state machine
- [x] Live accepted-socket handshake composition
- [x] Legacy phase announcements (`FD 01`, `FD 0A`, `FD 02`)
- [ ] Protocol version handling
- [x] Reject invalid handshake
- [x] Test client
- [ ] Verify real Metin2 client connection

## Phase 9 — Authentication

- [x] Auth module application core
- [x] Game login application core
- [ ] Account model
- [x] Password hashing strategy
- [x] Login request wire handler
- [x] Login application service
- [x] Credential verifier port
- [x] Auth token issuer port
- [x] Auth token consumer port
- [x] One-time Auth -> Game token handoff
- [x] Reference-confirmed login request/result packet definitions
- [x] Same-socket Handshake -> Auth login composition
- [x] Game TokenLogin wire handler
- [x] Same-socket Handshake -> Game Login composition
- [x] Session authentication state
- [x] Preserve 4-DWORD client security key after TokenLogin
- [x] PostgreSQL -> Game login consume-once/replay integration coverage
- [ ] Duplicate-login policy
- [ ] Account-status/client-status mapping
- [ ] Brute-force/rate limiting policy
- [x] Auth integration tests with deterministic sequence profile
- [ ] Configure verified production client sequence profile
- [ ] Enable post-TokenLogin encrypted transport

## Phase 10 — PostgreSQL

- [x] PostgreSQL Docker setup
- [x] Migration strategy
- [x] Account schema
- [x] Auth token schema and one-time store
- [ ] Character schema
- [x] Connection pooling via `NpgsqlDataSource`
- [ ] Repository contracts
- [x] PostgreSQL account credential verifier
- [x] Persistence integration tests against live PostgreSQL
- [ ] Query timing metrics
- [ ] Health check

## Phase 11 — Characters

- [ ] Character module
- [ ] Character model
- [ ] Character repository
- [ ] Character list
- [ ] Character create
- [ ] Character delete
- [ ] Character select
- [ ] Character protocol mapping
- [ ] Integration tests

## Phase 12 — World Foundation

- [ ] World module
- [ ] WorldEntity
- [ ] Position
- [ ] Rotation
- [ ] MapInstance
- [ ] Entity registry
- [ ] Character world enter/leave
- [ ] Spawn/despawn
- [ ] Nearby entity query

## Phase 13 — World Loop

- [ ] Choose fixed update rate
- [ ] World command queue
- [ ] Network-to-world command bridge
- [ ] Tick scheduler
- [ ] Tick metrics
- [ ] Tick overrun detection
- [ ] Graceful shutdown
- [ ] Deterministic test utilities

## Phase 14 — Spatial Grid

- [ ] Cell structure
- [ ] Insert/move/remove entity
- [ ] Radius query
- [ ] Nearby player query
- [ ] Visibility range
- [ ] Benchmarks
- [ ] Large entity-count tests

## Phase 15 — Movement

- [ ] Movement packet definitions
- [ ] Movement validation
- [ ] Map boundary validation
- [ ] Movement speed validation
- [ ] Position authority
- [ ] Movement broadcast
- [ ] Nearby replication
- [ ] Teleport foundation
- [ ] Exploit tests

## Milestone 1

- [ ] Original client connects
- [ ] Handshake completes
- [ ] Login succeeds
- [ ] Character list appears
- [ ] Character can be selected
- [ ] Character enters Map1
- [ ] Character spawns
- [ ] Character can move
- [ ] Nearby player movement replicates

## Later Gameplay Phases

- [ ] Monsters and AI
- [ ] Combat
- [ ] Stats and progression
- [ ] Items
- [ ] Inventory
- [ ] Equipment
- [ ] Drops
- [ ] Skills
- [ ] Affects
- [ ] NPC and shops
- [ ] Trade
- [ ] Party
- [ ] Guild
- [ ] Quest engine
- [ ] Dungeons
- [ ] Events
- [ ] Admin API
- [ ] Content hot reload
- [ ] Observability dashboards
- [ ] Architecture enforcement
- [ ] Performance/load validation
- [ ] Production deployment

## Permanent Engineering Rules

- [ ] Legacy implementation was not copy-pasted
- [ ] Feature lives in the correct module
- [ ] Domain does not depend on infrastructure
- [ ] Packet definitions remain the single source of truth
- [ ] No reflection-based packet codec
- [ ] Hot paths avoid unnecessary allocations
- [ ] Strong IDs used where appropriate
- [ ] Server remains authoritative
- [ ] Tests were added/updated
- [ ] Metrics/logging impact considered
- [ ] Public APIs remain minimal
- [ ] New dependency is actually justified
- [ ] No premature microservice split
- [ ] Architecture tests pass
