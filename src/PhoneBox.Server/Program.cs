using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddSingleton<IUserIdProvider, PhoneNumberUserIdProvider>();
            services.AddSingleton<IHostedService, TelephonyHubPublisher>();

            TelephonyConnectorRegistrar.RegisterProvider(builder, services);

            WebApplication app = builder.Build();

            app.MapHub<TelephonyHub>("/TelephonyHub");
            app.MapMethods("/TelephonyHook", EnumerableExtensions.Create((isDevelopment ? HttpMethod.Get : HttpMethod.Post).Method), x => x.RequestServices.GetRequiredService<ITelephonyHook>().Handle(x));
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}