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
            builder.Configuration.AddJsonFile($"appsettings.{Environment.MachineName}.json", optional: true, reloadOnChange: true);
            bool isDevelopment = builder.Environment.IsDevelopment();

            IConfigurationSection authorizationConfiguration = builder.Configuration.GetSection("Authorization");
            AuthorizationOptions authorizationOptions = authorizationConfiguration.Bind<AuthorizationOptions>();
            CorsOptions corsConfiguration = builder.Configuration.Bind<CorsOptions>("CORS");

            IServiceCollection services = builder.Services;
            services.Configure<AuthorizationOptions>(authorizationConfiguration);
            services.AddAuthentication()
                    .AddJwtBearer("HubConsumer", x =>
                    {
                        x.Authority = authorizationOptions.Authority;
                        x.TokenValidationParameters.ValidAudience = authorizationOptions.Audience;
                        x.RequireHttpsMetadata = !isDevelopment || authorizationOptions.Authority?.StartsWith("http:", StringComparison.OrdinalIgnoreCase) is null or false;

                        // We have to hook the OnMessageReceived event in order to
                        // allow the JWT authentication handler to read the access
                        // token from the query string when a WebSocket or 
                        // Server-Sent Events request comes in.

                        // Sending the access token in the query string is required due to
                        // a limitation in Browser APIs. We restrict it to only calls to the
                        // SignalR hub in this code.
                        // See https://docs.microsoft.com/aspnet/core/signalr/security#access-token-logging
                        // for more information about security considerations when using
                        // the query string to transmit the access token.
                        x.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // If the request is for our hub...
                                if (!context.HttpContext.IsSignalRHubRequest())
                                    return Task.CompletedTask;

                                // Read the token out of the query string
                                string? accessToken = context.Request.Query["access_token"];
                                if (!String.IsNullOrEmpty(accessToken)) 
                                    context.Token = accessToken;

                                return Task.CompletedTask;
                            }
                        };
                    });
            services.AddAuthorization(x =>
            {
                x.AddPolicy("HubConsumer", y => y.AddAuthenticationSchemes("HubConsumer")
                                                 .RequireAuthenticatedUser()
                                                 .RequireClaim(authorizationOptions.SubscriberIdClaimType)
                                                 .Build());
            });
            services.AddCors(x =>
            {
                x.AddDefaultPolicy(y => y.AllowCredentials()
                                         .AllowAnyHeader()
                                         .WithMethods("GET", "POST")
                                         .WithOrigins(corsConfiguration.AllowedOrigins ?? Array.Empty<string>()));
            });
            services.AddSignalR();
            services.AddSingleton<ITelephonyEventDispatcherFactory, TelephonyEventHubDispatcherFactory>();
            services.AddSingleton<IUserIdProvider, SubscriberIdClaimUserIdProvider>();

            TelephonyConnectorRegistrar.RegisterProvider(builder);

            WebApplication app = builder.Build();

            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHub<TelephonyHub>()
               .RequireAuthorization("HubConsumer");

            if (isDevelopment)
            {
                app.MapGet("/configuration", () => ConfigurationSerializer.DumpConfiguration(builder.Configuration));
            }

            TelephonyConnectorRegistrar.ConfigureProvider(app);

            await app.RunAsync().ConfigureAwait(false);
        }
    }
}