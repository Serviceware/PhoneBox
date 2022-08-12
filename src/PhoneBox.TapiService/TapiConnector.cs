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
        private readonly IDictionary<CallSubscriber, Func<string, Task>> _registrations;
        private TAPIClass? _tapiClient;
        private CallNotification? _callNotification;

        public TapiConnector(ITelephonyHubPublisher hubPublisher)
        {
            this._hubPublisher = hubPublisher;
            this._registrations = new Dictionary<CallSubscriber, Func<string, Task>>();
        }

        public void Register(CallSubscriber subscriber)
        {
            this._registrations.Add(subscriber, (phoneNumber => this._hubPublisher.OnCall(subscriber, phoneNumber)));
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPIClass();
            _tapiClient.Initialize();
            _callNotification = new CallNotification(this._registrations);
            this._tapiClient!.ITTAPIEventNotification_Event_Event += _callNotification.Event;
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            this._tapiClient!.ITTAPIEventNotification_Event_Event -= _callNotification!.Event;
            this._tapiClient!.Shutdown();
            return Task.CompletedTask;
        }

        private sealed class CallNotification : ITTAPIEventNotification
        {
            private readonly IDictionary<CallSubscriber, Func<string, Task>> _registrations;

            public CallNotification(IDictionary<CallSubscriber, Func<string, Task>> registrations)
            {
                this._registrations = registrations;
            }

            public async void Event(TAPI_EVENT TapiEvent, object pEvent)
            {
                if (TapiEvent != TAPI_EVENT.TE_CALLNOTIFICATION)
                {
                    return;
                }

                ITCallNotificationEvent notificationEvent = (ITCallNotificationEvent)pEvent;
                CallSubscriber subscriber = new CallSubscriber(notificationEvent.Call.Address.AddressName);
                if (this._registrations.TryGetValue(subscriber, out Func<string, Task> handler))
                {
                    string phoneNumber = CallInfoAsText(TapiEvent, notificationEvent.Call);
                    await handler(phoneNumber)
                }

            }
            private static string CallInfoAsText(TAPI_EVENT te, ITCallInfo callInfo, string txt = "")
            {
                int callid = callInfo.get_CallInfoLong(CALLINFO_LONG.CIL_CALLID);
                string callerNumber = callInfo.get_CallInfoString(CALLINFO_STRING.CIS_CALLERIDNUMBER);
                //string callerName = callInfo.get_CallInfoString(CALLINFO_STRING.CIS_CALLERIDNAME);

                return te
                    + " #" + callid
                    + " S:" + callInfo.CallState
                    + ", P:[" + callInfo.Privilege + "]"
                    + ", Cu:[" + callerNumber + "]"
                    //+ ", Ca:[" + callerName + "]"
                    + " " + txt
                    + ".";
            }
        }
    }
}
