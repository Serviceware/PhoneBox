using Microsoft.AspNetCore.Routing;
using PhoneBox.Generators.Tests;

namespace Microsoft.AspNetCore.Builder
{
    internal static class HubEndpointRouteBuilderExtensions
    {
        public static HubEndpointConventionBuilder MapHub<THub>(this IEndpointRouteBuilder endpoints) where THub : TelephonyHub
        {
            return endpoints.MapHub<THub>("/TelephonyHub");
        }
    }
}