using System.Threading.Tasks;

namespace PhoneBox.Abstractions
{
    public interface ITelephonyHub
    {
        Task ReceiveCallConnected(CallConnectedEvent content);
        Task ReceiveCallDisconnected(CallDisconnectedEvent content);
    }
}