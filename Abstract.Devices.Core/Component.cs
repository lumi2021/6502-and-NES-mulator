namespace Emulator.Components.Core;

public abstract class Component(IMotherBoard motherBoard)
{
    protected IMotherBoard motherBoard = motherBoard;
}
