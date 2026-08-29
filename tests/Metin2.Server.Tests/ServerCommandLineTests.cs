using System.Net;
using Metin2.Server;

namespace Metin2.Server.Tests;

[TestClass]
public sealed class ServerCommandLineTests
{
    [TestMethod]
    public void Auth_mode_uses_loopback_by_default()
    {
        ServerCommandLineResult result = ServerCommandLine.Parse(
            ["serve", "--mode", "auth", "--port", "15000"]);

        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(ServerRunMode.Auth, result.Options.Value.Mode);
        Assert.AreEqual(IPAddress.Loopback, result.Options.Value.BindAddress);
        Assert.AreEqual(15000, result.Options.Value.Port);
    }

    [TestMethod]
    public void Game_mode_accepts_explicit_bind_address()
    {
        ServerCommandLineResult result = ServerCommandLine.Parse(
            ["serve", "--mode", "game", "--bind", "0.0.0.0", "--port", "16000"]);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(ServerRunMode.Game, result.Options!.Value.Mode);
        Assert.AreEqual(IPAddress.Any, result.Options.Value.BindAddress);
        Assert.AreEqual(16000, result.Options.Value.Port);
    }

    [TestMethod]
    public void Missing_port_is_rejected()
    {
        ServerCommandLineResult result = ServerCommandLine.Parse(["serve", "--mode", "auth"]);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Error!, "--port");
    }

    [TestMethod]
    public void Invalid_mode_is_rejected()
    {
        ServerCommandLineResult result = ServerCommandLine.Parse(
            ["serve", "--mode", "world", "--port", "15000"]);

        Assert.IsFalse(result.IsValid);
        StringAssert.Contains(result.Error!, "--mode");
    }

    [TestMethod]
    public void Help_does_not_start_a_server()
    {
        ServerCommandLineResult result = ServerCommandLine.Parse(["--help"]);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.ShowHelp);
        Assert.IsNull(result.Options);
    }
}
