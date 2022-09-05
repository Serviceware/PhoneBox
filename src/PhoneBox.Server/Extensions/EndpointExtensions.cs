using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace PhoneBox.Server
{
    internal static class HubExtensions
    {
        public static bool IsSignalRHubRequest(this HttpContext context)
        {
            Endpoint? endpoint = context.GetEndpoint();
            if (endpoint == null)
                return false;

            return endpoint.Metadata.GetMetadata<HubMetadata>() != null;
        }
    }
}