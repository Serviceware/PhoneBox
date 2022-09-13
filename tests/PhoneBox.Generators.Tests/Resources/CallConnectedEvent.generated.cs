namespace PhoneBox.Abstractions
{
    public sealed class CallConnectedEvent
    {
        public string PhoneNumber { get; }

        public CallConnectedEvent(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}