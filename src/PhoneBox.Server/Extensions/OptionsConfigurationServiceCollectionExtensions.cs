using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class OptionsConfigurationServiceCollectionExtensions
    {
        public static IServiceCollection Configure<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(this IServiceCollection services, IConfiguration config, string sectionName) where TOptions : class, new()
        {
            services.AddOptions<TOptions>()
                    .Bind(config.GetSection(sectionName))
                    .ValidateOnStart();

            return services;
        }
    }
}