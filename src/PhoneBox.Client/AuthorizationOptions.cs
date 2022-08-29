namespace PhoneBox.Client
{
    internal sealed class AuthorizationOptions
    {
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }
}