namespace PhoneBox.Server
{
    internal sealed class WebHookOptions
    {
        public const string ConfigurationSectionName = "WebHook";

        public string? SharedSecret { get; set; }
    }
}