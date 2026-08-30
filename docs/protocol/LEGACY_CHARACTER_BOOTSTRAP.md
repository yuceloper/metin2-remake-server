# Legacy Metin2 Character Bootstrap Research

Status: **reference-confirmed, compatibility verification pending**

This document records observed wire behavior and the remake boundary chosen around it. The legacy implementation is evidence for what the client expects, not an architecture to copy.

## Lifecycle

After a valid `SelectCharacter (0x06)` the inspected reference moves the connection to Loading and loads the selected account-owned player. Before `EnterGame (0x0A)` it sends:

1. `CharacterDetails (0x71)`
2. `CharacterPoints (0x10)`
3. `CharacterUpdate (0x13)`
4. populated quick slots, if any

The remake currently treats quick-slot persistence/bootstrap as a separate bounded concern. Empty quick slots require no packets in the inspected reference.

## CharacterDetails — 0x71

Reference: `yuceloper/new-metin src/Executables/Game/Packets/CharacterDetails.cs`

Server -> client, fixed, no trailing sequence in the inspected outgoing serializer.

| Field | Wire type | Bytes |
| --- | --- | ---: |
| vid | u32le | 4 |
| class | u16le | 2 |
| name | fixed ASCII null-terminated[25] | 25 |
| position_x | i32le | 4 |
| position_y | i32le | 4 |
| position_z | i32le | 4 |
| empire | u8 | 1 |
| skill_group | u8 | 1 |

Payload: **45 bytes**. Frame: **46 bytes**.

Canonical definition: `protocol/server/legacy-character-details.packet.yml`.

## CharacterPoints — 0x10

Reference: `yuceloper/new-metin src/Executables/Game/Packets/CharacterPoints.cs` and `EPoints.cs`.

Payload is a fixed array of **255 little-endian uint32 values**. Payload: **1020 bytes**. Frame: **1021 bytes**.

Known reference indices include:

| Index | Meaning |
| ---: | --- |
| 1 | Level |
| 3 | Experience |
| 4 | NeededExperience |
| 5 | HP |
| 6 | MaxHP |
| 7 | SP |
| 8 | MaxSP |
| 11 | Gold |
| 12 | ST |
| 13 | HT |
| 14 | DX |
| 15 | IQ |
| 16 | DefenceGrade |
| 17 | AttackSpeed |
| 18 | AttackGrade |
| 19 | MoveSpeed |
| 20 | Defence |
| 26 | Available status points |
| 29 | Min attack damage |
| 30 | Max attack damage |
| 40 | Critical percentage |
| 41 | Penetrate percentage |
| 93 | Attack bonus |
| 94 | Defence bonus |
| 132 | Magic attack bonus |
| 136 | Resist critical |
| 137 | Resist penetrate |
| 200 | Min weapon damage |
| 201 | Max weapon damage |

The packet is used during Loading and later Game updates, so protocol metadata is `phase: any`. The application still controls when it is valid to publish.

Canonical definition: `protocol/server/legacy-character-points.packet.yml`.

## CharacterUpdate — 0x13

Reference: `yuceloper/new-metin src/Executables/Game/Packets/CharacterUpdate.cs` and `PlayerEntity.SendCharacterUpdate()`.

| Field | Wire type | Bytes |
| --- | --- | ---: |
| vid | u32le | 4 |
| parts | u16le[4] | 8 |
| move_speed | u8 | 1 |
| attack_speed | u8 | 1 |
| state | u8 | 1 |
| affects | u32le[2] | 8 |
| guild_id | u32le | 4 |
| rank_points | i16le | 2 |
| pk_mode | u8 | 1 |
| mount_vnum | u32le | 4 |

Payload: **34 bytes**. Frame: **35 bytes**.

The inspected reference derives equipment parts from the equipment window and sends this packet again during Game equipment/state changes, so metadata is `phase: any`.

Canonical definition: `protocol/server/legacy-character-update.packet.yml`.

## Remake state ownership

The legacy `PlayerData` persisted a mixture of durable state and values that were also recalculated or reset while loading. The remake does not copy that ambiguity.

Durable PostgreSQL state currently includes:

- identity/account ownership
- name/class/level
- playtime
- experience
- gold
- base ST/HT/DX/IQ
- appearance fields
- position/map
- skill group
- available status points

Runtime/derived bootstrap context owns values such as:

- VID / runtime entity identity
- HP/SP and maxima
- required experience
- attack/defence calculations
- movement/attack speed
- equipment parts
- affects
- guild/rank/PK/mount runtime projection until their bounded modules own them

`LegacyCharacterBootstrapPublisher` starts from a 255-value runtime point projection and overlays authoritative durable point indices from the owned PostgreSQL snapshot. This avoids making the wire array the persistence model.

## Open compatibility work

Before declaring stock-client compatibility:

- verify opcodes/layouts against the exact target client build,
- capture real Select -> Loading bootstrap traffic,
- verify post-TokenLogin encryption boundary,
- verify the production sequence profile,
- verify VID lifecycle and allocation behavior,
- verify exact equipment part semantics,
- verify all point indices/values expected by the target client.
