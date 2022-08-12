using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;
using PhoneBox.TapiService;

namespace PhoneBox.Server
{
    internal sealed class TelephonyHubPublisher : ITelephonyHubPublisher
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyHubPublisher(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            this._hub = hub;
        }

        public async Task OnCall(CallSubscriber subscriber, string phoneNumber)
        {
            await this._hub.Clients.User(subscriber.PhoneNumber).SendMessage("OnCall:" + phoneNumber).ConfigureAwait(false);
        }
    }
}