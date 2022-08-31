using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;
using PhoneBox.Server.SignalR;

namespace PhoneBox.Server
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile($"appsettings.{Environment.MachineName}.json", optional: true);
            bool isDevelopment = builder.Environment.IsDevelopment();

            IConfigurationSection authorizationConfiguration = builder.Configuration.GetSection("Authorization");
            AuthorizationOptions authorizationOptions = authorizationConfiguration.Bind<AuthorizationOptions>();
            CorsOptions corsConfiguration = builder.Configuration.Bind<CorsOptions>("CORS");

            IServiceCollection services = builder.Services;
            services.Configure<AuthorizationOptions>(authorizationConfiguration);
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(x =>
                    {
                        x.Authority = authorizationOptions.Authority;
                        x.TokenValidationParameters.ValidAudience = authorizationOptions.Audience;
                        x.RequireHttpsMetadata = !isDevelopment || authorizationOptions.Authority?.StartsWith("http:", StringComparison.OrdinalIgnoreCase) is null or false;
                    });
            services.AddAuthorization(x =>
            {
                x.AddPolicy("HubConsumer", y => y.RequireAuthenticatedUser()
                                                 .RequireClaim(authorizationOptions.SubscriberIdClaimType)
                                                 .Build());
            });
            services.AddCors(x =>
            {
                x.AddDefaultPolicy(y => y.AllowCredentials()
                                         .AllowAnyHeader()
                                         .WithMethods("GET", "POST")
                                         .WithOrigins(corsConfiguration.AllowedOrigins?.Split(';') ?? Array.Empty<string>()));
            });
            services.AddSignalR();
            services.AddSingleton<ITelephonyEventDispatcherFactory, TelephonyEventHubDispatcherFactory>();
            services.AddSingleton<IUserIdProvider, SubscriberIdClaimUserIdProvider>();

            if (isDevelopment)
            {
                // Periodically sends messages to the hub for debugging purposes
                services.AddSingleton<IHostedService, TelephonyHubWorker>();
            }

            TelephonyConnectorRegistrar.RegisterProvider(builder);

            WebApplication app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors();

            app.MapHub<TelephonyHub>()
               .RequireAuthorization("HubConsumer");

            TelephonyConnectorRegistrar.ConfigureProvider(app);

            await app.RunAsync().ConfigureAwait(false);
        }
    }
}