﻿namespace PhoneBox.Generators
{
    [global::System.Flags]
    internal enum SignalRHubGenerationOutputs
    {
        None = 0,
        Interface = 1,
        Implementation = 2,
        Model = 4,
        All = Interface | Implementation | Model
    }
}