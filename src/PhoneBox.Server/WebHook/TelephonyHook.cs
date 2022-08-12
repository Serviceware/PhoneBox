using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;
using PhoneBox.Server.SignalR;

namespace PhoneBox.Server.WebHook
{
    internal sealed class TelephonyHook : ITelephonyHook
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyHook(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            this._hub = hub;
        }

        public Task HandleGet(string fromPhoneNumber, string toPhoneNumber, HttpContext context) => this.HandleWebHookRequest(fromPhoneNumber, toPhoneNumber, context);

        public Task HandlePost(WebHookRequest request, HttpContext context) => this.HandleWebHookRequest(request.FromPhoneNumber, request.ToPhoneNumber, context);

        private async Task HandleWebHookRequest(string fromPhoneNumber, string toPhoneNumber, HttpContext context)
        {
            await this._hub.Clients.User(toPhoneNumber).SendMessage($"Webhook called: {fromPhoneNumber}").ConfigureAwait(false);
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Thx!").ConfigureAwait(false);
        }
    }
}