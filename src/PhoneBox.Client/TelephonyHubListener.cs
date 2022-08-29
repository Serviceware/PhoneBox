using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.Client
{
    internal sealed class TelephonyHubListener : IHostedService, ITelephonyHub
    {
        private readonly IAccessTokenProvider _accessTokenProvider;

        public TelephonyHubListener(IAccessTokenProvider accessTokenProvider)
        {
            this._accessTokenProvider = accessTokenProvider;
        }

        #region IHostedService Members
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting listener");

            HubConnection connection = new HubConnectionBuilder().WithUrl("https://localhost:63440/TelephonyHub", x =>
                                                                 {
                                                                     x.AccessTokenProvider = this._accessTokenProvider.GetAccessToken;
                                                                 })
                                                                 .WithAutomaticReconnect()
                                                                 .Build();
            connection.Closed += OnHubConnectionClosed;
            connection.On<string>(this.SendMessage);
            connection.On<CallNotificationEvent>(this.ReceiveCallNotification);
            connection.On<CallStateEvent>(this.ReceiveCallState);

            await connection.StartAsync(cancellationToken).ConfigureAwait(false);

            Console.WriteLine("Listener started");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping listener");
            Console.WriteLine("Listener stopped");
            return Task.CompletedTask;
        }

        private static Task OnHubConnectionClosed(Exception? exception)
        {
            Console.WriteLine("Hub connection closed");
            if (exception != null)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(exception);
                }
                finally
                {
                    Console.ResetColor();

                }
            }

            return Task.CompletedTask;
        }
        #endregion

        #region ITelephonyHub Members
        public Task SendMessage(string message)
        {
            Console.WriteLine($"Received message: {message}");
            return Task.CompletedTask;
        }

        public Task ReceiveCallNotification(CallNotificationEvent call)
        {
            Console.WriteLine($"Received call notification: {call.CallerPhoneNumber} {call.CallStateKey} {call.HasCallControl} ==> {call.DebugInfo}");
            return Task.CompletedTask;
        }

        public Task ReceiveCallState(CallStateEvent call)
        {
            Console.WriteLine($"Received call state: {call.DebugInfo}");
            return Task.CompletedTask;
        }
        #endregion
    }
}