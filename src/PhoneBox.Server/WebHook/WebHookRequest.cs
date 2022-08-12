namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookRequest
    {
        public string? PhoneNumber { get; }

        public WebHookRequest(string? phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}