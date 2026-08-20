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
- [ ] Verify Debug/Release builds
- [ ] Verify Ubuntu build
- [x] Add Docker build

## Phase 1 — Shared Kernel

- [ ] CharacterId
- [ ] AccountId
- [ ] ItemId
- [ ] GuildId
- [ ] MapId
- [ ] EntityId
- [ ] MonsterId
- [ ] Minimal result/error abstractions
- [ ] Domain event primitives

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
- [ ] Generate packet registry
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

- [ ] TCP listener
- [ ] Socket lifecycle
- [ ] Connection abstraction
- [ ] GameSession
- [ ] System.IO.Pipelines receive pipeline
- [ ] Send pipeline
- [ ] Legacy one-byte header framing profile
- [ ] Optional trailing sequence-byte framing
- [ ] Frame decoder/encoder
- [ ] Packet decoder/encoder
- [ ] Graceful disconnect
- [ ] Cancellation handling
- [ ] Connection timeout
- [ ] Maximum packet size
- [ ] Invalid packet handling
- [ ] Base rate limiting
- [ ] ArrayPool/buffer pooling
- [ ] Network benchmarks

## Phase 6 — Generated Packet Dispatcher

- [ ] Packet handler interface
- [ ] Generated opcode lookup
- [ ] Handler registration
- [ ] Unknown opcode handling
- [ ] Session-state validation
- [ ] Direction validation
- [ ] Handler exception isolation
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
- [ ] Resolve sequence progression/validation semantics
- [ ] Verify encryption activation boundary around TokenLogin

## Phase 8 — Handshake

- [x] Reference-confirmed legacy handshake packet definition
- [ ] Compatibility-verified legacy handshake packet definition
- [ ] Handshake handler
- [ ] Connection state machine
- [ ] Protocol version handling
- [ ] Reject invalid handshake
- [ ] Test client
- [ ] Verify real Metin2 client connection

## Phase 9 — Authentication

- [ ] Auth module
- [ ] Account model
- [ ] Password hashing strategy
- [ ] Login request handler
- [ ] Login application service
- [x] Reference-confirmed login request/result packet definitions
- [ ] Session authentication state
- [ ] Duplicate-login policy
- [ ] Brute-force/rate limiting policy
- [ ] Auth integration tests

## Phase 10 — PostgreSQL

- [ ] PostgreSQL Docker setup
- [ ] Migration strategy
- [ ] Account schema
- [ ] Character schema
- [ ] Connection pooling
- [ ] Repository contracts
- [ ] Persistence integration tests
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
