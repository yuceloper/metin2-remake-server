# Legacy packet security research

Status: **reference-confirmed in inspected sources; target-client compatibility not yet verified**.

## Why packet security is a client profile

Metin2 sources do not expose one universal packet-security contract across all client builds.
The server therefore must not hard-code a single encryption mode or sequence table globally.

A compatibility profile groups:

- the exact sequence table/profile,
- packet encryption mode,
- mode-specific key material,
- eventually any build-specific protocol metadata proven necessary.

Supported profile modes are modeled as:

- `None` — useful for deterministic protocol probes/tests,
- `ClassicTea` — classic `_IMPROVED_PACKET_ENCRYPTION_` disabled path,
- `ImprovedPacketEncryption` — explicitly represented but not implemented yet.

## Classic TEA evidence

Classic server `DESC::Setup` initializes both encryption and decryption key material before the handshake. Inspected source shows two locale-dependent examples (`1234abcd5678efgh` for the Europe branch and `testtesttesttest` otherwise); these are evidence, **not a universal target key**.

Classic phase transition behavior is significant:

1. the GC Phase packet is queued while the previous encryption state is still in effect,
2. entering Login/Select/Loading/Game/Auth enables encrypted transport,
3. therefore the `FD 02` Login announcement itself crosses plaintext,
4. the client enables its initial/static transport key after entering Login,
5. the subsequent Login2/TokenLogin traffic belongs to the initial encrypted stage.

On Login2, classic server `SetSecurityKey(clientKey)` does:

- copy the 4 DWORD client key as the server **decryption key**,
- derive the server **encryption key** by TEA-encrypting those 16 client-key bytes with locale-specific 16-byte derivation material.

This creates two distinct classic-key stages:

```text
Plaintext handshake
  -> FD02 Login (plaintext transition packet)
  -> InitialKey encrypted transport
  -> Login2 / TokenLogin client key accepted
  -> RotatedClientKey encrypted transport
```

`LegacyTeaSecurityState` models these stages explicitly.

## TEA primitive

The classic code path uses TEA (`TEA_Encrypt` / `TEA_Decrypt`), not XTEA, with:

- 64-bit blocks,
- 128-bit keys,
- 32 rounds,
- 32-bit little-endian words,
- delta `0x9E3779B9`.

The networking source processes encrypted receive data only in complete 8-byte blocks and allows encrypted output to expand to the next block boundary.

`LegacyTeaCipher` contains the allocation-free block primitive and fixed buffer helpers. Before binding it into live socket pumps, the exact legacy buffer/padding semantics and client-side transform boundary must remain verified against the target build.

## 40250 caution

The inspected 40250 test-client `EterLib/NetStream.cpp` contains the old direct TEA code path as commented/obsolete code around `CNetworkStream::Send`. This is consistent with that build using the improved packet-encryption path rather than classic TEA.

Therefore:

- do not label ClassicTea as the 40250 production profile,
- do not enable ClassicTea by default,
- do not claim stock-client compatibility until the exact client executable/source/config is identified and captured.

## Remaining compatibility blockers

Before the first meaningful stock-client compatibility claim:

1. identify the exact target client build,
2. obtain/verify its 32768-byte sequence table,
3. determine classic vs improved packet encryption,
4. implement the selected encryption transport path,
5. verify the phase/key transition with a real packet capture.

The existing plaintext protocol probe remains intentionally separate from these client-specific compatibility profiles.
