using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class TelephonyHook : ITelephonyHook
    {
        private readonly ITelephonyEventDispatcherFactory _telephonyEventDispatcherFactory;

        public TelephonyHook(ITelephonyEventDispatcherFactory telephonyEventDispatcherFactory)
        {
            this._telephonyEventDispatcherFactory = telephonyEventDispatcherFactory;
        }

        public Task HandleGet(string fromPhoneNumber, string toPhoneNumber, HttpContext context) => this.HandleWebHookRequest(fromPhoneNumber, toPhoneNumber, context);

        public Task HandlePost(WebHookRequest request, HttpContext context) => this.HandleWebHookRequest(request.FromPhoneNumber, request.ToPhoneNumber, context);

        private async Task HandleWebHookRequest(string fromPhoneNumber, string toPhoneNumber, HttpContext context)
        {
            ITelephonyEventDispatcher telephonyEventDispatcher = this._telephonyEventDispatcherFactory.Create(new CallSubscriber(toPhoneNumber));
            CallConnectedEvent call = new CallConnectedEvent(fromPhoneNumber);
            await telephonyEventDispatcher.OnCallConnected(call).ConfigureAwait(false);
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Thx!").ConfigureAwait(false);
        }
    }
}