using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PhoneBox.Server.WebHook
{
    internal interface ITelephonyHook
    {
        Task HandleGet(string fromPhoneNumber, string toPhoneNumber, HttpContext context);
        Task HandlePost(WebHookRequest request, HttpContext context);
    }
}