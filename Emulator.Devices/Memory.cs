using Emulator.Components.Core;

namespace Emulator.Components;

public class Memory(IMotherBoard mb) : Component(mb)
{

    byte[] _storage = new byte[0xFF * 0xFF];

    public void TickThead(Schedue schedue)
    {
        if ((int)motherBoard.Read(2) == 0) ReadData();  // Read Memory
        if ((int)motherBoard.Read(2) == 1) StoreData(); // Store Memory
    }

    void ReadData() => motherBoard.Write(1, (int)_storage[Convert.ToInt32(motherBoard.Read(0))]);
    void StoreData() => _storage[Convert.ToInt32(motherBoard.Read(0))] = Convert.ToByte(motherBoard.Read(1));
}
