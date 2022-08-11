using Microsoft.AspNetCore.SignalR;
using System.Linq;
using System.Security.Claims;

namespace PhoneBox.Server
{
    internal sealed class PhoneNumberUserIdProvider : IUserIdProvider
    {
        public PhoneNumberUserIdProvider()
        {
        }
        public string? GetUserId(HubConnectionContext connection)
        {
            Claim? phoneNumberClaim = connection.User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.HomePhone);
            string phoneNumber = "101";
            return phoneNumber;
        }
    }
}