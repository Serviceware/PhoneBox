using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.Server
{
    internal sealed class TelephonyHubPublisher : BackgroundService
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyHubPublisher(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            this._hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                string phoneNumber = "101";
                await this._hub.Clients.User(phoneNumber).SendMessage("Hey there:" + phoneNumber).ConfigureAwait(false);
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}