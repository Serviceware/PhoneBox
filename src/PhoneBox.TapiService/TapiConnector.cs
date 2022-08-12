using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;
using TAPI3Lib;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnector : IHostedService, ITelephonyConnector
    {
        private readonly ITelephonyHubPublisher _hubPublisher;
        private readonly TapiEventNotificationSink _callNotification;

        private TAPIClass? _tapiClient;

        public TapiConnector(ITelephonyHubPublisher hubPublisher)
        {
            _hubPublisher = hubPublisher;
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

        private sealed class TapiEventNotificationSink : ITTAPIEventNotification
        {
            private readonly IDictionary<string, TapiAddressSubscription> _registrations;

            private sealed class TapiAddressSubscription
            {
                internal readonly string AddressName;
                internal readonly CallSubscriber Subscriber;
                internal readonly ITelephonySubscriptionHubPublisher Publisher;
                public TapiAddressSubscription(ITAddress address, CallSubscriber subscriber, ITelephonySubscriptionHubPublisher subscriptionPublisher)
                {
                    AddressName = address.AddressName;
                    Subscriber = subscriber;
                    Publisher = subscriptionPublisher;
                }
            }

            public TapiEventNotificationSink()
            {
                _registrations = new SortedDictionary<string, TapiAddressSubscription>();
            }

            public void AddAddressRegistration(ITAddress address, CallSubscriber subscriber, ITelephonySubscriptionHubPublisher subscriptionPublisher)
            {
                var addressRegistration = new TapiAddressSubscription(address, subscriber, subscriptionPublisher);
                _registrations.Add(address.AddressName, addressRegistration);
            }

            public async void Event(TAPI_EVENT tapiEvent, object pEvent)
            {
                if (tapiEvent == TAPI_EVENT.TE_CALLNOTIFICATION)
                {
                    await PublishCallNotificationEvent(tapiEvent, (ITCallNotificationEvent)pEvent);
                }
                else if (tapiEvent == TAPI_EVENT.TE_CALLSTATE)
                {
                    await PublishCallStateEvent(tapiEvent, (ITCallStateEvent)pEvent);
                }
            }

            private async Task PublishCallStateEvent(TAPI_EVENT tapiEvent, ITCallStateEvent stateEvent)
            {
                ITCallInfo call = stateEvent.Call;
                if (!this._registrations.TryGetValue(call.Address.AddressName, out TapiAddressSubscription registration))
                    return;

                string phoneNumber = call.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                string debugInfo = CallInfoAsText(tapiEvent, call);
                await registration.Publisher.OnCallState(new CallStateEvent(phoneNumber, debugInfo));
            }

            private async Task PublishCallNotificationEvent(TAPI_EVENT tapiEvent, ITCallNotificationEvent notificationEvent)
            {
                ITCallInfo call = notificationEvent.Call;
                if (!this._registrations.TryGetValue(call.Address.AddressName, out TapiAddressSubscription registration))
                    return;

                string phoneNumber = call.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                string debugInfo = CallInfoAsText(tapiEvent, call);
                await registration.Publisher.OnCallNotification(new CallNotificationEvent(phoneNumber, debugInfo));
            }

            private static string CallInfoAsText(TAPI_EVENT tapiEvent, ITCallInfo callInfo, string txt = "")
            {
                int callId = callInfo.CallInfoLong[CALLINFO_LONG.CIL_CALLID];
                string callerNumber = callInfo.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                //string callerName = callInfo.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNAME];
                return $"{tapiEvent} #{callId} S:{callInfo.CallState}, P:[{callInfo.Privilege}], Cu:[{callerNumber}] {txt}.";
            }
        }

        void ITelephonyConnector.Register(CallSubscriber subscriber)
        {
            ITAddress? tapiAddress = FindTapiAddressForSubscriber(subscriber);
            if (tapiAddress == null)
            {
                // Boom?
            }
            else
            {
                _tapiClient!.RegisterCallNotifications(tapiAddress, fMonitor: true, fOwner: true, lMediaTypes: TapiConstants.TAPIMEDIATYPE_AUDIO, lCallbackInstance: 0);

                ITelephonySubscriptionHubPublisher subscriptionPublisher = _hubPublisher.RetrieveSubscriptionHubPublisher(subscriber);
                _callNotification.AddAddressRegistration(tapiAddress, subscriber, subscriptionPublisher);
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
                    Console.WriteLine("address:" + address.GetType().FullName);
                    Console.WriteLine("  address.ServiceProviderName:" + address.ServiceProviderName);
                    Console.WriteLine("    address.AddressName:" + address.AddressName);
                    Console.WriteLine("    address.DialableAddress:" + address.DialableAddress);
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
    }
}