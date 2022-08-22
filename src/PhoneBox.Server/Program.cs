using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
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
            builder.Configuration.AddJsonFile($"appsettings.{Environment.MachineName}.json", optional: true);
            bool isDevelopment = builder.Environment.IsDevelopment();

            AuthorizationOptions authorizationConfiguration = builder.Configuration.GetConfiguration<AuthorizationOptions>("Authorization");
            CorsOptions corsConfiguration = builder.Configuration.GetConfiguration<CorsOptions>("CORS");

            IServiceCollection services = builder.Services;
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(x =>
                    {
                        x.Authority = authorizationConfiguration.Authority;
                        x.TokenValidationParameters.ValidAudience = authorizationConfiguration.Audience;
                        x.RequireHttpsMetadata = !isDevelopment || authorizationConfiguration.Authority?.StartsWith("http:", StringComparison.OrdinalIgnoreCase) is null or false;
                    });
            services.AddCors(x => x.AddDefaultPolicy(y => y.AllowCredentials()
                                                           .AllowAnyHeader()
                                                           .WithMethods("GET", "POST")
                                                           .WithOrigins(corsConfiguration.AllowedOrigins?.Split(';') ?? Array.Empty<string>())));
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

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors();

            app.MapHub<TelephonyHub>("/TelephonyHub");

            if (isDevelopment)
            {
                app.MapGet("/TelephonyHook/{fromPhoneNumber}/{toPhoneNumber}", (string fromPhoneNumber, string toPhoneNumber, ITelephonyHook hook, HttpContext context) => hook.HandleGet(fromPhoneNumber, toPhoneNumber, context));
            }
            else
            {
                app.MapPost("/TelephonyHook", (WebHookRequest request, ITelephonyHook hook, HttpContext context) => hook.HandlePost(request, context))
                   .RequireAuthorization();
            }

            await app.RunAsync().ConfigureAwait(false);
        }
    }
}