# Packet Definition Schema v1

Packet YAML files are the protocol single source of truth.

## Top-level structure

```yaml
schema: 1
protocol: legacy-metin2
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

The current legacy fixed-string codec path implements ASCII + null termination, which matches the inspected login protocol reference. Other declared schema combinations remain future codec work.

A null-terminated fixed field reserves one byte for its terminator. A 31-byte field therefore accepts at most 30 ASCII bytes in the current writer.

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

The current fixed codec path supports scalar primitive elements and generates `ReadOnlyMemory<T>` packet fields.

Count-based arrays remain future work:

```yaml
- name: characters
  type: array
  lengthFrom: character_count
  maxLength: 4
  element:
    type: CharacterSummary
```

Complex/nested structure declarations will be introduced only when a real protocol requirement justifies them.

## Field ordering

YAML field order is wire order and must be preserved exactly.

## Validation rules

Build diagnostics cover or are expected to cover:

- duplicate packet names/opcodes
- unsupported direction/phase/wire type
- invalid generated C# identifiers or collisions
- invalid fixed-string length/encoding/termination policy
- fixed arrays without positive length/element metadata
- unsupported fixed-array element types
- variable fields without bounds
- invalid `lengthFrom`
- incompatible `domainType`/wire primitive
- invalid version ranges
- packet-size overflow

## Security rules

Variable-size definitions require explicit upper bounds. Generated readers reject malformed/truncated payloads before unsafe advancement/allocation. Generated fixed writers prevalidate capacity and fixed-field shape before writing packet data.

## What does not belong in YAML

Do not encode gameplay rules, database mappings, permissions, service names, login policy or quest logic in protocol YAML. YAML describes the wire contract only.

## Legacy research

Reference-derived values must carry evidence/confidence and must not import legacy implementation architecture.

Current research note:

```text
docs/protocol/LEGACY_HANDSHAKE_AUTH.md
```
