using Emulator.Components;

namespace Emulator.Mappers;

public abstract class Mapper(NESROM rom)
{
    public readonly NESROM romReference = rom;

    public byte Read(VirtualSystem sys, ushort address, ReadingAs device) => ProcessRead(sys, ProcessAddress(address, device), device);
    public void Write(VirtualSystem sys, ushort address, byte value, ReadingAs device) => ProcessWrite(sys, ProcessAddress(address, device), value, device);

    protected abstract ushort ProcessAddress(ushort address, ReadingAs device);

    protected virtual byte ProcessRead(VirtualSystem sys, ushort address, ReadingAs device)
    {
        // CPU RAM / PPU pattern tables
        if (address < 0x2000)
            return device != ReadingAs.PPU ? sys.Ram.Read(address) : sys.Rom.PPURead(address);

        // Picture PU registers
        else if (address >= 0x2000 && address < 0x4000)
            return sys.Ppu.ReadRegister(address);

        // Audio PU registers
        else if (address == 0x4015)
            return sys.Apu.ReadStatus();

        // Input shit
        else if (address == 0x4016)
        {
            //Console.WriteLine("Reading joy 1");
            return 0;
        }
        else if (address == 0x4017)
        {
            //Console.WriteLine("Reading joy 2");
            return 0;
        }

        // CHR PRG RAM and ROM
        else if (address >= 0x4020)
            return sys.Rom.CPURead(address);

        else // Open Bus
        {
            Console.WriteLine($"Reading in address {address:X} not implemented!");
            return 0;
        }

    }
    protected virtual void ProcessWrite(VirtualSystem sys, ushort address, byte value, ReadingAs device)
    {
        // CPU RAM / PPU pattern tables
        if (address < 0x2000)
        {
            if (device != ReadingAs.PPU)
                sys.Ram.Store(address, value);
            else Console.WriteLine($"CHR ROM is not writealbe!");
        }

        // Picture PU registers
        else if ((address >= 0x2000 && address < 0x4000) || address == 0x4014)
            sys.Ppu.WriteRegister(address, value);

        // Audio PU registers
        else if (address >= 0x4000 && address <= 0x4013) sys.Apu.Write(address, value);
        else if (address == 0x4015) sys.Apu.Write(address, value);

        // Input shit
        else if (address == 0x4016)
        {
            //if (value == 1) Console.WriteLine("Start pooling input");
            //else if (value == 0) Console.WriteLine("Stop pooling input");
        }

        // CHR PRG RAM and ROM
        else if (address >= 0x4020)
        {
            Console.WriteLine($"PRG ROM addr ${address:X4} is not writeable!");
        }

        else
        {
            //Console.WriteLine($"Writing in address ${address:X} not implemented!");
        }

    }
}
