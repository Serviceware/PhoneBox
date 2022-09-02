using System;
using System.Collections.Generic;
using System.Reflection;
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
            [TelephonyProvider.WebHook] = typeof(WebHookConnectorRegistrar)
          , [TelephonyProvider.Tapi] = typeof(TapiConnectorRegistrar)
        };

        public static void RegisterProvider(WebApplicationBuilder builder)
        {
            TelephonyOptions configuration = GetConfiguration(builder.Configuration);
            if (configuration.Provider == TelephonyProvider.None)
            {
                throw new InvalidOperationException($"No telephony provider supported. Please set the Telephony.Provider property in appsettings.json. Possible values are: {String.Join(", ", Map.Keys)}");
            }

            Type connectorFactoryType = GetConnectorType(configuration.Provider);
            Type connectorFactoryInterfaceType = typeof(ITelephonyConnectorRegistrar);

            VerifyInterfacesImplemented(connectorFactoryType, connectorFactoryInterfaceType);

            ConstructorInfo? defaultCtor = connectorFactoryType.GetConstructor(Type.EmptyTypes);
            if (defaultCtor == null)
                throw new InvalidOperationException($"Telephony provider type does not defined a parameterless constructor: {connectorFactoryType}");

            ITelephonyConnectorRegistrar registrar = (ITelephonyConnectorRegistrar)Activator.CreateInstance(connectorFactoryType)!;
            registrar.ConfigureServices(builder.Services, builder.Configuration);
            builder.Services.AddSingleton(registrar);

            Type connectorType = registrar.ImplementationType;
            Type hostedServiceInterfaceType = typeof(IHostedService);
            Type telephonyConnectorInterfaceType = typeof(ITelephonyConnector);
            
            VerifyInterfacesImplemented(connectorType, telephonyConnectorInterfaceType/*, hostedServiceInterfaceType*/);

            builder.Services.AddSingleton(connectorType);
            builder.Services.AddSingleton(telephonyConnectorInterfaceType, x => x.GetRequiredService(connectorType));

            if (hostedServiceInterfaceType.IsAssignableFrom(connectorType))
                builder.Services.AddSingleton(hostedServiceInterfaceType, x => x.GetRequiredService(connectorType));
        }

        public static void ConfigureProvider(WebApplication application)
        {
            TelephonyOptions configuration = GetConfiguration(application.Configuration);
            application.Logger.LogInformation("Configured telephony connector: {provider}", configuration.Provider);
            application.Services.GetRequiredService<ITelephonyConnectorRegistrar>().ConfigureApplication(application);
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

        private static TelephonyOptions GetConfiguration(IConfiguration configuration) => configuration.Bind<TelephonyOptions>("Telephony");
    }
}