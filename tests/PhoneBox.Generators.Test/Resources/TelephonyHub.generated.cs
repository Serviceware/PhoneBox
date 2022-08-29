using Microsoft.AspNetCore.SignalR;
using PhoneBox.Abstractions;

namespace PhoneBox.Generators.Tests
{
    public partial class TelephonyHub : Hub<ITelephonyHub>
    {
    }
}