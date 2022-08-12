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
        private readonly IDictionary<CallSubscriber, ITelephonySubscriptionHubPublisher> _registrations;
        private TAPIClass? _tapiClient;
        private TapiCallNotificationSink? _callNotification;

        public TapiConnector(ITelephonyHubPublisher hubPublisher)
        {
            this._hubPublisher = hubPublisher;
            this._registrations = new Dictionary<CallSubscriber, ITelephonySubscriptionHubPublisher>();
        }

        public void Register(CallSubscriber subscriber)
        {
            ITelephonySubscriptionHubPublisher subscriptionPublisher = _hubPublisher.RetrieveSubscriptionHubPublisher(subscriber);
            this._registrations.Add(subscriber, subscriptionPublisher);
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPIClass();
            _tapiClient.Initialize();
            _callNotification = new TapiCallNotificationSink(this._registrations);
            this._tapiClient!.ITTAPIEventNotification_Event_Event += _callNotification.Event;
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            this._tapiClient!.ITTAPIEventNotification_Event_Event -= _callNotification!.Event;
            this._tapiClient!.Shutdown();
            return Task.CompletedTask;
        }

        private sealed class TapiCallNotificationSink : ITTAPIEventNotification
        {
            private readonly IDictionary<CallSubscriber, ITelephonySubscriptionHubPublisher> _registrations;

            public TapiCallNotificationSink(IDictionary<CallSubscriber, ITelephonySubscriptionHubPublisher> registrations)
            {
                this._registrations = registrations;
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
                CallSubscriber subscriber = new CallSubscriber(call.Address.AddressName);
                if (!this._registrations.TryGetValue(subscriber, out ITelephonySubscriptionHubPublisher subscriptionHubPublisher))
                    return;

                string phoneNumber = call.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                string debugInfo = CallInfoAsText(tapiEvent, call);
                await subscriptionHubPublisher.OnCallState(new CallStateEvent(phoneNumber, debugInfo));
            }

            private async Task PublishCallNotificationEvent(TAPI_EVENT tapiEvent, ITCallNotificationEvent notificationEvent)
            {
                ITCallInfo call = notificationEvent.Call;
                CallSubscriber subscriber = new CallSubscriber(call.Address.AddressName);
                if (!this._registrations.TryGetValue(subscriber, out ITelephonySubscriptionHubPublisher subscriptionHubPublisher))
                    return;

                string phoneNumber = call.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                string debugInfo = CallInfoAsText(tapiEvent, call);
                await subscriptionHubPublisher.OnCallNotification(new CallNotificationEvent(phoneNumber, debugInfo));
            }

            private static string CallInfoAsText(TAPI_EVENT tapiEvent, ITCallInfo callInfo, string txt = "")
            {
                int callId = callInfo.CallInfoLong[CALLINFO_LONG.CIL_CALLID];
                string callerNumber = callInfo.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNUMBER];
                string callerName = callInfo.CallInfoString[CALLINFO_STRING.CIS_CALLERIDNAME];
                return $"{tapiEvent} #{callId} S:{callInfo.CallState}, P:[{callInfo.Privilege}], Cu:[{callerNumber}] {txt}.";
            }
        }
    }
}