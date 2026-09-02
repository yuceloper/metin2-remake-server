using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Shared.Identity;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class ConfiguredLegacyCharacterSelectionWireContextProvider(
    IPAddress advertisedAddress,
    ushort advertisedPort) : ILegacyCharacterSelectionWireContextProvider
{
    private readonly int _addressWireValue = ToWireValue(advertisedAddress);

    public LegacyCharacterSelectionWireContext Get(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        byte empireSequence = session.CompatibilityProfile?.Sequence[0]
            ?? session.SequenceState?.Profile[0]
            ?? throw new InvalidOperationException("A sequence profile is required for character selection.");

        return new LegacyCharacterSelectionWireContext(
            _addressWireValue,
            advertisedPort,
            NextNonZeroUInt32(),
            NextNonZeroUInt32(),
            empireSequence);
    }

    private static int ToWireValue(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        byte[] bytes = address.MapToIPv4().GetAddressBytes();
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static uint NextNonZeroUInt32()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        uint value;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
        while (value == 0);

        return value;
    }
}

public sealed class DefaultLegacyCharacterBootstrapRuntimeContextProvider
    : ILegacyCharacterBootstrapRuntimeContextProvider
{
    private const int PointCount = 255;

    public LegacyCharacterBootstrapRuntimeContext Get(
        GameSession session,
        in CharacterBootstrapSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new LegacyCharacterBootstrapRuntimeContext(
            0,
            new uint[PointCount],
            new ushort[] { snapshot.BodyPart, 0, 0, snapshot.HairPart },
            100,
            100,
            0,
            new uint[2],
            new GuildId(0),
            0,
            0,
            0);
    }
}
