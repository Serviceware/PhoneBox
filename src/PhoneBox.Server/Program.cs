using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

            WebApplication app = builder.Build();

            app.MapHub<TelephonyHub>("/TelephonyHub");
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}