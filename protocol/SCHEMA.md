# Packet Definition Schema v1

Packet YAML files are the protocol single source of truth.

## Top-level structure

```yaml
schema: 1
protocol: legacy-metin2
types:
  - name: ExampleSummary
    size: fixed
    fields:
      - name: id
        type: u32le
packets:
  - name: ExamplePacket
    opcode: 0x01
    direction: client_to_server
    phase: game
    size: fixed
    sequence: false
    since: 1
    fields: []
```

`types:` is optional. It declares reusable fixed-layout wire structures that can be referenced by packet fields or fixed-array elements.

## Packet metadata

### `name`

Unique PascalCase packet name within the protocol.

### `opcode`

Unsigned protocol identifier. Hex notation is preferred for readability.

The generic schema allows `0..65535`. A framing profile may further restrict this. The legacy Metin2 framing profile uses a one-byte header, therefore legacy definitions must fit `u8` even though the generic model is not permanently coupled to that width.

Opcode uniqueness is validated within the applicable direction/phase namespace.

### `direction`

Allowed values:

- `client_to_server`
- `server_to_client`
- `bidirectional`

### `phase`

Connection-state metadata used for generated validation and dispatch. It is **not automatically serialized**.

Initial values:

- `handshake`
- `login`
- `auth`
- `select`
- `loading`
- `game`
- `any`

Generated `PacketPhase` values are internal dispatcher metadata. They are not legacy GCPhase wire values.

### `size`

- `fixed`: payload size is known at generation time.
- `variable`: one or more fields determine payload length.

### `sequence`

Optional boolean, default `false`.

```yaml
sequence: true
```

This tells a framing/session profile that the packet carries protocol-specific sequence framing. It is not a normal payload field and is not encoded by payload codecs.

### Version metadata

```yaml
since: 1
until: 3
```

Both bounds are inclusive.

## Payload vs framing

Generated packet codecs operate on **payload only**. They do not silently encode transport framing, header/opcode bytes, sequence bytes or encryption envelopes.

For the researched fixed legacy profile:

```text
[ header:u8 ][ payload ][ optional sequence:u8 ]
FrameSize = 1 + PayloadSize + (HasSequence ? 1 : 0)
```

This rule is legacy-profile specific.

## Generated model metadata

Generated packet models contain packet data only. Protocol metadata is emitted into a separate generated `<PacketName>Metadata` class so payload fields such as `phase`, `opcode` or `direction` cannot collide with metadata members.

Reusable fixed wire types are generated under `Metin2.Protocol.Generated.Types` as readonly record structs with compile-time codecs and `PayloadSize` constants.

## Primitive wire types

Endianness is explicit for multi-byte primitives:

```text
u8 i8
u16le u16be i16le i16be
u32le u32be i32le i32be
u64le u64be i64le i64be
f32le f32be f64le f64be
bool8
```

Machine-native endianness is never implied.

## Strong/domain IDs

```yaml
- name: character_id
  type: u32le
  domainType: CharacterId
```

The generated model exposes the domain type while the codec preserves the declared wire primitive. Domain/wire width compatibility is validated at generation time.

## Fixed strings

```yaml
- name: username
  type: fixed_string
  length: 31
  encoding: ascii
  termination: "null"
  trim: "null"
```

`"null"` is intentionally quoted: unquoted YAML `null` is a null value, not the string policy name.

Required metadata:

- `length`: byte capacity on the wire
- `encoding`: `ascii`, `utf8`, or `latin1` at schema level
- `termination`: `"null"` or `none`
- `trim`: `"null"` or `none` when specified

The current legacy fixed-string codec path implements ASCII + null termination, which matches the inspected login and character-selection protocol references. Other declared schema combinations remain future codec work.

A null-terminated fixed field reserves one byte for its terminator. A 31-byte field therefore accepts at most 30 ASCII bytes in the current writer.

## Reusable fixed composite types

