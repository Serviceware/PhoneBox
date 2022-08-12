namespace PhoneBox.Abstractions
{
    public readonly struct CallNotificationEvent
    {
        public string PhoneNumber { get; }
        public string DebugInfo { get; }

        public CallNotificationEvent(string phoneNumber, string debugInfo)
        {
            this.PhoneNumber = phoneNumber;
            this.DebugInfo = debugInfo;
        }
    }
    public readonly struct CallStateEvent
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