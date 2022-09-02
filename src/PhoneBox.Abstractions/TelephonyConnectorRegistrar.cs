using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneBox.Abstractions
{
    public abstract class TelephonyConnectorRegistrar<TImplementation> : ITelephonyConnectorRegistrar where TImplementation : ITelephonyConnector
    {
        public Type ImplementationType => typeof(TImplementation);

        public abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);
        public abstract void ConfigureApplication(WebApplication application);
    }
}