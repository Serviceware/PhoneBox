using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PhoneBox.Client
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                             .ConfigureAppConfiguration(x =>
                             {
                                 x.AddJsonFile($"appsettings.{Environment.MachineName}.json", optional: true);
                             })
                             .ConfigureServices((context, services) =>
                             {
                                 services.AddHttpClient();
                                 services.Configure<AuthorizationOptions>(context.Configuration.GetSection("Authorization"));
                                 services.AddSingleton<IAccessTokenProvider, AccessTokenProvider>();
                                 services.AddHostedService<TelephonyHubListener>();
                             })
                             .Build();

            await host.RunAsync().ConfigureAwait(false);
        }
    }
}