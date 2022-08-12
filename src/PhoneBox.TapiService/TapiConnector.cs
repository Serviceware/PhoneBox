using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnector : IHostedService, ITelephonyConnector
    {
        private readonly ITelephonyHubPublisher _hubPublisher;
        private TAPI3Lib.TAPI? _tapiClient;

        public TapiConnector(ITelephonyHubPublisher hubPublisher)
        {
            this._hubPublisher = hubPublisher;
        }

        public void Register(CallSubscriber subscriber)
        {
            Subscribe(phoneNumber => this._hubPublisher.OnCall(subscriber, phoneNumber));
        }

        Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPI3Lib.TAPI();
            _tapiClient.Initialize();
            return Task.CompletedTask;
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            var tmp = _tapiClient;
            _tapiClient = null;
            if (tmp != null)
            {
                tmp.Shutdown();
            }
            return Task.CompletedTask;
        }

        private void Subscribe(Func<string, Task> onCall)
        {
            // TODO: Subscribe TAPI
        }
    }

    internal readonly struct Caller
    {
        public string PhoneNumber { get; }

        public Caller(string phoneNumber)
        {
            this.PhoneNumber = phoneNumber;
        }
    }
}