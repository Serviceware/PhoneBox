using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace PhoneBox.Server.SignalR
{
    internal sealed class SubscriberIdClaimUserIdProvider : IUserIdProvider
    {
        private readonly IOptionsMonitor<AuthorizationOptions> _authorizationOptions;

        public SubscriberIdClaimUserIdProvider(IOptionsMonitor<AuthorizationOptions> authorizationOptions)
        {
            this._authorizationOptions = authorizationOptions;
        }

        public string? GetUserId(HubConnectionContext connection)
        {
            string? phoneNumber = connection.User.FindFirstValue(this._authorizationOptions.CurrentValue.SubscriberIdClaimType);
            return phoneNumber;
        }
    }
}