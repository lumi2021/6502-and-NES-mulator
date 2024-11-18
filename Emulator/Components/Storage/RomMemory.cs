
using Emulator.Components.Core;

namespace Emulator.Components.Storage;

public class RomMemory : Component
{

    private NESROM? _rom;
    public NESROM RomData => _rom!;

    public RomMemory(VirtualSystem sys) : base(sys)
    {

    }

    public void LoadRom(NESROM rom)
    {
        _rom = rom;
    }


    public byte PPURead(ushort addr)
    {
        if (addr < 0x2000) return _rom?.ChrData[addr] ?? 0;
        else
        {
            Console.WriteLine($"PPU can't read ROM address ${addr:X4}!");
            return 0;
        }
    }
    public byte CPURead(ushort addr)
    {
        if (addr >= 0x4020)
        {
            if (addr >= 0x8000)
            {
                return _rom?.PrgData[addr - 0x8000] ?? 0;
            }
            else
            {
                Console.WriteLine($"CPU ROM address ${addr:X4} not implemented!");
                return 0;
            }

        }
        else
        {
            Console.WriteLine($"CPU can't read ROM address ${addr:X4}!");
            return 0;
        }
    }

    public void Write(ushort addr, byte value)
    {
        Console.WriteLine("ROM Memory is not writeable!");
    }

}
