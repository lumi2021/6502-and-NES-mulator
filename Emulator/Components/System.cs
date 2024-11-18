using Emulator.Components.Storage;

namespace Emulator.Components;

public class VirtualSystem
{
    private CPU _cpu;
    private PPU _ppu;
    private APU _apu;
    private RamMemory _ramMemory;
    private RomMemory _romMemory;

    public CPU Cpu => _cpu;
    public PPU Ppu => _ppu;
    public APU Apu => _apu;
    public RamMemory Ram => _ramMemory;
    public RomMemory Rom => _romMemory;

    public bool testMode = false;
    public byte[] debugMemory = new byte[256 * 256];

    public VirtualSystem()
    {
        _cpu = new(this);
        _ppu = new(this);
        _apu = new(this);
        _ramMemory = new(this);
        _romMemory = new(this);
    }


    public void Process()
    {
        for (var i = 0; i < 1516; i++)
            _cpu.Tick();
    }
    public void Draw()
    {
        _ppu.OnVblank = false;

        //for (var i = 0; i < 758; i++)
        //    _cpu.Tick();

        _ppu.OnVblank = true;
        _ppu.Draw();
    }


    public void InsertCartriadge(NESROM rom)
    {
        Console.WriteLine($"Mapper: {rom.Mapper}");
        Console.WriteLine($"PRG size: {rom.PRGDataSize16KB * 16} KiB");
        Console.WriteLine($"CHR size: {rom.CHRDataSize8KB * 8} KiB");
        Console.WriteLine($"Nametable arrangementg: {rom.NametableArrangement}");

        _romMemory.LoadRom(rom);

        _cpu.Reset();
        _ppu.ResetRomData();

        Console.WriteLine($"Entry: ${_cpu.ResetVector:X4}");
    }


    public byte Read(ushort address, ReadingAs from = 0)
    {
        if (testMode) return debugMemory[address];

        // CPU RAM / PPU pattern tables
        if (address < 0x2000)
            return from != ReadingAs.PPU ? _ramMemory.Read(address) : _romMemory.PPURead(address);

        // Picture PU registers
        else if (address >= 0x2000 && address < 0x4000)
            return _ppu.ReadRegister(address);

        // Audio PU registers
        else if (address == 0x4015)
            return _apu.ReadStatus();

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
            return _romMemory.CPURead(address);

        else // Open Bus
        {
            Console.WriteLine($"Reading in address {address:X} not implemented!");
            return 0;
        }
    }
    public void Write(ushort address, byte value, ReadingAs from = 0)
    {
        if (testMode)
        {
            debugMemory[address] = value;
            return;
        }

        // CPU RAM / PPU pattern tables
        if (address < 0x2000)
        {
            if (from != ReadingAs.PPU)
                _ramMemory.Store(address, value);
            else Console.WriteLine($"CHR ROM is not writealbe!");
        }

        // Picture PU registers
        else if ((address >= 0x2000 && address < 0x4000) || address == 0x4014)
            _ppu.WriteRegister(address, value);

        // Audio PU registers
        else if (address >= 0x4000 && address <= 0x4013) _apu.Write(address, value);
        else if (address == 0x4015) _apu.Write(address, value);

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

    public enum ReadingAs : byte
    {
        None = 0,
        CPU,
        PPU,
    }
}
