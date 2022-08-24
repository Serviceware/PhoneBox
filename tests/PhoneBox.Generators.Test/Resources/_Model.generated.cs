namespace PhoneBox.Generators.Tests
{
    public sealed class CallNotificationEvent
    {
        public string DebugInfo { get; }
        public string CallerPhoneNumber { get; }
        public string CallStateKey { get; }
        public bool HasCallControl { get; }

        public CallNotificationEvent(string debugInfo, string callerPhoneNumber, string callStateKey, bool hasCallControl)
        {
            this.DebugInfo = debugInfo;
            this.CallerPhoneNumber = callerPhoneNumber;
            this.CallStateKey = callStateKey;
            this.HasCallControl = hasCallControl;
        }
    }

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