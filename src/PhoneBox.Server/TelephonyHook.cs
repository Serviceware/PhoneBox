using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PhoneBox.Server;

internal static class TelephonyHook
{
    public static Task Handle(HttpContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.WriteAsync("Thx!");
        return Task.CompletedTask;
    }
}