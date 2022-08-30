using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhoneBox.Abstractions;
using PhoneBox.Server.WebHook;
using PhoneBox.TapiService;

namespace PhoneBox.Server
{
    internal static class TelephonyConnectorRegistrar
    {
        private static readonly IDictionary<TelephonyProvider, Type> Map = new Dictionary<TelephonyProvider, Type>
        {
            [TelephonyProvider.WebHook] = typeof(WebHookConnector)
          , [TelephonyProvider.Tapi] = typeof(TapiConnector)
        };

        public static void RegisterProvider(WebApplicationBuilder builder, IServiceCollection services)
        {
            TelephonyOptions configuration = GetConfiguration(builder.Configuration);
            if (configuration.Provider == TelephonyProvider.None)
            {
                throw new InvalidOperationException($"No telephony provider supported. Please set the Telephony.Provider property in appsettings.json. Possible values are: {String.Join(", ", Map.Keys)}");
            }

            Type connectorType = GetConnectorType(configuration.Provider);
            Type hostedServiceInterfaceType = typeof(IHostedService);
            Type telephonyConnectorInterfaceType = typeof(ITelephonyConnector);
            
            VerifyInterfacesImplemented(connectorType, telephonyConnectorInterfaceType/*, hostedServiceInterfaceType*/);

            services.AddSingleton(connectorType);
            services.AddSingleton(telephonyConnectorInterfaceType, x => x.GetRequiredService(connectorType));

            if (hostedServiceInterfaceType.IsAssignableFrom(connectorType))
                services.AddSingleton(hostedServiceInterfaceType, x => x.GetRequiredService(connectorType));
        }

        public static void SetupProvider(WebApplication application)
        {
            TelephonyOptions configuration = GetConfiguration(application.Configuration);
            application.Logger.LogInformation("Configured telephony connector: {provider}", configuration.Provider);
            if (application.Services.GetRequiredService(typeof(ITelephonyConnector)) is not ITelephonyConnectorSetup setup) 
                return;

            setup.Setup(application);
        }

        private static Type GetConnectorType(TelephonyProvider provider)
        {
            if (!Map.TryGetValue(provider, out Type? connectorType))
                throw new InvalidOperationException($"Telephony connector not registered: {provider}");

            return connectorType;
        }

        private static void VerifyInterfacesImplemented(Type connectorType, params Type[] interfaceTypes)
        {
            foreach (Type interfaceType in interfaceTypes)
            {
                if (!interfaceType.IsAssignableFrom(connectorType))
                    throw new InvalidOperationException($"Telephony provider '{connectorType}' does not implement '{interfaceType}'");
            }
        }

        private static TelephonyOptions GetConfiguration(IConfiguration configuration) => configuration.GetConfiguration<TelephonyOptions>("Telephony");
    }
}