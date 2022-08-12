using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;

namespace PhoneBox.Server
{
    internal sealed class TelephonyHub : Hub<ITelephonyHub>
    {
        private readonly ITelephonyConnector _connector;

        public TelephonyHub(ITelephonyConnector connector)
        {
            _connector = connector;
        }

        public override Task OnConnectedAsync()
        {
            string? userid = base.Context.UserIdentifier;
            //Claim? phoneNumberClaim = base.Context.User?.Claims.FirstOrDefault(x => x.Type == ClaimTypes.HomePhone);
            //string phoneNumber = "101";
            ///base.Context.Items["MyPhoneNo"] = phoneNumber;
            Console.WriteLine("Client connected:" + userid);
            return Task.CompletedTask;
        }
    }
}