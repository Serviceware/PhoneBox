using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.SignalR
{
    public partial class TelephonyHub : Hub<ITelephonyHub>
    {
        private readonly ITelephonyConnector _connector;

        public TelephonyHub(ITelephonyConnector connector)
        {
            _connector = connector;
        }

        public override Task OnConnectedAsync()
        {
            string userid = Context.UserIdentifier!;
            _connector.Subscribe(new CallSubscriber(userid));
            Console.WriteLine($"Client connected: {userid}");
            return Task.CompletedTask;
        }
    }
}