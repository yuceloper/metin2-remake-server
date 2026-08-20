# Legacy Metin2 Handshake & Auth Protocol Research

Status: **reference-confirmed, compatibility verification pending**

This document records protocol evidence only. The referenced project is not an architectural dependency and its implementation must not be copied into the remake.

## Evidence policy

Confidence labels used here:

- **reference-confirmed** — directly represented in the inspected `yuceloper/new-metin` reference source.
- **compatibility-verified** — confirmed against the original legacy source and/or a real client packet capture. Nothing in this document has this status yet.

## Framing boundary

The inspected reference implementation reads the packet header separately from packet payload data.

For the legacy profile:

```text
[ header: u8 ][ payload: N bytes ][ optional sequence: u8 ]
```

The generated packet codec is responsible for **payload only**.

The networking/framing layer is responsible for:

- reading/writing the legacy one-byte header,
- selecting a packet definition from opcode + connection state,
- reading/writing the optional trailing sequence byte,
- ensuring the full frame is available before dispatch.

Therefore generated fixed codecs expose `PayloadSize`, not total frame size.

For a fixed legacy packet:

```text
FrameSize = 1 + PayloadSize + (HasSequence ? 1 : 0)
```

This is a legacy framing rule, not a universal rule for future/native protocol profiles.

## Handshake packet

Evidence source:

```text
yuceloper/new-metin
src/Core/Core/Packets/GCHandshake.cs
```

Reference definition:

```text
Header:    0xFF
Direction: bidirectional
Handshake: uint32
Time:      uint32
Delta:     uint32
```

The inspected serializer writes multi-byte integer values least-significant byte first, therefore the current YAML represents these fields as `u32le`.

Payload size: **12 bytes**.
Legacy frame size without sequence: **13 bytes**.

The canonical remake definition is:

```text
protocol/common/legacy-handshake.packet.yml
```

### Handshake lifecycle

Evidence source:

```text
yuceloper/new-metin
src/Core/Core/Networking/Connection.cs
```

Observed behavior:

1. A newly established TCP connection starts handshaking.
2. Server generates a random 32-bit handshake token.
3. Connection state is set to Handshake.
4. Server sends `Handshake(token, serverTime, 0)`.
5. Client responds using the same handshake token plus its time/delta values.
6. A mismatching token closes the connection.
7. Server computes:

```text
difference = serverTime - (clientTime + clientDelta)
```

8. When difference is in the accepted range `0..50 ms`, handshaking completes.
9. Otherwise a new delta is calculated and another handshake packet is sent.

The exact timing algorithm is recorded as behavior evidence, but should be re-evaluated when implementing the new session state machine rather than copied mechanically.

## Phase packet

Evidence sources:

```text
yuceloper/new-metin
src/Core/Core/Packets/GCPhase.cs
src/CorePluginAPI/Game/Types/EPhases.cs
src/Core/Extensions/ConnectionExtensions.cs
```

Reference definition:

```text
Header:    0xFD
Direction: server -> client
Payload:   phase:u8
```

Reference wire phase values:

| Wire value | Meaning |
| ---: | --- |
| 1 | Handshake |
| 2 | Login |
| 3 | Select |
| 4 | Loading |
| 5 | Game |
| 10 | Auth |

Important: these are **legacy wire values**. They are not the numeric values of the remake generator's `PacketPhase` metadata enum. `PacketPhase` exists to constrain dispatcher state and must not be serialized as GCPhase implicitly.

The Auth reference server changes phase to `Auth` after the handshake completion callback, therefore the client receives a phase frame corresponding to:

```text
FD 0A
```

subject to final packet-capture/original-source verification.

## Auth login request

Evidence source:

```text
yuceloper/new-metin
src/Executables/Auth/Packets/LoginRequest.cs
```

Reference metadata:

```text
Header:      0x6F
Direction:   client -> server
Sequence:    true
Username:    fixed string, 31 bytes
Password:    fixed string, 17 bytes
EncryptKey:  4 x uint32
```

The reference serializer writes a sequence byte after payload data and counts it in total packet size. The reference packet reader separately consumes that trailing byte after payload deserialization.

Expected reference frame size from this definition:

```text
1 header
+ 31 username
+ 17 password
+ 16 encrypt key
+ 1 sequence
= 66 bytes
```

This LoginRequest is **not yet committed as a canonical packet YAML definition** because the remake generator still needs fixed-string and fixed-array codec support and the exact string termination/encoding behavior should be checked against original source/client traffic.

## Sequence behavior

Evidence sources:

```text
yuceloper/new-metin
src/Core.Networking/PacketAttribute.cs
src/Core.Networking.Generators/SerializeGenerator.cs
src/Core.Networking/PacketReader.cs
```

The reference implementation models `Sequence` as packet metadata.

When enabled:

- serialization appends one trailing byte after all normal packet fields,
- packet total size includes that byte,
- deserialization reads normal payload first,
- the reader then consumes one additional sequence byte.

At this research stage the byte's actual validation/progression semantics have not yet been confirmed. The remake therefore records only `sequence: true|false`; sequence generation/verification behavior belongs to the future legacy framing/session layer.

## Open verification items

Before declaring login compatibility complete:

- cross-check handshake struct and headers against original Metin2 source,
- capture a real client handshake/login session if practical,
- verify login fixed-string encoding,
- verify null termination/padding semantics,
- verify encrypt-key byte order,
- determine sequence-byte progression/validation semantics,
- verify whether client variants alter LoginRequest layout.

## Architecture consequence

No legacy `Connection`, reflection-based packet registry, generated serializer implementation, Redis dependency, plugin model, or thread model from the reference project is adopted.

Only the observed wire contract and behavior are carried forward as evidence.
