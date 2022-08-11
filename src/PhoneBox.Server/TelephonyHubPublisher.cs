using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

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
                await this._hub.Clients.All.SendMessage("Hey there").ConfigureAwait(false);
                await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}