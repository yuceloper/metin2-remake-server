# ClientVS22 28249 compatibility profile

Status: **source-verified foundation; real-client capture verification remains pending**.

## Identified artifact

The supplied source archive identifies itself as ClientVS22 build `1.0.28249.1` in
`ClientVS22/source/UserInterface/Version.h`. It must not be labeled as the previously
researched 40250 client.

Evidence fingerprints:

- source archive SHA-256: `82466dbc9315878e32a5bfaab85aa07599e26f336c869f4bccc57c0f4fa84db4`
- `EterLib/NetStream.cpp` SHA-256: `e19462f99d1ed8ceda3c3a8cfdae75072d4f5d1c92747b225e8e1a31f1648dd4`
- `EterLib/cipher.cpp` SHA-256: `a0b860076a5a53a066e98a723e8fcf1f59a86b854f9420272baad216719a0e95`
- `EterBase/ServiceDefs.h` SHA-256: `86ef95a3acf5cf287f2949cca5f6f80f39432246407827bab4da5e2e70376f7a`

## Packet-security mode

`EterBase/ServiceDefs.h` enables `_IMPROVED_PACKET_ENCRYPTION_`.
The source follows the improved handshake already modeled by the server:

1. time synchronization handshake,
2. server cipher offer (`0xFB`),
3. client cipher reply,
4. server completion (`0xFA`),
5. cipher activation after completion,
6. continuous CTR transport.

The profile therefore selects `LegacyPacketEncryptionMode.ImprovedPacketEncryption`.

## Sequence table

The exact `s_bSequenceTable` initializer from `EterLib/NetStream.cpp` is embedded in
`ClientVs22_28249CompatibilityProfile`.

- length: `32768` bytes
- SHA-256: `3f6f31964896e712f1f54cb813c7b488b656f700f6ca948ece3313419f876279`
- first 32 bytes: `afca8acf48a754c7d7df012572f76f84bc3746e324daa1c8ee367c332f98765e`
- last 32 bytes: `732a6e66125864e725c02fd42de5f47d02c32cec0d913b8516f859de8e492481`

The profile validates both the embedded length and hash during initialization and creates
fresh `LegacySequenceProfile` instances so callers do not share mutable profile state.

## Remaining blockers

This profile closes the exact-build and exact-sequence-table unknowns. A production
compatibility claim still requires:

1. runtime selection and composition of this profile in the server host,
2. confirmation that the negotiated cipher selector is one of the managed provider's
   supported algorithms,
3. support or a deliberate negotiation strategy for MARS and SHACAL2, which exist in the
   client source but are currently rejected by the managed provider,
4. a real ClientVS22 28249 connection and packet capture covering cipher completion,
   phase transition, login and game traffic.
