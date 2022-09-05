namespace PhoneBox.Abstractions
{
    public sealed class CallStateEvent
    {
        public string PhoneNumber { get; }
        public string DebugInfo { get; }

        public CallStateEvent(string phoneNumber, string debugInfo)
        {
            this.PhoneNumber = phoneNumber;
            this.DebugInfo = debugInfo;
        }
    }
}