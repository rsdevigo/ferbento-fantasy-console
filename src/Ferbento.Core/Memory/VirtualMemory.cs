using Ferbento.Core.Exceptions;

namespace Ferbento.Core.Memory;

public sealed class VirtualMemory
{
    private readonly byte[] _data;
    private readonly int _size;

    public VirtualMemory(int size = MemoryMap.TotalSize)
    {
        if (size > MemoryMap.TotalSize)
            throw new HardwareLimitException($"RAM size {size} exceeds maximum {MemoryMap.TotalSize} bytes.");
        _size = size;
        _data = new byte[MemoryMap.TotalSize];
    }

    public int Size => _size;
    public byte[] Buffer => _data;

    public byte Peek(int address)
    {
        ValidateAddress(address);
        return _data[address];
    }

    public void Poke(int address, byte value)
    {
        ValidateAddress(address);
        _data[address] = value;
    }

    public ushort Peek2(int address)
    {
        ValidateAddress(address + 1);
        return (ushort)(_data[address] | (_data[address + 1] << 8));
    }

    public void Poke2(int address, ushort value)
    {
        ValidateAddress(address + 1);
        _data[address] = (byte)(value & 0xFF);
        _data[address + 1] = (byte)((value >> 8) & 0xFF);
    }

    public uint Peek4(int address)
    {
        ValidateAddress(address + 3);
        return (uint)(_data[address]
            | (_data[address + 1] << 8)
            | (_data[address + 2] << 16)
            | (_data[address + 3] << 24));
    }

    public void Poke4(int address, uint value)
    {
        ValidateAddress(address + 3);
        _data[address] = (byte)(value & 0xFF);
        _data[address + 1] = (byte)((value >> 8) & 0xFF);
        _data[address + 2] = (byte)((value >> 16) & 0xFF);
        _data[address + 3] = (byte)((value >> 24) & 0xFF);
    }

    public void Memcpy(int dst, int src, int len)
    {
        if (len <= 0) return;
        ValidateRange(dst, len);
        ValidateRange(src, len);
        if (dst == src) return;

        if (dst < src)
        {
            for (var i = 0; i < len; i++)
                _data[dst + i] = _data[src + i];
        }
        else
        {
            for (var i = len - 1; i >= 0; i--)
                _data[dst + i] = _data[src + i];
        }
    }

    public void Memset(int dst, byte value, int len)
    {
        if (len <= 0) return;
        ValidateRange(dst, len);
        Array.Fill(_data, value, dst, len);
    }

    public void Clear() => Array.Clear(_data);

    private void ValidateAddress(int address)
    {
        if (address < 0 || address >= _size)
            throw new HardwareLimitException($"Address 0x{address:X4} out of range (0..0x{_size - 1:X4}).");
    }

    private void ValidateRange(int address, int len)
    {
        if (address < 0 || address + len > _size)
            throw new HardwareLimitException($"Memory range 0x{address:X4}+0x{len:X4} out of bounds.");
    }
}
