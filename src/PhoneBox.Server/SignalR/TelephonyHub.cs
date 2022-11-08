using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.SignalR
{
    public partial class TelephonyHub
    {
        private readonly ITelephonyConnector _connector;
        private readonly ILogger<TelephonyHub> _logger;

        public TelephonyHub(ITelephonyConnector connector, ILogger<TelephonyHub> logger)
        {
            _connector = connector;
            _logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            string connectionId = Context.ConnectionId;
            string userid = GetUserIdSafe();
            _connector.Subscribe(new CallSubscriberConnection(connectionId, new CallSubscriber(userid)));
            _logger.LogInformation("Client connected: [ConnectionId: {ConnectionId}] [UserId: {Userid}]", connectionId, userid);
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;
            string userid = GetUserIdSafe();
            _connector.Unsubscribe(new CallSubscriberConnection(connectionId, new CallSubscriber(userid)));
            StringBuilder sb = new StringBuilder("Client disconnected: [ConnectionId: {ConnectionId}] [UserId: {Userid}]");
            if (exception != null)
            {
                sb.Append(@"
Exception: {Exception}");
            }
            string message = sb.ToString();
            _logger.LogInformation(message, connectionId, userid, exception);
            return Task.CompletedTask;
        }

        private string GetUserIdSafe()
        {
            string? userIdentifier = Context.UserIdentifier;
            if (String.IsNullOrEmpty(userIdentifier))
                throw new InvalidOperationException("Unresolved user identifier from SignalR connection");

            return userIdentifier;
        }
    }
}