using System.Buffers.Binary;
using System.Text;

namespace Metin2.Protocol.IO;

public ref struct PacketWriter
{
    private Span<byte> _buffer;
    private int _offset;

    public PacketWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    public int Written => _offset;
    public int Remaining => _buffer.Length - _offset;

    public bool TryWriteByte(byte value)
    {
        if (Remaining < 1) return false;
        _buffer[_offset++] = value;
        return true;
    }

    public bool TryWriteSByte(sbyte value) => TryWriteByte(unchecked((byte)value));
    public bool TryWriteUInt16LittleEndian(ushort value) => TryWriteUInt16(value, true);
    public bool TryWriteUInt16BigEndian(ushort value) => TryWriteUInt16(value, false);
    public bool TryWriteInt16LittleEndian(short value) => TryWriteInt16(value, true);
    public bool TryWriteInt16BigEndian(short value) => TryWriteInt16(value, false);
    public bool TryWriteUInt32LittleEndian(uint value) => TryWriteUInt32(value, true);
    public bool TryWriteUInt32BigEndian(uint value) => TryWriteUInt32(value, false);
    public bool TryWriteInt32LittleEndian(int value) => TryWriteInt32(value, true);
    public bool TryWriteInt32BigEndian(int value) => TryWriteInt32(value, false);
    public bool TryWriteUInt64LittleEndian(ulong value) => TryWriteUInt64(value, true);
    public bool TryWriteUInt64BigEndian(ulong value) => TryWriteUInt64(value, false);
    public bool TryWriteInt64LittleEndian(long value) => TryWriteInt64(value, true);
    public bool TryWriteInt64BigEndian(long value) => TryWriteInt64(value, false);
    public bool TryWriteSingleLittleEndian(float value) => TryWriteInt32LittleEndian(BitConverter.SingleToInt32Bits(value));
    public bool TryWriteSingleBigEndian(float value) => TryWriteInt32BigEndian(BitConverter.SingleToInt32Bits(value));
    public bool TryWriteDoubleLittleEndian(double value) => TryWriteInt64LittleEndian(BitConverter.DoubleToInt64Bits(value));
    public bool TryWriteDoubleBigEndian(double value) => TryWriteInt64BigEndian(BitConverter.DoubleToInt64Bits(value));
    public bool TryWriteBool8(bool value) => TryWriteByte(value ? (byte)1 : (byte)0);

    public bool TryWriteBytes(ReadOnlySpan<byte> value)
    {
        if (Remaining < value.Length) return false;
        value.CopyTo(_buffer.Slice(_offset, value.Length));
        _offset += value.Length;
        return true;
    }

    public bool TryWriteFixedAsciiNullTerminated(string? value, int byteLength)
    {
        if (byteLength <= 0 || Remaining < byteLength) return false;

        value ??= string.Empty;
        int encodedLength = Encoding.ASCII.GetByteCount(value);
        if (encodedLength > byteLength - 1) return false;

        Span<byte> destination = _buffer.Slice(_offset, byteLength);
        destination.Clear();
        if (encodedLength > 0)
        {
            _ = Encoding.ASCII.GetBytes(value.AsSpan(), destination[..encodedLength]);
        }

        _offset += byteLength;
        return true;
    }

    private bool TryWriteUInt16(ushort value, bool littleEndian)
    {
        if (Remaining < sizeof(ushort)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(ushort));
        if (littleEndian) BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt16BigEndian(destination, value);
        _offset += sizeof(ushort);
        return true;
    }

    private bool TryWriteInt16(short value, bool littleEndian)
    {
        if (Remaining < sizeof(short)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(short));
        if (littleEndian) BinaryPrimitives.WriteInt16LittleEndian(destination, value);
        else BinaryPrimitives.WriteInt16BigEndian(destination, value);
        _offset += sizeof(short);
        return true;
    }

    private bool TryWriteUInt32(uint value, bool littleEndian)
    {
        if (Remaining < sizeof(uint)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(uint));
        if (littleEndian) BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        _offset += sizeof(uint);
        return true;
    }

    private bool TryWriteInt32(int value, bool littleEndian)
    {
        if (Remaining < sizeof(int)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(int));
        if (littleEndian) BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        else BinaryPrimitives.WriteInt32BigEndian(destination, value);
        _offset += sizeof(int);
        return true;
    }

    private bool TryWriteUInt64(ulong value, bool littleEndian)
    {
        if (Remaining < sizeof(ulong)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(ulong));
        if (littleEndian) BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
        else BinaryPrimitives.WriteUInt64BigEndian(destination, value);
        _offset += sizeof(ulong);
        return true;
    }

    private bool TryWriteInt64(long value, bool littleEndian)
    {
        if (Remaining < sizeof(long)) return false;
        Span<byte> destination = _buffer.Slice(_offset, sizeof(long));
        if (littleEndian) BinaryPrimitives.WriteInt64LittleEndian(destination, value);
        else BinaryPrimitives.WriteInt64BigEndian(destination, value);
        _offset += sizeof(long);
        return true;
    }
}
