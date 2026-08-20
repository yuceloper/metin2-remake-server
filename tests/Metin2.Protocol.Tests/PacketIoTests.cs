using Metin2.Protocol.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Metin2.Protocol.Tests;

[TestClass]
public sealed class PacketIoTests
{
    [TestMethod]
    public void FixedAsciiNullTerminated_RoundTripsAndConsumesExactWidth()
    {
        Span<byte> buffer = stackalloc byte[31];
        var writer = new PacketWriter(buffer);

        Assert.IsTrue(writer.TryWriteFixedAsciiNullTerminated("metin2", 31));
        Assert.AreEqual(31, writer.Written);
        Assert.AreEqual((byte)0, buffer[30]);

        var reader = new PacketReader(buffer);
        Assert.IsTrue(reader.TryReadFixedAsciiNullTerminated(31, out string value));
        Assert.AreEqual("metin2", value);
        Assert.AreEqual(31, reader.Consumed);
    }

    [TestMethod]
    public void FixedAsciiNullTerminated_RejectsValueWithoutTerminatorCapacityBeforeWriting()
    {
        Span<byte> buffer = stackalloc byte[31];
        buffer.Fill(0xCC);
        var writer = new PacketWriter(buffer);
        string value = new('A', 31);

        Assert.IsFalse(writer.TryWriteFixedAsciiNullTerminated(value, 31));
        Assert.AreEqual(0, writer.Written);
        Assert.IsTrue(buffer.ToArray().All(static value => value == 0xCC));
    }

    [TestMethod]
    public void FixedAsciiNullTerminated_TruncatedInputDoesNotAdvanceReader()
    {
        ReadOnlySpan<byte> buffer = new byte[30];
        var reader = new PacketReader(buffer);

        Assert.IsFalse(reader.TryReadFixedAsciiNullTerminated(31, out _));
        Assert.AreEqual(0, reader.Consumed);
    }
}
