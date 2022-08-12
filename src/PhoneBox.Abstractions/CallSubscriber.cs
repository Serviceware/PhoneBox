namespace PhoneBox.Abstractions
{
    public readonly struct CallSubscriber
    {
        public string PhoneNumber { get; }

        public CallSubscriber(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}