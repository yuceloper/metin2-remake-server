using Metin2.Infrastructure.Networking.Sessions;
using Metin2.Modules.Characters.Application;
using Metin2.Modules.World;

namespace Metin2.Infrastructure.Networking.Game;

public sealed class WorldCharacterBootstrapRuntimeContextProvider(
    PlayerRuntimeRegistry registry,
    ILegacyCharacterBootstrapRuntimeContextProvider inner)
    : ILegacyCharacterBootstrapRuntimeContextProvider
{
    public LegacyCharacterBootstrapRuntimeContext Get(
        GameSession session,
        in CharacterBootstrapSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(session);

        LegacyCharacterBootstrapRuntimeContext context = inner.Get(session, in snapshot);
        var position = new Position(snapshot.MapId, snapshot.PositionX, snapshot.PositionY);

        if (!registry.TryReserve(snapshot.CharacterId, position, out PlayerRuntimeReservation reservation))
        {
            throw new InvalidOperationException(
                $"Character {snapshot.CharacterId} already has an active runtime reservation.");
        }

        try
        {
            session.AttachRuntimeEntity(reservation.EntityId);
            return context with { Vid = reservation.EntityId.Value };
        }
        catch
        {
            registry.Release(reservation.EntityId);
            session.DetachRuntimeEntity();
            throw;
        }
    }
}
