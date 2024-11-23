using Emulator.Components.Storage;
using Emulator.Mappers;

namespace Emulator.Components;

public class VirtualSystem
{
    private CPU _cpu;
    private PPU _ppu;
    private APU _apu;
    private RamMemory _ramMemory;
    private RomMemory _romMemory;

    private JoyController _joy1;
    //private JoyController _joy2;

    public CPU Cpu => _cpu;
    public PPU Ppu => _ppu;
    public APU Apu => _apu;
    public RamMemory Ram => _ramMemory;
    public RomMemory Rom => _romMemory;

    public JoyController Joy1 => _joy1;

    public Mapper RomMapper => _romMemory.RomData.mapper;

    public VirtualSystem()
    {
        _cpu = new(this);
        _ppu = new(this);
        _apu = new(this);
        _ramMemory = new(this);
        _romMemory = new(this);

        _joy1 = new(0, this, Program.input);
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
        Console.WriteLine($"Mapper: {rom.mapper.GetType().Name}");
        Console.WriteLine($"PRG size: {rom.PRGDataSize16KB * 16} KiB ({rom.PRGDataSize16KB})");
        Console.WriteLine($"CHR size: {rom.CHRDataSize8KB * 8} KiB ({rom.CHRDataSize8KB})");
        Console.WriteLine($"Nametable arrangementg: {rom.NametableArrangement}");

        _romMemory.LoadRom(rom);

        _cpu.Reset();
        _ppu.ResetRomData();

        Console.WriteLine($"Entry: ${_cpu.ResetVector:X4}");
    }


    public byte Read(ushort address, ReadingAs from = 0) => RomMapper.Read(this, address, from);
    public void Write(ushort address, byte value, ReadingAs from = 0) => RomMapper.Write(this, address, value, from);

}
public enum ReadingAs : byte
{
    CPU,
    PPU,
    None
}
