using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;
using PhoneBox.Server.Authorization;
using PhoneBox.Server.Cors;
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

            IServiceCollection services = builder.Services;
            services.Configure<AuthorizationOptions>(builder.Configuration.GetSection(AuthorizationOptions.ConfigurationSectionName));
            services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.ConfigurationSectionName));
            services.ConfigureTarget<JwtBearerOptions>("HubConsumer", builder.Configuration)
                    .MapFrom<AuthorizationOptions>(AuthorizationOptions.ConfigurationSectionName, (from, to) =>
                    {
                        to.Authority = from.Authority;
                        to.TokenValidationParameters.ValidAudience = from.Audience;
                        to.RequireHttpsMetadata = !isDevelopment || from.Authority?.StartsWith("http:", StringComparison.OrdinalIgnoreCase) is null or false;
                    });
            
            services.AddLogging(x => x.AddSimpleConsole(y => y.TimestampFormat = "\x1B[1'm'\x1B[37'm'[yyyy-MM-dd HH:mm:ss.fff\x1B[39'm'\x1B[22'm'] "));

            services.AddAuthentication()
                    .AddJwtBearer("HubConsumer", x =>
                    {
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
            services.AddTransient<IAuthorizationPolicyProvider, AuthorizationPolicyProvider>();
            services.AddAuthorization(x =>
            {
                x.AddPolicy("HubConsumer", y => y.AddAuthenticationSchemes("HubConsumer")
                                                 .RequireAuthenticatedUser()
                                               // See additional configuration in AuthorizationPolicyProvider
                                               //.RequireClaim(authorizationOptions.SubscriberIdClaimType)
                                                 .Build());
            });

            services.AddTransient<ICorsPolicyProvider, CorsPolicyProvider>();
            services.AddCors(x =>
            {
                x.AddDefaultPolicy(y => y.AllowCredentials()
                                         .AllowAnyHeader()
                                         .WithMethods("GET", "POST")
                                       // See additional configuration in CorsPolicyProvider
                                       /*.WithOrigins(corsOptions.AllowedOrigins ?? Array.Empty<string>())*/);
            });
            services.AddHsts(x =>
            {
                x.Preload = true;
                x.IncludeSubDomains = true;
                x.MaxAge = TimeSpan.FromDays(730); // 2 years => https://hstspreload.org/
            });

            services.AddSignalR();

            services.AddSingleton<ITelephonyEventDispatcherFactory, TelephonyEventHubDispatcherFactory>();
            services.AddSingleton<IUserIdProvider, SubscriberIdClaimUserIdProvider>();

            TelephonyConnectorRegistrar.RegisterProvider(builder);

            WebApplication app = builder.Build();

            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();

            app.MapGeneratedHub<TelephonyHub>()
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