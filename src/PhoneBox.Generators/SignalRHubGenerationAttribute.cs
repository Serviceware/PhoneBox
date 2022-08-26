using System;

namespace PhoneBox.Generators
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class SignalRHubGenerationAttribute : Attribute
    {
        public SignalRHubGenerationOutputs OutputFilter { get; }

        public SignalRHubGenerationAttribute(SignalRHubGenerationOutputs outputFilter = SignalRHubGenerationOutputs.All)
        {
            this.OutputFilter = outputFilter;
        }
    }
}