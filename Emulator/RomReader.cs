using Emulator.Mappers;

namespace Emulator;

internal static class RomReader
{

    public static NESROM LoadFromPath(string path)
    {
        byte[] data = File.ReadAllBytes(path);

        if (data[0] == 0x4E && data[1] == 0x45 && data[2] == 0x53 && data[3] == 0x1A)
            return new NESROM(data);
        else throw new Exception("Invalid NES room!");
    }

}

public class NESROM
{

    private byte[] header = [];
    private byte[] trainer = [];
    private byte[] prgData = [];
    private byte[] chrData = [];

    public Mapper mapper;

    // Header data
    public byte PRGDataSize16KB => header[4];
    public byte CHRDataSize8KB => header[5];

    public bool Trainer => ((header[6] >> 6) & 1) == 1;
    public NametableMirroring NametableArrangement => (((header[6] >> 8) & 1) == 0) ? NametableMirroring.Horizontal : NametableMirroring.Vertical;

    public byte[] PrgData => [.. prgData];
    public byte[] ChrData => [.. chrData];

    public NESROM(byte[] data)
    {
        header = data[0..16];

        int b = 16;
        if (Trainer)
        {
            trainer = data[b..(b+512)];
            b = 528;
        }

        int dl = PRGDataSize16KB * 16 * 1024;
        prgData = data[b .. (b + dl)];
        b += dl;

        dl = CHRDataSize8KB * 8 * 1024;
        chrData = data[b..(b + dl)];
        b += dl;

        mapper = GetMapper((byte)((header[6] >> 4) | (header[7] & 0xF0)), this);
    }

    private static Mapper GetMapper(byte mapper, NESROM parent)
    {
        return mapper switch
        {
            0x00 => new NROM(parent),

            _ => throw new NotImplementedException($"mapper {mapper}")
        };
    }
}

public enum NametableMirroring : byte
{
    Horizontal,
    Vertical,
}
