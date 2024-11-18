namespace Emulator.Components.Core;

public interface IPort
{
    void Write(byte port, object value);
    object Read(byte port);
}
