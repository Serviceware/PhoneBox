namespace PhoneBox.Abstractions
{
    public sealed class CallDisconnectedEvent
    {
        public string PhoneNumber { get; }

        public CallDisconnectedEvent(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}