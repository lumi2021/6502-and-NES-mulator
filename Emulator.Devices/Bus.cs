using Emulator.Components.Core;
namespace Emulator.Components;

public class Bus() : Component(null!), IPortOut
{
    private object _data = 0;

    public object Read() => _data;
    public void Write(object value) => _data = value;
}
