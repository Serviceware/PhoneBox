using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.SignalR
{
    internal sealed class TelephonyHubPublisher : ITelephonyHubPublisher
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyHubPublisher(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            _hub = hub;
        }

        public async Task OnCall(CallSubscriber subscriber, CallInfo call)
        {
            await _hub.Clients.User(subscriber.PhoneNumber).SendMessage("OnCall:" + call.PhoneNumber).ConfigureAwait(false);
        }
    }
}