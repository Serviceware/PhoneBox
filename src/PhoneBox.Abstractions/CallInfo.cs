namespace PhoneBox.Abstractions
{
    public readonly struct CallInfo
    {
        public string PhoneNumber { get; }

        public CallInfo(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}