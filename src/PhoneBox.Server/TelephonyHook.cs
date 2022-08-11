using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace PhoneBox.Server
{
    internal sealed class TelephonyHook : ITelephonyHook
    {
        private readonly IHubContext<TelephonyHub, ITelephonyHub> _hub;

        public TelephonyHook(IHubContext<TelephonyHub, ITelephonyHub> hub)
        {
            this._hub = hub;
        }

        public async Task Handle(HttpContext context)
        {
            await this._hub.Clients.All.SendMessage("Webhook called!").ConfigureAwait(false);
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Thx!").ConfigureAwait(false);
        }
    }
}