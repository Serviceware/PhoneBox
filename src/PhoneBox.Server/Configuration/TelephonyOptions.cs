namespace PhoneBox.Server
{
    internal sealed class TelephonyOptions
    {
        public const string ConfigurationSectionName = "Telephony";

        public TelephonyProvider Provider { get; set; }
    }
}