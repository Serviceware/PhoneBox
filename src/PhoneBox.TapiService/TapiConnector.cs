﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnector : IHostedService, ITelephonyConnector
    {
        private TAPI3Lib.TAPI? _tapiClient;
        public TapiConnector()
        {
        }

        public void Register(string phoneNumber)
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _tapiClient = new TAPI3Lib.TAPI();
            _tapiClient.Initialize();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var tmp = _tapiClient;
            _tapiClient = null;
            if (tmp != null)
            {
                tmp.Shutdown();
            }
            return Task.CompletedTask;
        }
    }
}