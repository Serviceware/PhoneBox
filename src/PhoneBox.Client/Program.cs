using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PhoneBox.Client
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                             .ConfigureServices(services =>
                             {
                                 services.AddHostedService<TelephonyHubListener>();
                             })
                             .Build();

            await host.RunAsync().ConfigureAwait(false);
        }
    }
}