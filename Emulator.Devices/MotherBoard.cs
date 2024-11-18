using Emulator.Components.Core;

namespace Emulator.Components;

public class MotherBoard : IMotherBoard
{
    private CPU _cpu;
    private Memory _memory;

    List<IPortOut> ports = [];

    public MotherBoard()
    {
        ports.Add(new Bus()); // addr
        ports.Add(new Bus()); // data
        ports.Add(new Bus()); // controll

        _cpu = new(this);
        _memory = new(this);
    }

    public void Start()
    {

        Schedue cpuSchedue = new();
        Schedue memSchedue = new();

        Thread cpuThead = new(() => { while (true) { cpuSchedue.Wait(); _cpu.TickThead(cpuSchedue); } });
        Thread memThead = new(() => { while (true) { memSchedue.Wait(); _memory.TickThead(memSchedue); } });

        cpuThead.Start();
        memThead.Start();

        int timer = 100;

        while (timer-- > 0)
        {
            cpuSchedue.Run();
            memSchedue.Run();
        }

    }

    public object Read(byte port) => ports[port].Read();
    public void Write(byte port, object value) => ports[port].Write(value);
}
