namespace PhoneBox.Generators
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
    internal sealed class SignalRHubGenerationAttribute : global::System.Attribute
    {
        public global::PhoneBox.Generators.SignalRHubGenerationOutputs OutputFilter { get; }

        public SignalRHubGenerationAttribute(global::PhoneBox.Generators.SignalRHubGenerationOutputs outputFilter = global::PhoneBox.Generators.SignalRHubGenerationOutputs.All)
        {
            OutputFilter = outputFilter;
        }
    }
}