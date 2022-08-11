using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace PhoneBox.Server
{
    internal sealed class TelephonyHub : Hub<ITelephonyHub>
    {
        public override Task OnConnectedAsync()
        {
            Claim? phoneNumberClaim = base.Context.User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.HomePhone);
            Console.WriteLine("Client connected");
            return Task.CompletedTask;
        }
    }
}