using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddHostedService<TapiServiceListener>();
            services.AddSingleton<ITelephonyHook, TelephonyHook>();

            WebApplication app = builder.Build();

            app.MapHub<TelephonyHub>("/TelephonyHub");
            app.MapGet("/TelephonyHook", x => x.RequestServices.GetRequiredService<ITelephonyHook>().Handle(x));
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}