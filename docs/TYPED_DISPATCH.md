# Generated Typed Packet Dispatch

The protocol source generator emits a compile-time dispatch contract for every canonical packet that already has a generated fixed-layout codec.

## Boundary

The generated dispatcher owns only payload decoding and typed method selection:

```text
PacketRegistration + ReadOnlySequence<byte>
                ↓
       generated PacketDispatcher
                ↓
       generated packet struct
                ↓
        IPacketDispatchTarget
```

It does not own sockets, session state, dependency injection, encryption, sequence progression, logging, or application/domain behavior.

## Allocation behavior

Single-segment payloads are decoded directly from the existing memory. Small segmented fixed payloads are copied into bounded stack storage before decoding. Payloads larger than the generator's stack threshold are not coalesced onto the heap by this layer.

## Async handlers

`PacketDispatcher.Dispatch` performs decode synchronously and returns a `PacketDispatchAttempt`. A successful attempt carries the handler's `ValueTask`, which the caller may await after the synchronous decode has completed. This keeps stack-backed segmented decode storage from crossing an `await` boundary.
