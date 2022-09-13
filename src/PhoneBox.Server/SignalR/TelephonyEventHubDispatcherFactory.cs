using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.SignalR
{
    internal sealed class TelephonyEventHubDispatcherFactory : ITelephonyEventDispatcherFactory
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyEventHubDispatcherFactory(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            _hub = hub;
        }

        ITelephonyEventDispatcher ITelephonyEventDispatcherFactory.Create(CallSubscriber subscriber)
        {
            return new TelephonyEventHubDispatcher(_hub, subscriber.PhoneNumber);
        }

        private sealed class TelephonyEventHubDispatcher : ITelephonyEventDispatcher
        {
            private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;
            private readonly string _userid;

            public TelephonyEventHubDispatcher(IHubContext<TelephonyHub, ITelephonyHub> hub, string userid)
            {
                _hub = hub;
                _userid = userid;
            }

            async Task ITelephonyEventDispatcher.OnCallNotification(CallNotificationEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallNotification(call).ConfigureAwait(false);
            }

            async Task ITelephonyEventDispatcher.OnCallState(CallStateEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallState(call).ConfigureAwait(false);
            }
            async Task ITelephonyEventDispatcher.OnCallConnected(CallConnectedEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallConnected(call).ConfigureAwait(false);
            }
            async Task ITelephonyEventDispatcher.OnCallDisconnected(PhoneBox.Abstractions.CallDisconnectedEvent call)
            {
                await _hub.Clients.User(_userid).ReceiveCallDisconnected(call).ConfigureAwait(false);
            }
        }
    }
}