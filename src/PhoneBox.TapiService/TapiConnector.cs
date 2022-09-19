using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;
using TAPI3Lib;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnector : IHostedService, ITelephonyConnector
    {
        private readonly ITelephonyEventDispatcherFactory _eventDispatcherFactory;
        private readonly ILogger<TapiConnector> _logger;
        private readonly TapiEventNotificationSink _callNotificationSink;

        private TAPIClass? _tapiClient;

        public TapiConnector(ITelephonyEventDispatcherFactory eventDispatcherFactory, ILogger<TapiConnector> logger)
        {
            _eventDispatcherFactory = eventDispatcherFactory;
            _logger = logger;
            _callNotificationSink = new TapiEventNotificationSink();
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPIClass();
            _tapiClient.Initialize();
            _tapiClient.ITTAPIEventNotification_Event_Event += _callNotificationSink.Event;
            _tapiClient.EventFilter = (int)(
                TAPI_EVENT.TE_CALLNOTIFICATION |
                TAPI_EVENT.TE_CALLSTATE |
                TAPI_EVENT.TE_DIGITEVENT |
                TAPI_EVENT.TE_PHONEEVENT |
                TAPI_EVENT.TE_GENERATEEVENT |
                TAPI_EVENT.TE_GATHERDIGITS |
                TAPI_EVENT.TE_REQUEST
                );
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            TAPIClass client = GetClientSafe();
            client.ITTAPIEventNotification_Event_Event -= _callNotificationSink!.Event;
            client.Shutdown();
            return Task.CompletedTask;
        }

        void ITelephonyConnector.Subscribe(CallSubscriberConnection connection)
        {
            CallSubscriber subscriber = connection.Subscriber;
            ITAddress? tapiAddress = FindTapiAddressForSubscriber(subscriber);
            if (tapiAddress == null)
            {
                throw new InvalidOperationException($"Could not resolve TAPI address by phone number: {subscriber.PhoneNumber}");
            }

            if (_callNotificationSink.IsRegistered(tapiAddress))
            {
                _callNotificationSink.AddConnectionToSubscription(connection);
            }
            else
            {
                int subscriptionId = GetClientSafe().RegisterCallNotifications(tapiAddress, fMonitor: true, fOwner: true, lMediaTypes: TapiConstants.TAPIMEDIATYPE_AUDIO, lCallbackInstance: 0);
                ITelephonyEventDispatcher eventDispatcher = _eventDispatcherFactory.Create(subscriber);
                _callNotificationSink.AddSubscriber(tapiAddress, connection, eventDispatcher, subscriptionId);
            }
        }

        void ITelephonyConnector.Unsubscribe(CallSubscriberConnection connection)
        {
            _callNotificationSink.RemoveSubscriber(connection, GetClientSafe());
        }

        private ITAddress? FindTapiAddressForSubscriber(CallSubscriber subscriber)
        {
            IEnumAddress addresses = GetClientSafe().EnumerateAddresses();
            uint fetched = 0;
            do
            {
                addresses.Next(1, out ITAddress address, ref fetched);
                if (fetched == 1 && address != null)
                {
                    string addressType = address.GetType().FullName!;
                    _logger.LogTrace(@"AddressType: {AddressType}
ServiceProviderName: {ServiceProviderName}
AddressName: {AddressName}
DialableAddress: {DialableAddress}", addressType, address.ServiceProviderName, address.AddressName, address.DialableAddress);
                    if (string.Equals(subscriber.PhoneNumber, address.AddressName, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(subscriber.PhoneNumber, address.DialableAddress, StringComparison.OrdinalIgnoreCase)
                        )
                    {
                        return address;
                    }
                }
            } while (fetched > 0);

            return null;
        }

        private TAPIClass GetClientSafe()
        {
            if (_tapiClient == null)
                throw new InvalidOperationException("Tapi client not initialized");

            return _tapiClient;
        }

        private sealed class TapiEventNotificationSink : ITTAPIEventNotification
        {
            private readonly IDictionary<string, TapiAddressSubscription> _addressSubscriptionMap;
            private readonly IDictionary<string, TapiAddressSubscription> _subscriberAddressMap;

            public TapiEventNotificationSink()
            {
                _addressSubscriptionMap = new SortedDictionary<string, TapiAddressSubscription>();
                _subscriberAddressMap = new Dictionary<string, TapiAddressSubscription>();
            }

            public bool IsRegistered(ITAddress tapiAddress) => _addressSubscriptionMap.ContainsKey(tapiAddress.AddressName);
            
            public void AddConnectionToSubscription(CallSubscriberConnection connection)
            {
                TapiAddressSubscription subscription = GetSubscription(connection.Subscriber);
                subscription.Connections.Add(connection);
            }

            public void AddSubscriber(ITAddress address, CallSubscriberConnection connection, ITelephonyEventDispatcher eventDispatcher, int subscriptionId)
            {
                string addressName = address.AddressName;
                if (_addressSubscriptionMap.ContainsKey(addressName))
                {
                    throw new InvalidOperationException(@$"Address already registered: {addressName}
ConnectionId: {connection.ConnectionId}
SubscriberPhoneNumber: {connection.Subscriber.PhoneNumber}");
                }

                var addressRegistration = new TapiAddressSubscription(address, eventDispatcher, subscriptionId, connection);
                _addressSubscriptionMap.Add(addressName, addressRegistration);
                _subscriberAddressMap.Add(connection.Subscriber.PhoneNumber, addressRegistration);
            }

            public void RemoveSubscriber(CallSubscriberConnection connection, ITTAPI client)
            {
                CallSubscriber subscriber = connection.Subscriber;
                TapiAddressSubscription subscription = GetSubscription(subscriber);
                if (subscription.Connections.Count < 2)
                {
                    client.UnregisterNotifications(subscription.SubscriptionId);
                    _subscriberAddressMap.Remove(subscriber.PhoneNumber);
                    _addressSubscriptionMap.Remove(subscription.AddressName);
                }
                else
                {
                    subscription.Connections.Remove(connection);
                }
            }

            public async void Event(TAPI_EVENT tapiEvent, object pEvent)
            {
                switch (tapiEvent)
                {
                    case TAPI_EVENT.TE_CALLSTATE:
                        await PublishCallStateEvent((ITCallStateEvent)pEvent).ConfigureAwait(false);
                        break;
                }
            }

            private async Task PublishCallStateEvent(ITCallStateEvent stateEvent)
            {
                ITCallInfo call = stateEvent.Call;
                if (!_addressSubscriptionMap.TryGetValue(call.Address.AddressName, out TapiAddressSubscription registration))
                    return;

                string phoneNumber = call.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                switch (call.CallState)
                {
                    case CALL_STATE.CS_CONNECTED:
                        await registration.Publisher.OnCallConnected(new CallConnectedEvent(phoneNumber)).ConfigureAwait(false);
                        break;

                    case CALL_STATE.CS_DISCONNECTED:
                        await registration.Publisher.OnCallDisconnected(new CallDisconnectedEvent(phoneNumber)).ConfigureAwait(false);
                        break;
                }
            }

            private TapiAddressSubscription GetSubscription(CallSubscriber subscriber)
            {
                if (!_subscriberAddressMap.TryGetValue(subscriber.PhoneNumber, out TapiAddressSubscription subscription))
                    throw new InvalidOperationException($"No subscriptions registered for phone number: {subscriber.PhoneNumber}");

                return subscription;
            }
        }

        private readonly struct TapiAddressSubscription
        {
            public string AddressName { get; }
            public ITelephonyEventDispatcher Publisher { get; }
            public int SubscriptionId { get; }
            public ICollection<CallSubscriberConnection> Connections { get; }

            public TapiAddressSubscription(ITAddress address, ITelephonyEventDispatcher publisher, int subscriptionId, CallSubscriberConnection connection)
            {
                AddressName = address.AddressName;
                Publisher = publisher;
                SubscriptionId = subscriptionId;
                Connections = new Collection<CallSubscriberConnection> { connection };
            }
        }
    }
}