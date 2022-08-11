using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace PhoneBox.TapiService
{
    public sealed class TapiServiceListener : BackgroundService
    {
        public TapiServiceListener()
        {
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("TapiServiceListener started");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("TapiServiceListener stopped");
            return base.StopAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }
    }
}