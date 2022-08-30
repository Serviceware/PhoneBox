using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnectorRegistrar
    {
        Type ImplementationType { get; }

        void ConfigureServices(IServiceCollection services);
        void ConfigureApplication(WebApplication application);
    }
}