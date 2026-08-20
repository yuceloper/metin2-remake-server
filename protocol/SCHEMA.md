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

Important: generated `PacketPhase` values are internal dispatcher metadata. They must not be confused with any protocol packet that happens to contain a phase byte. Legacy Metin2 GCPhase wire values are documented separately.

### `size`

- `fixed`: payload size is known at generation time.
- `variable`: one or more fields determine payload length.

### `sequence`

Optional boolean, default `false`.

```yaml
sequence: true
```

This metadata tells a framing/session profile that the packet carries protocol-specific sequence framing. It does not add a normal payload field and is not encoded by payload codecs.

For the researched legacy Metin2 profile, a sequenced packet has one trailing sequence byte after the payload.

### Version metadata

Optional:

```yaml
since: 1
until: 3
```

`since` is inclusive. `until` is inclusive when present.

## Payload vs framing

Generated packet codecs operate on packet **payload only**.

They do not silently encode:

- transport framing,
- packet header/opcode bytes,
- subheaders,
- sequence bytes,
- encryption envelopes.

For a researched fixed legacy Metin2 frame:

```text
[ header:u8 ][ payload ][ optional sequence:u8 ]
```

A generated fixed codec exposes `PayloadSize`.

The legacy frame layer can therefore compute:

```text
FrameSize = 1 + PayloadSize + (HasSequence ? 1 : 0)
```

This rule belongs to the legacy framing profile and must not constrain future/native protocol profiles.

## Generated model metadata

Generated packet models contain packet data only. Protocol metadata is emitted into a separate generated `<PacketName>Metadata` class so real payload fields such as `phase`, `opcode`, or `direction` can never collide with metadata members.

## Primitive wire types

Endianness is explicit whenever width is greater than one byte.

```text
u8
i8
u16le
u16be
i16le
i16be
u32le
u32be
i32le
i32be
u64le
u64be
i64le
i64be
f32le
f32be
f64le
f64be
bool8
```

The generator must never use machine-native endianness.

## Strong/domain IDs

Domain meaning is independent of wire storage.

```yaml
- name: character_id
  type: u32le
  domainType: CharacterId
```

The generated C# packet model may expose `CharacterId`, while its codec reads/writes an unsigned 32-bit little-endian integer.

`domainType` does not change packet size. Domain/wire width compatibility is validated at generation time.

## Fixed strings

```yaml
- name: username
  type: fixed_string
  length: 31
  encoding: ascii
  termination: null
  trim: null
```

Required:

- `length`: byte capacity on the wire
- `encoding`: initially `ascii`, `utf8`, or `latin1`

`termination`:

- `null`
- `none`

`trim` controls bytes stripped after decode. `null` means trim trailing zero bytes.

The generator must validate encoded length before serialization.

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

Fixed arrays:

```yaml
- name: points
  type: array
  length: 4
  element:
    type: u32le
```

Count-based arrays:

```yaml
- name: characters
  type: array
  lengthFrom: character_count
  maxLength: 4
  element:
    type: CharacterSummary
```

Complex/nested structures will be declared in `types` once required. V1 intentionally keeps the initial generator surface small.

## Field ordering

Field order in YAML is wire order and is therefore significant.

The generator must preserve it exactly.

## Validation rules

A build diagnostic must be emitted for at least:

- duplicate packet name
- invalid or duplicate opcode for the same namespace
- unsupported direction or phase
- unsupported wire type
- missing explicit endianness
- fixed packet containing an unbounded variable field
- invalid fixed-string length
- variable field without a maximum length
- `lengthFrom` referring to a later/nonexistent field
- `domainType` incompatible with the declared wire primitive
- duplicate field names
- generated C# identifier collisions
- invalid protocol version range
- integer or packet size overflow

## Security rules

Definitions of variable-size data must always provide a maximum bound.

Generated readers must reject malformed/truncated payloads and lengths that exceed declared maximums before allocating or advancing the reader.

## What does not belong in YAML

Do not put these in protocol definitions:

- damage formulas
- login rules
- movement validation
- database mappings
- quest logic
- permissions
- service names

Packet YAML describes the wire contract, not application behavior.

## Legacy research

Values discovered from reference implementations must be documented with evidence/confidence and then represented in this schema. Do not copy legacy implementation architecture into generated or handwritten server code.

Current research notes:

```text
docs/protocol/LEGACY_HANDSHAKE_AUTH.md
```
