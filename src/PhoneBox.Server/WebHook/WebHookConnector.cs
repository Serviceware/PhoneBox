using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookConnector : ITelephonyConnector, ITelephonyConnectorSetup
    {
        void ITelephonyConnector.Register(CallSubscriber subscriber) { }

        void ITelephonyConnectorSetup.Setup(WebApplication application)
        {
            if (application.Environment.IsDevelopment())
            {
                application.MapGet("/TelephonyHook/{fromPhoneNumber}/{toPhoneNumber}", (string fromPhoneNumber, string toPhoneNumber, ITelephonyHook hook, HttpContext context) => hook.HandleGet(fromPhoneNumber, toPhoneNumber, context));
            }

            application.MapPost("/TelephonyHook", (WebHookRequest request, ITelephonyHook hook, HttpContext context) => hook.HandlePost(request, context))
                       .RequireAuthorization("WebHookConsumer");
        }
    }
}