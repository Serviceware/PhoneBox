namespace Microsoft.Extensions.Configuration
{
    public static class ConfigurationExtensions
    {
        public static TOptions Bind<TOptions>(this IConfiguration configuration, string name) where TOptions : new() => Bind<TOptions>(configuration.GetSection(name));
        public static TOptions Bind<TOptions>(this IConfiguration configuration) where TOptions : new()
        {
            TOptions instance = new TOptions();
            configuration.Bind(instance);
            return instance;
        }
    }
}