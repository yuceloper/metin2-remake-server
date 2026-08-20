# ADR 0001: Use strict YAML for packet definitions

- Status: Accepted
- Date: 2026-08-20

## Context

The server needs a declarative protocol source of truth that can drive packet models, serializers, deserializers, opcode registries, validation, documentation and later tooling.

The legacy Metin2 source is only a protocol and behavior reference. Its C/C++ structs must not become the new architecture.

We considered:

1. Hand-written C# packet classes
2. A custom packet DSL
3. YAML packet definitions with a strict schema

## Decision

Use YAML files with a strict, versioned schema.

Packet definitions describe wire concerns only: opcode, direction, connection phase, size model and ordered fields.

Domain semantics are expressed separately through optional metadata such as `domainType`. For example, a CharacterId can be represented on the wire as `u32le` without making the domain type a primitive integer.

The generator must treat field order as significant and must never depend on host endianness.

## Why YAML

- Easy to review in pull requests
- Friendly for protocol research and hand-authored compatibility mappings
- No custom lexer/parser language to maintain
- Supports comments during reverse-engineering work
- Suitable as Roslyn `AdditionalFiles` input
- Can be validated against a formal schema

## Consequences

### Positive

- Packet definitions become the single source of truth.
- Generated codecs can be allocation-conscious and reflection-free.
- Legacy wire layouts remain isolated from domain models.
- Protocol documentation can be generated from the same definitions.
- Invalid or ambiguous definitions can fail the build through generator diagnostics.

### Negative

- The generator needs a deterministic YAML parsing strategy.
- YAML's flexible syntax must be constrained by our own schema.
- Schema evolution must be explicitly versioned.

## Rejected alternatives

### Hand-written packet classes

Rejected because packet model, codec, validation and documentation would drift over time.

### Custom DSL

Rejected for the initial implementation because it adds lexer/parser/tooling maintenance without providing enough benefit over strict YAML. A custom DSL may only be reconsidered if concrete limitations appear later.

## Rules

- No gameplay logic in protocol definitions.
- No runtime reflection-based serialization.
- No implicit endianness.
- No packet definition may infer a domain primitive representation.
- Schema-breaking changes require an ADR and schema-version change.
