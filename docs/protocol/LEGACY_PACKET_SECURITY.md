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
- derive the server **encryption key** by running those 16 client-key bytes through the classic Metin2 `TEA_Encrypt` API with locale-specific 16-byte derivation material.

This creates two distinct classic-key stages:

```text
Plaintext handshake
  -> FD02 Login (plaintext transition packet)
  -> InitialKey encrypted transport
  -> Login2 / TokenLogin client key accepted
  -> RotatedClientKey encrypted transport
```

`LegacyTeaSecurityState` models these stages explicitly.

## The `TEA_Encrypt` naming trap

The original-compatible `libthecore/tea.cpp` exports functions named `TEA_Encrypt` and `TEA_Decrypt`, but the underlying 32-round block routine is **not the conventional TEA round function**.

The source uses XTEA-style key scheduling:

```text
y += (((z << 4) ^ (z >> 5)) + z) ^ (sum + key[sum & 3])
sum += 0x9E3779B9
z += (((y << 4) ^ (y >> 5)) + y) ^ (sum + key[(sum >> 11) & 3])
```

for 32 rounds. Decryption reverses those same operations.

The bulk wrapper invokes `tea_code(src[1], src[0], ...)`; the block function then initializes `y = sy` and `z = sz`, so the logical wire word order remains the two little-endian `uint32` words in source order.

`LegacyTeaCipher` deliberately retains the historical `Tea` name because it models the Metin2 API/transport contract, while its implementation follows the source's XTEA-style round semantics exactly.

## Bulk/padding semantics

The original bulk functions establish the following behavior:

- 64-bit (8-byte) blocks,
- 128-bit keys,
- 32 rounds,
- 32-bit little-endian words,
- delta `0x9E3779B9`,
- plaintext whose size is not divisible by 8 is zero-filled to the next 8-byte boundary before encryption,
- `TEA_Encrypt` returns that rounded encrypted byte count,
- `TEA_Decrypt` processes/returns the rounded block byte count,
- the cipher layer does **not** encode or recover the original unpadded plaintext length.

Therefore packet framing supplies the meaningful plaintext frame length; zero bytes at the end of a decrypted encrypted block are transport padding, not packet fields.

Encrypted receive processing in classic server source only works on complete 8-byte ciphertext blocks. Partial trailing ciphertext remains buffered until another socket read completes the block.

## Enqueue-time output semantics

Classic encryption is applied when individual packets are queued, not later when the socket happens to flush them. This matters across key transitions and for padding.

For example three plaintext frames of lengths `3`, `2`, and `329` are encrypted as three independent records:

```text
8 + 8 + 336 = 352 ciphertext bytes
```

Encrypting their concatenated 334-byte plaintext as one buffer would produce 336 bytes and is wire-incompatible.

`LegacyPacketOutput` therefore encrypts each complete wire frame independently using the key stage active at enqueue time.

## 40250 caution

The inspected 40250 test-client `EterLib/NetStream.cpp` contains the old direct classic-TEA path as commented/obsolete code around `CNetworkStream::Send`. This is consistent with that build using the improved packet-encryption path rather than classic TEA.

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
