namespace PhoneBox.Abstractions
{
    public readonly struct CallInfo
    {
        public string PhoneNumber { get; }
        public string DebugInfo { get; }

        public CallInfo(string phoneNumber, string debugInfo)
        {
            this.PhoneNumber = phoneNumber;
            this.DebugInfo = debugInfo;
        }
    }
}