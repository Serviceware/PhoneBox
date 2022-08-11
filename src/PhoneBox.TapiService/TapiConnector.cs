using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PhoneBox.Contracts;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnector : IHostedService, ITelephonyConnector
    {
        public TapiConnector()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}