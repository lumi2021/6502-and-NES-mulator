using Emulator.Components.Core;

namespace Emulator.Components;

public class CPU(MotherBoard mb) : Component(mb)
{
    private ushort progCounter = 0;

    private byte stackPointer = 0;

    private byte Accumulator = 0;
    private byte IndexX = 0;
    private byte IndexY = 0;

    private byte Flags = 0;

    public void TickThead(Schedue schedue)
    {
        // set 0x1 as 1
        motherBoard.Write(0, 1); // addr 0x1
        motherBoard.Write(1, 1); // val  0x1
        motherBoard.Write(2, 1); // op   store

        schedue.Wait();

        while (true)
        {
            // ask for read IndexX and increment IndexX
            motherBoard.Write(0, IndexX++); // addr IndexX
            motherBoard.Write(2, 0);      // op   read

            schedue.Wait();

            // set IndexY
            IndexY = Convert.ToByte(motherBoard.Read(1));

            // ask for read IndexX and increment IndexX
            motherBoard.Write(0, IndexX);
            motherBoard.Write(2, 0);

            schedue.Wait();

            // add IndexY
            IndexY += Convert.ToByte(motherBoard.Read(1));

            schedue.Wait();

            // save IndexY in [IndexX + 1]
            motherBoard.Write(0, IndexX + 1);
            motherBoard.Write(1, IndexY);
            motherBoard.Write(2, 1);

            schedue.Wait();

            Console.WriteLine($"{IndexY}");
        }
    }

    //public void Process

}
