using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PhoneBox.Abstractions;

namespace PhoneBox.TapiService
{
    public sealed class TapiConnectorRegistrar : TelephonyConnectorRegistrar<TapiConnector>
    {
        public override void ConfigureServices(IServiceCollection services) { }
        public override void ConfigureApplication(WebApplication application) { }
    }
}