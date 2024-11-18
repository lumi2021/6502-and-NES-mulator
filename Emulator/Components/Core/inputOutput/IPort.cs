namespace Emulator.Components.Core.inputOutput;

public interface IPort
{
    void Write(byte port, object value);
    object Read(byte port);
}
