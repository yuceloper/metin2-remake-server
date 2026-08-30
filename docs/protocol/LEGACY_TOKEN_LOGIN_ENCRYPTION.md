# Legacy TokenLogin Encryption Boundary

Status: **reference-confirmed from classic public client/server source; stock-client compatibility capture pending**

This document records protocol evidence only. It does not adopt legacy architecture or crypto implementation.

## TokenLogin wire packet

Canonical remake definition:

```text
protocol/client/legacy-token-login.packet.yml
```

```text
Header:      0x6D
Direction:   client -> server
Phase:       Login
Sequence:    true
Username:    ASCII fixed[31]
Login key:   u32le
Client key:  u32le[4]
Payload:     51 bytes
Frame:       53 bytes
```

## Classic client boundary

Classic `PythonNetworkStreamPhaseLogin.cpp` `SendLoginPacketNew` behavior is ordered as follows:

1. Build the Login2/TokenLogin packet.
2. Copy the four DWORD client encryption values into the packet.
3. Send the packet.
4. Send the packet sequence byte.
5. Flush the internal send buffer.
6. Under classic `!_IMPROVED_PACKET_ENCRYPTION_`, enable security mode **after** that send completes.

Architecture consequence: TokenLogin itself is read using the normal legacy plaintext frame decoder. The remake must not attempt to decrypt the `0x6D` frame before dispatch.

## Classic server boundary

Classic `game/input_login.cpp` `CInputLogin::LoginByKey` behavior:

1. Parse the Login2 packet in Login phase.
2. Normalize the login name.
3. Store the login key.
4. Under classic `!_IMPROVED_PACKET_ENCRYPTION_`, call `SetSecurityKey` with the four DWORD values carried by the packet.
5. Forward login/key/client-key information to the DB/auth side for validation.

Architecture consequence: successful TokenLogin processing must preserve the four client key DWORDs as connection/session state before later encrypted traffic is enabled.

## Remake boundary

Phase 9B therefore does exactly this:

```text
Handshake
  -> FD 02 (Login)
  -> plaintext TokenLogin + validated trailing sequence
  -> consume one-time Auth token
  -> authenticate GameSession
  -> copy and retain client u32[4] security key
```

Phase 9B deliberately does **not**:

- decrypt TokenLogin,
- enable XTEA/security mode yet,
- transition to Select,
- send Empire/CharacterList packets.

Those steps require the post-TokenLogin transport and character-selection protocol to be implemented together so the connection cannot enter a half-valid protocol state.

## Remaining verification

Before calling stock-client encryption compatibility complete:

- verify exact target client build and whether it uses classic or improved packet encryption,
- verify DWORD/key byte order against that build,
- capture the first post-TokenLogin client/server frames where practical,
- implement encrypted transport with golden vectors before enabling it in production composition.

## Evidence

Classic public server source:

```text
game/input_login.cpp
CInputLogin::LoginByKey
```

Classic public client source:

```text
UserInterface/PythonNetworkStreamPhaseLogin.cpp
CPythonNetworkStream::SendLoginPacketNew
```

The earlier `yuceloper/new-metin` reference also confirms the `0x6D` field layout and token/username validation behavior, but its XTEA field is not sufficient by itself to establish the real activation boundary.
