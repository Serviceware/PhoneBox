using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
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
            services.AddCors(x => x.AddDefaultPolicy(y => y.WithOrigins(builder.Configuration["AllowedOrigins"]?.Split(';') ?? Array.Empty<string>())));
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

            app.UseCors();

            app.MapHub<TelephonyHub>("/TelephonyHub");

            if (isDevelopment)
                app.MapGet("/TelephonyHook/{fromPhoneNumber}/{toPhoneNumber}", (string fromPhoneNumber, string toPhoneNumber, ITelephonyHook hook, HttpContext context) => hook.HandleGet(fromPhoneNumber, toPhoneNumber, context));
            else
                app.MapPost("/TelephonyHook", (WebHookRequest request, ITelephonyHook hook, HttpContext context) => hook.HandlePost(request, context));

            await app.RunAsync().ConfigureAwait(false);
        }
    }
}