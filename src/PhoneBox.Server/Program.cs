using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PhoneBox.Contracts;
using PhoneBox.TapiService;

namespace PhoneBox.Server
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            IServiceCollection services = builder.Services;
            services.AddSignalR();
            services.AddHostedService<TelephonyHubPublisher>();
            services.AddHostedService<TapiConnector>(x => x.GetRequiredService<TapiConnector>());
            services.AddSingleton<ITelephonyHook, TelephonyHook>();
            services.AddSingleton<TapiConnector, TapiConnector>();
            services.AddSingleton<ITelephonyConnector, TapiConnector>(x => x.GetRequiredService<TapiConnector>());
            services.AddSingleton<IUserIdProvider, PhoneNumberUserIdProvider>();

            WebApplication app = builder.Build();

            app.MapHub<TelephonyHub>("/TelephonyHub");
            app.MapGet("/TelephonyHook", x => x.RequestServices.GetRequiredService<ITelephonyHook>().Handle(x));
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}