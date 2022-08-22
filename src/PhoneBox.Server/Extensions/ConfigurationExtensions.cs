namespace Microsoft.Extensions.Configuration
{
    public static class ConfigurationExtensions
    {
        public static TOptions GetConfiguration<TOptions>(this IConfiguration configuration, string name) where TOptions : new()
        {
            TOptions instance = new TOptions();
            configuration.GetSection(name).Bind(instance);
            return instance;
        }
    }
}