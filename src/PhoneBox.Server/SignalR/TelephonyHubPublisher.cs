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

        ITelephonySubscriptionHubPublisher ITelephonyHubPublisher.RetrieveSubscriptionHubPublisher(CallSubscriber subscriber)
        {
            return new SubscriptionPublisher(_hub, subscriber.PhoneNumber);
        }


        private sealed class SubscriptionPublisher : ITelephonySubscriptionHubPublisher
        {
            private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;
            private readonly string _userid;

            public SubscriptionPublisher(IHubContext<TelephonyHub, ITelephonyHub> hub, string userid)
            {
                _hub = hub;
                _userid = userid;
            }

            async Task ITelephonySubscriptionHubPublisher.OnCallNotification(CallNotificationEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallNotification(call).ConfigureAwait(false);
            }

            async Task ITelephonySubscriptionHubPublisher.OnCallState(CallStateEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallState(call).ConfigureAwait(false);
            }
        }
    }
}