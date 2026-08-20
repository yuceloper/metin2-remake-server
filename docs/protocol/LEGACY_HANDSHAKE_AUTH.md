# Legacy Metin2 Handshake, Auth & Game Login Research

Status: **reference-confirmed, compatibility verification pending**

This document records protocol evidence only. The inspected `yuceloper/new-metin` implementation is not an architectural dependency and its design is not copied into the remake.

## Evidence policy

- **reference-confirmed** — directly represented in inspected reference source.
- **compatibility-verified** — confirmed against original legacy source and/or real client packet capture.

Nothing here is compatibility-verified yet.

## Legacy framing boundary

The reference reader/serializer establishes this legacy frame shape:

```text
[ header:u8 ][ payload:N ][ optional sequence:u8 ]
```

Generated packet codecs own **payload only**. The future legacy framing/session layer owns the header, optional sequence byte, full-frame availability and sequence progression/validation.

For a fixed legacy packet:

```text
FrameSize = 1 + PayloadSize + (HasSequence ? 1 : 0)
```

This rule belongs only to the legacy framing profile.

## Fixed string behavior

Evidence:

```text
src/Core.Networking/SerializerExtensions.cs
```

The reference implementation uses ASCII fixed-width buffers:

- read exactly the declared byte width,
- decode ASCII,
- stop at the first `0x00` when present,
- write into the full fixed-width region,
- reserve/set the final byte as `0x00`,
- unused bytes are zero-filled by the serializer buffer.

Therefore a 31-byte field safely carries at most 30 ASCII bytes plus its terminator.

The remake intentionally rejects an overlong value before writing instead of silently truncating. Valid values remain byte-compatible while malformed application input cannot partially corrupt an outgoing packet.

Canonical representation:

```yaml
type: fixed_string
length: 31
encoding: ascii
termination: null
trim: null
```

## Handshake — 0xFF

Evidence:

```text
src/Core/Core/Packets/GCHandshake.cs
src/Core/Core/Networking/Connection.cs
```

```text
Header:    0xFF
Direction: bidirectional
Payload:   handshake:u32 + time:u32 + delta:u32
Endian:    little
Sequence:  false
Payload:   12 bytes
Frame:     13 bytes
```

Canonical definition:

```text
protocol/common/legacy-handshake.packet.yml
```

Observed lifecycle:

1. TCP connection begins in handshake state.
2. Server creates a random 32-bit handshake token.
3. Server sends `Handshake(token, serverTime, 0)`.
4. Client responds with the same token plus time/delta.
5. Wrong token closes the connection.
6. Server computes `serverTime - (clientTime + clientDelta)`.
7. A difference in the reference acceptance range `0..50 ms` completes the handshake; otherwise another delta/handshake round occurs.

The behavior is evidence, not code to copy mechanically.

## Phase — 0xFD

Evidence:

```text
src/Core/Core/Packets/GCPhase.cs
src/CorePluginAPI/Game/Types/EPhases.cs
src/Core/Extensions/ConnectionExtensions.cs
```

```text
Header:    0xFD
Direction: server -> client
Payload:   phase:u8
Sequence:  false
```

Reference wire phase values:

| Value | Meaning |
| ---: | --- |
| 1 | Handshake |
| 2 | Login |
| 3 | Select |
| 4 | Loading |
| 5 | Game |
| 10 | Auth |

These values are legacy wire values and are intentionally independent of the generator's `PacketPhase` dispatcher metadata enum.

After auth handshake completion the reference server changes the client to Auth, giving the expected reference frame `FD 0A` pending final traffic/original-source verification.

Canonical definition:

```text
protocol/server/legacy-phase.packet.yml
```

## Auth LoginRequest — 0x6F

Evidence:

```text
src/Executables/Auth/Packets/LoginRequest.cs
src/Core.Networking/SerializerExtensions.cs
src/Core.Networking.Generators/SerializeGenerator.cs
src/Core.Networking/PacketReader.cs
```

```text
Header:      0x6F
Direction:   client -> server
Phase:       Auth
Sequence:    true
Username:    ASCII fixed[31]
Password:    ASCII fixed[17]
EncryptKey:  u32le[4]
Payload:     64 bytes
Frame:       66 bytes
```

Canonical definition:

```text
protocol/client/legacy-login-request.packet.yml
```

## Auth LoginSuccess — 0x96

Evidence:

```text
src/Executables/Auth/Packets/LoginSuccess.cs
```

```text
Header:     0x96
Direction:  server -> client
Phase:      Auth
Sequence:   false
Key:        u32le
Result:     u8
Payload:    5 bytes
Frame:      6 bytes
```

Canonical definition:

```text
protocol/server/legacy-login-success.packet.yml
```

## Auth LoginFailed — 0x07

Evidence:

```text
src/Executables/Auth/Packets/LoginFailed.cs
src/Core.Networking/SerializerExtensions.cs
```

```text
Header:     0x07
Direction:  server -> client
Phase:      Auth
Sequence:   false
Unknown:    u8
Status:     ASCII fixed[9]
Payload:    10 bytes
Frame:      11 bytes
```

Canonical definition:

```text
protocol/server/legacy-login-failed.packet.yml
```

## Game TokenLogin — 0x6D

Evidence:

```text
src/Executables/Game/Packets/TokenLogin.cs
src/Executables/Game/PacketHandlers/TokenLoginHandler.cs
src/Executables/Game/GameConnection.cs
src/Executables/Game/GameServer.cs
```

The game connection runs the shared handshake first. After handshake completion, `GameServer`'s new-connection listener sets the connection to the reference `Login` phase. TokenLogin is therefore modeled with dispatcher phase `login`.

```text
Header:     0x6D
Direction:  client -> server
Phase:      Login
Sequence:   true
Username:   ASCII fixed[31]
Key:        u32le
XteaKey:    u32le[4]
Payload:    51 bytes
Frame:      53 bytes
```

Canonical definition:

```text
protocol/client/legacy-token-login.packet.yml
```

After a valid token, the inspected handler sets encryption/session data, marks the session logged in, sends empire information, changes phase to Select and sends the character list. This flow remains behavior evidence only.

## Sequence behavior

Reference metadata marks individual packet types as sequenced. When enabled:

- serialization writes the normal header,
- writes payload fields,
- appends one trailing sequence byte,
- total frame size includes that byte,
- deserialization reads header and payload separately, then consumes sequence.

The actual sequence progression and validation algorithm remains unresolved and belongs to the future legacy framing/session implementation.

## Current canonical reference-confirmed packets

```text
protocol/common/legacy-handshake.packet.yml
protocol/server/legacy-phase.packet.yml
protocol/client/legacy-login-request.packet.yml
protocol/server/legacy-login-success.packet.yml
protocol/server/legacy-login-failed.packet.yml
protocol/client/legacy-token-login.packet.yml
```

## Open compatibility verification

Before declaring login compatibility complete:

- cross-check headers/layouts against original Metin2 source,
- capture real client handshake/auth/game-login traffic where practical,
- verify client-build differences,
- verify exact key byte order against original source/traffic,
- determine sequence progression/validation,
- verify encryption activation boundary around TokenLogin.

## Architecture consequence

No legacy `Connection`, reflection packet registry, serializer generator architecture, singleton/server pattern, Redis/plugin model or threading design is adopted.

Only observed wire contract and behavior are carried forward as evidence.
