using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.SignalR
{
    public partial class TelephonyHub : Hub<ITelephonyHub>
    {
        private readonly ITelephonyConnector _connector;
        private readonly ILogger<TelephonyHub> _logger;

        public TelephonyHub(ITelephonyConnector connector, ILogger<TelephonyHub> logger)
        {
            _connector = connector;
            this._logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            string userid = Context.UserIdentifier!;
            _connector.Subscribe(new CallSubscriber(userid));
            _logger.LogInformation("Client connected: {Userid}", userid);
            return Task.CompletedTask;
        }
    }
}