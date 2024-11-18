﻿using Emulator.Components.Core.inputOutput;
using Emulator.Components.Storage;

namespace Emulator.Components.Core;

public interface IMotherBoard : IPort
{

    public CPU CpuRef { get; }
    public RamMemory MemRef {  get; }

}
