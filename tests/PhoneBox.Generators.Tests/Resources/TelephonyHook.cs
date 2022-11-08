using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PhoneBox.Generators.Tests
{
    internal partial class TelephonyHook
    {
        Task ITelephonyHook.OnCallConnected(HttpContext context, WebHookCallConnectedRequest content) => Task.CompletedTask;
        Task ITelephonyHook.OnCallDisconnected(HttpContext context, WebHookCallDisconnectedRequest content) => Task.CompletedTask;
    }
}