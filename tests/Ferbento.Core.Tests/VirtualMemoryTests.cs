using Ferbento.Core.Memory;

namespace Ferbento.Core.Tests;

public class VirtualMemoryTests
{
    [Fact]
    public void PeekPoke_RoundTrip()
    {
        var mem = new VirtualMemory();
        mem.Poke(0x100, 0xAB);
        Assert.Equal(0xAB, mem.Peek(0x100));
    }

    [Fact]
    public void Memcpy_CopiesBytes()
    {
        var mem = new VirtualMemory();
        mem.Poke(0x10, 1);
        mem.Poke(0x11, 2);
        mem.Memcpy(0x20, 0x10, 2);
        Assert.Equal(1, mem.Peek(0x20));
        Assert.Equal(2, mem.Peek(0x21));
    }
}
