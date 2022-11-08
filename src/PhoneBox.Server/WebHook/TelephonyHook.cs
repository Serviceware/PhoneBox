using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PhoneBox.Abstractions;

namespace PhoneBox.Server
{
    internal partial class TelephonyHook
    {
        private readonly ITelephonyEventDispatcherFactory _telephonyEventDispatcherFactory;

        public TelephonyHook(ITelephonyEventDispatcherFactory telephonyEventDispatcherFactory)
        {
            this._telephonyEventDispatcherFactory = telephonyEventDispatcherFactory;
        }

        public Task OnCallConnected(HttpContext context, WebHookCallConnectedRequest request) => this.HandleWebHookRequest(request.FromPhoneNumber, request.ToPhoneNumber, context, (x, y) => x.OnCallConnected(new CallConnectedEvent(y)));

        public Task OnCallDisconnected(HttpContext context, WebHookCallDisconnectedRequest request) => this.HandleWebHookRequest(request.FromPhoneNumber, request.ToPhoneNumber, context, (x, y) => x.OnCallDisconnected(new CallDisconnectedEvent(y)));

        private async Task HandleWebHookRequest(string fromPhoneNumber, string toPhoneNumber, HttpContext context, Func<ITelephonyEventDispatcher, string, Task> handler)
        {
            ITelephonyEventDispatcher telephonyEventDispatcher = this._telephonyEventDispatcherFactory.Create(new CallSubscriber(toPhoneNumber));
            await handler(telephonyEventDispatcher, fromPhoneNumber).ConfigureAwait(false);
            context.Response.StatusCode = StatusCodes.Status202Accepted;
        }
    }
}