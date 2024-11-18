namespace Emulator.Components.Core.inputOutput;

public interface IPortOut
{
    void Write(object value);
    object Read();
}
