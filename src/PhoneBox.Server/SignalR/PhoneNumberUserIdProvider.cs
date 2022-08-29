using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace PhoneBox.Server.SignalR
{
    internal sealed class PhoneNumberUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            Claim? phoneNumberClaim = connection.User?.Claims.FirstOrDefault(x => x.Type == ClaimType.PhoneNumber);
            return phoneNumberClaim?.Value;
        }
    }
}