using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;

namespace PhoneBox.Client
{
    internal sealed class TelephonyHubListener : IHostedService, ITelephonyHub
    {
        private readonly IAccessTokenProvider _accessTokenProvider;
        private readonly ILogger<TelephonyHubListener> _logger;
        private HubConnection? _connection;

        public TelephonyHubListener(IAccessTokenProvider accessTokenProvider, ILogger<TelephonyHubListener> logger)
        {
            this._accessTokenProvider = accessTokenProvider;
            this._logger = logger;
        }

        #region IHostedService Members
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation("Starting listener");

            this._connection = new HubConnectionBuilder().WithUrl("https://localhost:63440/TelephonyHub", x =>
                                                         {
                                                             x.AccessTokenProvider = this._accessTokenProvider.GetAccessToken;
                                                         })
                                                         .WithAutomaticReconnect()
                                                         .Build();
            this._connection.Closed += this.OnHubConnectionClosed;
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
                    this._logger.LogError(e, message: null);
                    if (++retries == 5)
                        throw;
                }
            }

            this._logger.LogInformation("Listener started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this._logger.LogInformation("Stopping listener");
            await (this._connection?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
            this._logger.LogInformation("Listener stopped");
        }

        private Task OnHubConnectionClosed(Exception? exception)
        {
            this._logger.LogInformation("Hub connection closed");
            if (exception != null) 
                this._logger.LogError(exception, message: null);

            return Task.CompletedTask;
        }
        #endregion

        #region ITelephonyHub Members
        public Task ReceiveCallNotification(CallNotificationEvent call)
        {
            this._logger.LogInformation("Received call notification: {CallerPhoneNumber} {CallStateKey} {ca.HasCallControl} ==> {DebugInfo}", call.CallerPhoneNumber, call.CallStateKey, call.HasCallControl, call.DebugInfo);
            return Task.CompletedTask;
        }

        public Task ReceiveCallState(CallStateEvent call)
        {
            this._logger.LogInformation("Received call state: {DebugInfo}", call.DebugInfo);
            return Task.CompletedTask;
        }
        #endregion
    }
}