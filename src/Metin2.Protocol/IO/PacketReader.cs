using System.Buffers.Binary;

namespace Metin2.Protocol.IO;

public ref struct PacketReader
{
    private ReadOnlySpan<byte> _buffer;
    private int _offset;

    public PacketReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    public int Consumed => _offset;
    public int Remaining => _buffer.Length - _offset;

    public bool TryReadByte(out byte value)
    {
        if (Remaining < 1)
        {
            value = default;
            return false;
        }

        value = _buffer[_offset++];
        return true;
    }

    public bool TryReadSByte(out sbyte value)
    {
        if (!TryReadByte(out byte raw))
        {
            value = default;
            return false;
        }

        value = unchecked((sbyte)raw);
        return true;
    }

    public bool TryReadUInt16LittleEndian(out ushort value) =>
        TryReadUInt16(out value, littleEndian: true);

    public bool TryReadUInt16BigEndian(out ushort value) =>
        TryReadUInt16(out value, littleEndian: false);

    public bool TryReadInt16LittleEndian(out short value) =>
        TryReadInt16(out value, littleEndian: true);

    public bool TryReadInt16BigEndian(out short value) =>
        TryReadInt16(out value, littleEndian: false);

    public bool TryReadUInt32LittleEndian(out uint value) =>
        TryReadUInt32(out value, littleEndian: true);

    public bool TryReadUInt32BigEndian(out uint value) =>
        TryReadUInt32(out value, littleEndian: false);

    public bool TryReadInt32LittleEndian(out int value) =>
        TryReadInt32(out value, littleEndian: true);

    public bool TryReadInt32BigEndian(out int value) =>
        TryReadInt32(out value, littleEndian: false);

    public bool TryReadUInt64LittleEndian(out ulong value) =>
        TryReadUInt64(out value, littleEndian: true);

    public bool TryReadUInt64BigEndian(out ulong value) =>
        TryReadUInt64(out value, littleEndian: false);

    public bool TryReadInt64LittleEndian(out long value) =>
        TryReadInt64(out value, littleEndian: true);

    public bool TryReadInt64BigEndian(out long value) =>
        TryReadInt64(out value, littleEndian: false);

    public bool TryReadSingleLittleEndian(out float value)
    {
        if (!TryReadInt32LittleEndian(out int bits))
        {
            value = default;
            return false;
        }

        value = BitConverter.Int32BitsToSingle(bits);
        return true;
    }

    public bool TryReadSingleBigEndian(out float value)
    {
        if (!TryReadInt32BigEndian(out int bits))
        {
            value = default;
            return false;
        }

        value = BitConverter.Int32BitsToSingle(bits);
        return true;
    }

    public bool TryReadDoubleLittleEndian(out double value)
    {
        if (!TryReadInt64LittleEndian(out long bits))
        {
            value = default;
            return false;
        }

        value = BitConverter.Int64BitsToDouble(bits);
        return true;
    }

    public bool TryReadDoubleBigEndian(out double value)
    {
        if (!TryReadInt64BigEndian(out long bits))
        {
            value = default;
            return false;
        }

        value = BitConverter.Int64BitsToDouble(bits);
        return true;
    }

    public bool TryReadBool8(out bool value)
    {
        if (!TryReadByte(out byte raw))
        {
            value = default;
            return false;
        }

        value = raw != 0;
        return true;
    }

    public bool TryReadBytes(int length, out ReadOnlySpan<byte> value)
    {
        if (length < 0 || Remaining < length)
        {
            value = default;
            return false;
        }

        value = _buffer.Slice(_offset, length);
        _offset += length;
        return true;
    }

    private bool TryReadUInt16(out ushort value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(ushort), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
        return true;
    }

    private bool TryReadInt16(out short value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(short), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadInt16BigEndian(bytes);
        return true;
    }

    private bool TryReadUInt32(out uint value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(uint), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return true;
    }

    private bool TryReadInt32(out int value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(int), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadInt32BigEndian(bytes);
        return true;
    }

    private bool TryReadUInt64(out ulong value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(ulong), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return true;
    }

    private bool TryReadInt64(out long value, bool littleEndian)
    {
        if (!TryReadBytes(sizeof(long), out ReadOnlySpan<byte> bytes))
        {
            value = default;
            return false;
        }

        value = littleEndian
            ? BinaryPrimitives.ReadInt64LittleEndian(bytes)
            : BinaryPrimitives.ReadInt64BigEndian(bytes);
        return true;
    }
}
