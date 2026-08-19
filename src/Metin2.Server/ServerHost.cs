namespace Metin2.Server;

public static class ServerHost
{
    public static Task RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Console.WriteLine("Metin2 Remake Server bootstrap ready.");
        return Task.CompletedTask;
    }
}
