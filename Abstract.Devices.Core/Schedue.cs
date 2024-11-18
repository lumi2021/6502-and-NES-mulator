namespace Emulator.Components.Core;

public sealed class Schedue {
    private bool _onHold = true;

    public void Run()
    {
        _onHold = false;
        while (!_onHold);
    }
    public void Wait() {
        _onHold = true;
        while (_onHold);
    }

    public void TryWait()
    {
        while(_onHold);
    }
}
