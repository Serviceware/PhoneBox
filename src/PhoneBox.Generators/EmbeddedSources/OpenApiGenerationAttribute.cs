namespace PhoneBox.Generators
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly)]
    internal sealed class OpenApiGenerationAttribute : global::System.Attribute
    {
        public global::PhoneBox.Generators.SignalRHubGenerationOutputs OutputFilter { get; }

        public OpenApiGenerationAttribute(global::PhoneBox.Generators.SignalRHubGenerationOutputs outputFilter = global::PhoneBox.Generators.SignalRHubGenerationOutputs.All)
        {
            OutputFilter = outputFilter;
        }
    }
}