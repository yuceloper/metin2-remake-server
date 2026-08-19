using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Architecture.Tests;

[TestClass]
public sealed class RepositoryFoundationTests
{
    [TestMethod]
    public void ServerHost_IsPublicArchitectureEntryPoint()
    {
        Assert.IsTrue(typeof(Metin2.Server.ServerHost).IsAbstract && typeof(Metin2.Server.ServerHost).IsSealed);
    }
}
