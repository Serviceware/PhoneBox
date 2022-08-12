using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PhoneBox.Server
{
    internal interface ITelephonyHook
    {
        Task Handle(HttpContext context);
    }
}