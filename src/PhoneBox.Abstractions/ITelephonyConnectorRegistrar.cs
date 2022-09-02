using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyConnectorRegistrar
    {
        Type ImplementationType { get; }

        void ConfigureServices(IServiceCollection services, IConfiguration configuration);
        void ConfigureApplication(WebApplication application);
    }
}