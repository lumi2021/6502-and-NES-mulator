namespace Emulator.Components.Core;

public interface IPortOut
{
    void Write(object value);
    object Read();
}
