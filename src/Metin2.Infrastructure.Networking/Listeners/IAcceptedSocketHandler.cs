using System.Net.Sockets;

namespace Metin2.Infrastructure.Networking.Listeners;

public interface IAcceptedSocketHandler
{
    ValueTask HandleAsync(Socket socket, CancellationToken cancellationToken);
}
