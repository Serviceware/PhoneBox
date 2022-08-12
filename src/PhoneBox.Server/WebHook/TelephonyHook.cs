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

        public Task HandleGet(string phoneNumber, HttpContext context) => this.HandleWebHookRequest(phoneNumber, context);

        public Task HandlePost(WebHookRequest request, HttpContext context) => this.HandleWebHookRequest(request.PhoneNumber!, context);

        private async Task HandleWebHookRequest(string phoneNumber, HttpContext context)
        {
            await this._hub.Clients.User(phoneNumber).SendMessage($"Webhook called: {phoneNumber}").ConfigureAwait(false);
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Thx!").ConfigureAwait(false);
        }
    }
}