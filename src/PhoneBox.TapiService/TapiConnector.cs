﻿using System;
using System.Collections.Generic;
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
        private readonly TapiEventNotificationSink _callNotification;

        private TAPIClass? _tapiClient;

        public TapiConnector(ITelephonyEventDispatcherFactory eventDispatcherFactory, ILogger<TapiConnector> logger)
        {
            _eventDispatcherFactory = eventDispatcherFactory;
            _logger = logger;
            _callNotification = new TapiEventNotificationSink();
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPIClass();
            _tapiClient.Initialize();
            _tapiClient.ITTAPIEventNotification_Event_Event += _callNotification.Event;
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
            _tapiClient!.ITTAPIEventNotification_Event_Event -= _callNotification!.Event;
            _tapiClient!.Shutdown();
            return Task.CompletedTask;
        }

        void ITelephonyConnector.Subscribe(CallSubscriber subscriber)
        {
            ITAddress? tapiAddress = FindTapiAddressForSubscriber(subscriber);
            if (tapiAddress == null)
            {
                // Boom?
            }
            else
            {
                _tapiClient!.RegisterCallNotifications(tapiAddress, fMonitor: true, fOwner: true, lMediaTypes: TapiConstants.TAPIMEDIATYPE_AUDIO, lCallbackInstance: 0);

                ITelephonyEventDispatcher eventDispatcher = _eventDispatcherFactory.Create(subscriber);
                _callNotification.AddAddressRegistration(tapiAddress, subscriber, eventDispatcher);
            }

        }

        private ITAddress? FindTapiAddressForSubscriber(CallSubscriber subscriber)
        {
            IEnumAddress addresses = _tapiClient!.EnumerateAddresses();
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

        private sealed class TapiEventNotificationSink : ITTAPIEventNotification
        {
            private readonly IDictionary<string, TapiAddressSubscription> _registrations;

            public TapiEventNotificationSink()
            {
                _registrations = new SortedDictionary<string, TapiAddressSubscription>();
            }

            public void AddAddressRegistration(ITAddress address, CallSubscriber subscriber, ITelephonyEventDispatcher eventDispatcher)
            {
                var addressRegistration = new TapiAddressSubscription(address, subscriber, eventDispatcher);
                _registrations.Add(address.AddressName, addressRegistration);
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
                if (!_registrations.TryGetValue(call.Address.AddressName, out TapiAddressSubscription registration))
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
        }

        private readonly struct TapiAddressSubscription
        {
            public string AddressName { get; }
            public CallSubscriber Subscriber { get; }
            public ITelephonyEventDispatcher Publisher { get; }

            public TapiAddressSubscription(ITAddress address, CallSubscriber subscriber, ITelephonyEventDispatcher publisher)
            {
                AddressName = address.AddressName;
                Subscriber = subscriber;
                Publisher = publisher;
            }
        }
    }
}