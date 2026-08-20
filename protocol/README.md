# Protocol Workspace

This directory is the single source of truth for declarative packet definitions.

The legacy Metin2 source may be consulted to discover packet layouts, opcodes and behavior, but legacy implementation code is not copied into the new server.

## Format

Packet definitions use strict YAML schema version 1.

See:

- `SCHEMA.md` for the human-readable contract
- `schema/packet.schema.json` for machine-readable validation
- `examples/` for non-authoritative schema examples
- `../docs/adr/0001-use-yaml-for-packet-definitions.md` for the architecture decision

## Planned source layout

```text
protocol/
  client/
  server/
  common/
  schema/
  examples/
```

Real legacy-compatible packet definitions will be added only after their layouts are verified during protocol research.

## Core rule

Wire representation and domain semantics are separate.

For example, a `CharacterId` may be represented by `u32le` on a particular protocol without making the domain model depend on that primitive representation.
