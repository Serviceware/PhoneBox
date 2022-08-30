using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.Server.WebHook
{
    internal sealed class WebHookConnectorRegistrar : TelephonyConnectorRegistrar<WebHookConnector>
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ITelephonyHook, TelephonyHook>();
            services.AddAuthorization(x =>
            {
                x.AddPolicy("WebHookConsumer", y => y.RequireAuthenticatedUser()
                                                     .Build());
            });
        }

        public override void ConfigureApplication(WebApplication application)
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