A real protocol requirement (`Characters/LoginSuccess4`) introduced the first reusable packed structure, equivalent to classic `TSimplePlayerInformation`.

```yaml
types:
  - name: CharacterSummary
    size: fixed
    fields:
      - name: id
        type: u32le
        domainType: CharacterId
      - name: name
        type: fixed_string
        length: 25
        encoding: ascii
        termination: "null"
        trim: "null"
      - name: level
        type: u8
```

Schema v1 deliberately keeps composites narrow:

- only `size: fixed` reusable types are supported,
- composite fields must have compile-time-known width,
- current composite members are scalar primitives/domain IDs and supported fixed strings,
- packet fields may reference a declared composite directly,
- fixed arrays may reference a declared composite as their element type,
- recursive/nested composite declarations are rejected,
- variable nested fields are not supported,
- no runtime reflection or opaque-byte fallback is introduced.

The generated codec computes composite and containing packet sizes transitively at generation time.

## Variable strings

```yaml
- name: message
  type: string
  encoding: utf8
  lengthType: u16le
  maxLength: 1024
```

Variable strings always have an explicit length representation and maximum size.

## Raw bytes

Fixed:

```yaml
- name: token
  type: bytes
  length: 16
```

Count-based:

```yaml
- name: payload
  type: bytes
  lengthFrom: payload_length
  maxLength: 4096
```

## Arrays

Fixed primitive arrays:

```yaml
- name: key
  type: array
  length: 4
  element:
    type: u32le
```

Fixed composite arrays:

```yaml
- name: character_list
  type: array
  length: 4
  element:
    type: CharacterSummary
```

Fixed-string arrays carry string metadata on the element because each array item is an independent fixed-width field:

```yaml
- name: guild_names
  type: array
  length: 4
  element:
    type: fixed_string
    length: 13
    encoding: ascii
    termination: "null"
    trim: "null"
```

The current fixed codec path supports primitive, supported fixed-string and declared fixed-composite elements. Generated packet fields use `ReadOnlyMemory<T>`.

Count-based arrays remain future work:

```yaml
- name: entries
  type: array
  lengthFrom: entry_count
  maxLength: 128
  element:
    type: u32le
```

## Field ordering

YAML field order is wire order and must be preserved exactly.

Classic character-selection evidence uses `#pragma pack(1)`; generated codecs therefore express the declared byte sequence directly and do not insert C#/.NET structure padding.

## Validation rules

Build diagnostics cover or are expected to cover:

- duplicate packet names/opcodes
- duplicate/reserved reusable wire-type names
- unsupported direction/phase/wire type
- unknown composite references
- unsupported nested/recursive composite shapes
- invalid generated C# identifiers or collisions
- invalid fixed-string length/encoding/termination policy
- fixed arrays without positive length/element metadata
- unsupported fixed-array element types
- invalid fixed-string array element metadata
- variable fields without bounds
- invalid `lengthFrom`
- incompatible `domainType`/wire primitive
- invalid version ranges
- packet-size overflow

## Security rules

Variable-size definitions require explicit upper bounds. Generated readers reject malformed/truncated payloads before unsafe advancement/allocation. Generated fixed writers prevalidate capacity and fixed-field shape before writing packet data.

Generated dispatch targets provide a rejecting default implementation for packet types a target does not explicitly handle. Adding a new packet to the protocol SSOT therefore does not require unrelated Auth/Handshake targets to add meaningless stubs, while an actually dispatched unsupported packet still fails explicitly.

## What does not belong in YAML

Do not encode gameplay rules, database mappings, permissions, service names, login policy or quest logic in protocol YAML. YAML describes the wire contract only.

## Legacy research

Reference-derived values must carry evidence/confidence and must not import legacy implementation architecture.

Current research notes:

```text
docs/protocol/LEGACY_HANDSHAKE_AUTH.md
docs/protocol/LEGACY_CHARACTER_SELECTION.md
```
