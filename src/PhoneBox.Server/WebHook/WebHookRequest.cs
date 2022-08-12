namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookRequest
    {
        public string FromPhoneNumber { get; }
        public string ToPhoneNumber { get; }

        public WebHookRequest(string fromPhoneNumber, string toPhoneNumber)
        {
            this.FromPhoneNumber = fromPhoneNumber;
            this.ToPhoneNumber = toPhoneNumber;
        }
    }
}