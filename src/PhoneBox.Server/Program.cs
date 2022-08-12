using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;
using PhoneBox.Server.SignalR;
using PhoneBox.Server.WebHook;

namespace PhoneBox.Server
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            bool isDevelopment = builder.Environment.IsDevelopment();

            IServiceCollection services = builder.Services;
            services.AddSignalR();
            services.AddSingleton<ITelephonyHook, TelephonyHook>();
            services.AddSingleton<ITelephonyHubPublisher, TelephonyHubPublisher>();
            services.AddSingleton<IUserIdProvider, PhoneNumberUserIdProvider>();

            if (isDevelopment)
            {
                // Periodically sends messages to the hub for debugging purposes
                services.AddSingleton<IHostedService, TelephonyHubWorker>();
            }

            TelephonyConnectorRegistrar.RegisterProvider(builder, services);

            WebApplication app = builder.Build();

            app.MapHub<TelephonyHub>("/TelephonyHub");
            app.MapMethods
            (
                pattern: "/TelephonyHook/{phoneNumber}"
              , httpMethods: EnumerableExtensions.Create((isDevelopment ? HttpMethod.Get : HttpMethod.Post).Method)
              , handler: (string phoneNumber, ITelephonyHook hook, HttpContext context) => hook.Handle(phoneNumber, context)
            );
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}