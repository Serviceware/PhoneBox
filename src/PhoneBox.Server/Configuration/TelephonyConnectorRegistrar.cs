using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhoneBox.Abstractions;
using PhoneBox.TapiService;

namespace PhoneBox.Server
{
    internal static class TelephonyConnectorRegistrar
    {
        private static readonly IDictionary<TelephonyProvider, Type> Map = new Dictionary<TelephonyProvider, Type>
        {
            [TelephonyProvider.Tapi] = typeof(TapiConnector)
        };

        public static void RegisterProvider(WebApplicationBuilder builder, IServiceCollection services)
        {
            TelephonyOptions configuration = builder.Configuration.GetConfiguration<TelephonyOptions>("Telephony");
            if (configuration.Provider == TelephonyProvider.None)
            {
                // No specific provider configured
                // By default web hook is enabled
                return;
            }
            Type connectorType = GetConnectorType(configuration.Provider);

            services.AddSingleton(connectorType);
            services.AddSingleton(typeof(ITelephonyConnector), x => x.GetRequiredService(connectorType));
            services.AddSingleton(typeof(IHostedService), x => x.GetRequiredService(connectorType));
        }

        private static Type GetConnectorType(TelephonyProvider provider)
        {
            if (!Map.TryGetValue(provider, out Type? connectorType))
                throw new InvalidOperationException($"Telephony connector not registered: {provider}");

            return connectorType;
        }
    }
}