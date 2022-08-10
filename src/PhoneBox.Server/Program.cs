using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace PhoneBox.Server
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            WebApplication app = WebApplication.CreateBuilder(args)
                                               .Build();

            app.MapGet("/hi", () => "Hello!");
            
            await app.RunAsync().ConfigureAwait(false);
        }
    }
}
