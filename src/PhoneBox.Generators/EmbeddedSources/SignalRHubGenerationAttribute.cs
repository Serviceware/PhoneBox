using System;

namespace PhoneBox.Generators
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class SignalRHubGenerationAttribute : Attribute
    {
        public SignalRHubGenerationOutputs OutputFilter { get; }

        public SignalRHubGenerationAttribute(SignalRHubGenerationOutputs outputFilter = SignalRHubGenerationOutputs.All)
        {
            OutputFilter = outputFilter;
        }
    }
}