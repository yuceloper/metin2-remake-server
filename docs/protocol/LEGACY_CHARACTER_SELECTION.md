# Legacy Metin2 Character Selection Research

Status: **reference-confirmed, compatibility verification pending**

This document records wire-contract evidence only. Legacy/reference implementations are specifications of observed behavior, not architectural dependencies for the remake.

## Evidence

Inspected references include:

- classic client `UserInterface/Packet.h`
- `yuceloper/new-metin` packet declarations and Select-phase handlers

The relevant classic packet structures are declared under `#pragma pack(1)`. Therefore their wire layouts contain no compiler alignment padding.

## CharacterSummary / TSimplePlayerInformation

The reusable packed character summary used by the four-slot character list has this wire order:

| Field | Wire type | Bytes |
| --- | --- | ---: |
| id | u32le | 4 |
| name | fixed ASCII null-terminated[25] | 25 |
| class/job | u8 | 1 |
| level | u8 | 1 |
| playtime | u32le | 4 |
| ST | u8 | 1 |
| HT | u8 | 1 |
| DX | u8 | 1 |
| IQ | u8 | 1 |
| body/main part | u16le | 2 |
| name-change flag | u8 | 1 |
| hair part | u16le | 2 |
| unknown/dummy DWORD | u32le | 4 |
| position X | i32le | 4 |
| position Y | i32le | 4 |
| address/IP | i32le | 4 |
| port | u16le | 2 |
| skill group | u8 | 1 |

Packed size: **63 bytes**.

Canonical reusable wire type:

```text
protocol/server/legacy-characters.packet.yml -> types.CharacterSummary
```

The remake generates a strongly typed readonly record struct and compile-time codec rather than representing this structure as an opaque 63-byte blob.

## Characters / LoginSuccess4 — 0x20

```text
Header:       0x20
Direction:    server -> client
Phase:        Select
Sequence:     false
Characters:   CharacterSummary[4]
Guild IDs:    u32le[4]
Guild names:  fixed ASCII null-terminated[13] × 4
Handle:       u32le
Random key:   u32le
Payload:      328 bytes
Frame:        329 bytes
```

Size derivation:

```text
4 * 63  CharacterSummary = 252
4 * 4   guild IDs        =  16
4 * 13  guild names      =  52
2 * 4   handle/key       =   8
--------------------------------
payload                    328
header                        1
frame                        329
```

Canonical definition:

```text
protocol/server/legacy-characters.packet.yml
```

## Empire — 0x5A

The inspected reference declares Empire as bidirectional and sequenced:

```text
Header:     0x5A
Direction:  bidirectional
Payload:    empire_id:u8
Sequence:   true
```

It participates in character-selection lifecycle behavior. The current dispatcher metadata uses phase `any` because evidence shows the packet can occur at more than one lifecycle point; this is protocol-state metadata, not a relaxation of application validation.

Canonical definition:

```text
protocol/common/legacy-empire.packet.yml
```

## SelectCharacter — 0x06

Classic client `HEADER_CG_PLAYER_SELECT` is `6`. The inspected reference packet contains only a slot byte and uses sequence framing.

```text
Header:     0x06
Direction:  client -> server
Phase:      Select
Payload:    slot:u8
Sequence:   true
Payload:    1 byte
Frame:      3 bytes
```

After a valid selection, the inspected reference moves the connection to Loading phase before loading/sending the selected player's data.

Canonical definition:

```text
protocol/client/legacy-select-character.packet.yml
```

## EnterGame — 0x0A

Classic client `HEADER_CG_ENTERGAME` is `10`. The packet has no payload fields and is sequenced.

```text
Header:     0x0A
Direction:  client -> server
Phase:      Loading
Payload:    empty
Sequence:   true
Payload:    0 bytes
Frame:      2 bytes
```

Canonical definition:

```text
protocol/client/legacy-enter-game.packet.yml
```

## Generator consequence

`Characters` is the first real packet requiring reusable nested fixed-layout data. The protocol schema therefore supports a deliberately narrow composite model:

- top-level reusable `types:` declarations,
- fixed-size composites only,
- fixed primitive and fixed ASCII-null fields inside composites,
- packet fields and fixed arrays may reference a reusable composite,
- fixed arrays may also contain fixed strings with explicit element metadata,
- no recursion,
- no variable nested structures,
- no reflection,
- no opaque raw-byte workaround.

This keeps packet definitions as the wire-contract single source of truth while avoiding premature general-purpose serialization features.

## Open compatibility verification

Before declaring character selection stock-client compatible:

- verify these layouts against the exact target client build,
- capture real post-login character-list traffic where practical,
- verify the exact meaning and byte order of `handle` / `random_key`,
- verify the encryption boundary for post-TokenLogin server packets,
- verify Empire lifecycle/state expectations against the target client,
- verify Select -> Loading -> EnterGame sequencing against a real client.
