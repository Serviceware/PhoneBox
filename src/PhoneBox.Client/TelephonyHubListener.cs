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
        private HubConnection? _connection;

        public TelephonyHubListener(IAccessTokenProvider accessTokenProvider)
        {
            this._accessTokenProvider = accessTokenProvider;
        }

        #region IHostedService Members
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting listener");

            this._connection = new HubConnectionBuilder().WithUrl("https://localhost:63440/TelephonyHub", x =>
                                                         {
                                                             x.AccessTokenProvider = this._accessTokenProvider.GetAccessToken;
                                                         })
                                                         .WithAutomaticReconnect()
                                                         .Build();
            this._connection.Closed += OnHubConnectionClosed;
            _ = this._connection.On<string>(this.SendMessage);
            _ = this._connection.On<CallNotificationEvent>(this.ReceiveCallNotification);
            _ = this._connection.On<CallStateEvent>(this.ReceiveCallState);

            int retries = 0;
            while (true)
            {
                try
                {
                    await this._connection.StartAsync(cancellationToken).ConfigureAwait(false);
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    if (++retries == 5)
                        throw;
                }
            }

            Console.WriteLine("Listener started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping listener");
            await (this._connection?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
            Console.WriteLine("Listener stopped");
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