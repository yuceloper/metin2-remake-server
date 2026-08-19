namespace Metin2.Architecture.Tests;

[TestClass]
public sealed class RepositoryFoundationTests
{
    [TestMethod]
    public void ServerHost_IsPublicArchitectureEntryPoint()
    {
        Assert.IsTrue(typeof(Server.ServerHost).IsAbstract && typeof(Server.ServerHost).IsSealed);
    }
}
