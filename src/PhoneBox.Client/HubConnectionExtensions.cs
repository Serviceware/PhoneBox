using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR.Client
{
    internal static class HubConnectionExtensions
    {
        public static IDisposable On<T1>(this HubConnection hubConnection, Func<T1, Task> handler) => hubConnection.On(handler.Method.Name, handler);
    }
}