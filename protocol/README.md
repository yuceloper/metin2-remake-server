# Protocol Workspace

This directory is the single source of truth for declarative packet definitions.

The legacy Metin2 source may be consulted to discover packet layouts, opcodes and behavior, but legacy implementation code is not copied into the new server.

Planned structure:

```text
protocol/
  client/
  server/
  common/
```

The concrete schema format (YAML vs custom DSL) is intentionally deferred to Phase 2.
