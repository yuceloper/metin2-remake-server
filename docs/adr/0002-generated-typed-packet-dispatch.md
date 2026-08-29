# ADR 0002: Generated typed packet dispatch

## Status

Accepted

## Context

The server must decode legacy Metin2 payloads without runtime reflection, object envelopes, or handwritten opcode/type switch tables that duplicate the protocol schema.

## Decision

Canonical packet definitions remain the single source of truth. The Roslyn generator emits:

- a typed `IPacketDispatchTarget` overload for each packet with a supported generated codec,
- `PacketDispatcher` routing by generated `PacketId`,
- payload-size validation before handler invocation,
- direct decode for contiguous payloads,
- bounded stack coalescing for small segmented fixed payloads.

The dispatcher returns a synchronous dispatch attempt containing the handler `ValueTask`; it does not await internally.

## Consequences

- Adding/removing a canonical dispatchable packet changes the handler contract at compile time.
- No runtime reflection or boxed packet envelope is needed.
- Protocol code remains independent of networking/session/application layers.
- Segmented payloads above the bounded stack threshold require a future codec strategy instead of silent heap allocation.
