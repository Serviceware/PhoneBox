using System;

namespace PhoneBox.Generators
{
    [Flags]
    public enum SignalRHubGenerationOutputs
    {
        None = 0,
        Interface = 1,
        Implementation = 2,
        Model = 4,
        All = Interface | Implementation | Model
    }
}