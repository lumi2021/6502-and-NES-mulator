using Emulator.Components;

namespace Emulator.Mappers;

internal class NROM(NESROM r) : Mapper(r)
{
    protected override ushort ProcessAddress(ushort address, ReadingAs device)
    {
        if (device == ReadingAs.CPU)
        {

            if (address >= 0x8000 && address <= 0xBFFF) return address;
            else if (address >= 0xC000 && address <= 0xFFFF)
            {
                if (romReference.PRGDataSize16KB == 1) return (ushort)(address - 0x4000);
                else return address;
            }

            return address;
        }
        else return address;
    }
}
