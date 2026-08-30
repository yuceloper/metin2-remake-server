using Metin2.Infrastructure.Networking.Sessions;

namespace Metin2.Infrastructure.Networking.Game;

public readonly record struct LegacyCharacterSelectionWireContext(
    int AddressWireValue,
    ushort Port,
    uint Handle,
    uint RandomKey,
    byte EmpireSequence);

public interface ILegacyCharacterSelectionWireContextProvider
{
    LegacyCharacterSelectionWireContext Get(GameSession session);
}
