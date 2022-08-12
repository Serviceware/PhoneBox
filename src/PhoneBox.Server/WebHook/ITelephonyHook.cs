using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PhoneBox.Server.WebHook
{
    internal interface ITelephonyHook
    {
        Task Handle(string phoneNumber, HttpContext context);
    }
}