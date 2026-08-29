using System.Diagnostics;

namespace Metin2.Infrastructure.Networking.Handshake;

public interface IServerTimeProvider
{
    long GetMilliseconds();
}

public sealed class StopwatchServerTimeProvider : IServerTimeProvider
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public long GetMilliseconds() => _stopwatch.ElapsedMilliseconds;
}
