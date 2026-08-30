using Metin2.Shared.Identity;

namespace Metin2.Modules.Characters.Application;

public interface IAccountEmpireRepository
{
    ValueTask<byte> GetEmpireAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);
}
