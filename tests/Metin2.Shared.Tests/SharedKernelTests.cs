using Metin2.Shared.Identity;
using Metin2.Shared.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Shared.Tests;

[TestClass]
public sealed class SharedKernelTests
{
    [TestMethod]
    public void StrongIds_UseValueSemanticsWithoutCrossTypeConversion()
    {
        var first = new CharacterId(42);
        var second = new CharacterId(42);
        var other = new CharacterId(43);

        Assert.AreEqual(first, second);
        Assert.AreNotEqual(first, other);
        Assert.AreEqual("42", first.ToString());
    }

    [TestMethod]
    public void Result_Success_HasNoError()
    {
        var result = Result.Success();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.AreEqual(Error.None, result.Error);
    }

    [TestMethod]
    public void ResultOfT_Failure_ThrowsWhenValueIsAccessed()
    {
        var error = new Error("character.not_found", "Character was not found.");
        var result = Result<CharacterId>.Failure(error);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(error, result.Error);
        Assert.ThrowsException<InvalidOperationException>(() => _ = result.Value);
    }
}
