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

Generated packet codecs own **payload only**. The legacy framing/session layer owns the header, optional sequence byte, full-frame availability and sequence progression/validation.

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
termination: "null"
trim: "null"
```

## Phase — 0xFD

Evidence:

```text
src/Core/Core/Packets/GCPhase.cs
src/CorePluginAPI/Game/Types/EPhases.cs
src/Core/Extensions/ConnectionExtensions.cs
src/Executables/Auth/AuthServer.cs
src/Executables/Game/GameServer.cs
```

```text
Header:    0xFD
Direction: server -> client
Payload:   phase:u8
Sequence:  false
Frame:     2 bytes
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

These values are legacy wire values and are intentionally independent of the generator's internal `PacketPhase` dispatcher metadata enum.

`ConnectionExtensions.SetPhase` first updates the connection phase and then sends `GCPhase`. `Connection.StartHandshake()` calls `SetPhase(Handshake)` before it sends the first Handshake packet. Therefore the reference-confirmed initial wire order is:

```text
FD 01
FF <handshake payload>
```

After handshake completion:

- AuthServer calls `SetPhase(Auth)`, producing `FD 0A`.
- GameServer calls `SetPhase(Login)`, producing `FD 02`.

A handshake time-resynchronization retry does **not** change phase; it emits another `0xFF` Handshake only.

Canonical definition:

```text
protocol/server/legacy-phase.packet.yml
```

The remake models these values explicitly as `LegacyPhaseCode` rather than relying on internal enum ordinal values.

## Handshake — 0xFF

Evidence:

```text
src/Core/Core/Packets/GCHandshake.cs
src/Core/Core/Networking/Connection.cs
src/Core/Extensions/ConnectionExtensions.cs
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
3. Server announces Handshake phase with `FD 01`.
4. Server sends `Handshake(token, serverTime, 0)` as `0xFF`.
5. Client responds with the same token plus time/delta.
6. Wrong token closes the connection.
7. Server computes `serverTime - (clientTime + clientDelta)`.
8. A difference in the reference acceptance range `0..50 ms` completes the handshake.
9. Otherwise another `0xFF` Handshake is emitted with the reference retry delta; phase remains Handshake.
10. On completion Auth announces `FD 0A`, while game announces `FD 02`.

Reference `serverTime` is monotonic elapsed milliseconds from a server-started `Stopwatch`, not wall-clock or Unix time.

The behavior is evidence, not code to copy mechanically.

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

The game connection runs the shared handshake first. After handshake completion, `GameServer` sets the connection to legacy wire Login phase (`FD 02`). TokenLogin is therefore modeled with dispatcher phase `login`.

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

The earlier `yuceloper/new-metin` reference is useful only for confirming the existence and framing position of the trailing sequence byte. Its serializer writes a default zero byte and its reader consumes the byte without validation, so it is **not** sufficient evidence for the real legacy progression algorithm.

Classic server/client source establishes the actual progression model:

- the connection sequence index begins at `0`,
- a sequenced packet carries one trailing byte after its payload,
- the server expects `sequenceTable[currentIndex]`,
- a mismatch rejects/closes the connection and does **not** advance the index,
- a match advances the index by one,
- the index wraps to `0` at the sequence-table length,
- the client mirrors the same table/index progression when sending a sequence byte,
- clearing/resetting the client connection resets the sequence index to `0`.

Classic 40250-era public source uses a table size of `32768` bytes (`SEQUENCE_MAX_NUM` / `SEQUENCE_TABLE_SIZE`). The public table commonly associated with that source begins:

```text
AF CA 8A CF 48 A7 54 C7 ...
```

However, public client/source variants exist with different sequence tables. Therefore the remake does **not** treat that 32768-byte table as universal Metin2 protocol data. The progression algorithm is now understood, but the exact table remains a **client-build compatibility profile** that must be selected and verified against the stock client we target.

Architecture consequence:

- `LegacySequenceProfile` owns an explicit immutable table supplied by the selected compatibility profile.
- `LegacySequenceState` is connection-local and starts at index `0`.
- the receive path validates a configured sequence before typed dispatch.
- no default sequence profile is configured until the target client build/table is verified.

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

- cross-check headers/layouts and phase ordering against original Metin2 source,
- capture real client handshake/auth/game-login traffic where practical,
- verify client-build differences,
- select and verify the exact sequence table/profile for the target stock client,
- verify exact key byte order against original source/traffic,
- verify encryption activation boundary around TokenLogin.

## Architecture consequence

No legacy `Connection`, reflection packet registry, serializer generator architecture, singleton/server pattern, Redis/plugin model or threading design is adopted.

Only observed wire contract and behavior are carried forward as evidence.
